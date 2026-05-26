using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using StoryPoints.Data;

namespace StoryPoints.Services;

public sealed class RoomCoordinator
{
    private const int CountdownSeconds = 10;
    private const double RevealThreshold = 2.0 / 3.0;

    private readonly IOptionsMonitor<RoomLimitOptions> _options;
    private readonly ConcurrentDictionary<Guid, RoomState> _rooms = new();
    private readonly ConcurrentDictionary<Guid, DateTime> _pendingActivity = new();

    public RoomCoordinator(IOptionsMonitor<RoomLimitOptions> options)
    {
        _options = options;
    }

    public RoomState GetOrCreate(Guid roomId, Scale scale, TimeSpan? maxVoteDuration)
    {
        return _rooms.GetOrAdd(roomId, id =>
        {
            var state = new RoomState(id, scale, maxVoteDuration, CountdownSeconds, RevealThreshold,
                _options.CurrentValue.MaxMembersPerRoom);
            state.Changed += () => _pendingActivity[id] = DateTime.UtcNow;
            _pendingActivity[id] = DateTime.UtcNow;
            return state;
        });
    }

    public RoomState? TryGet(Guid roomId) =>
        _rooms.TryGetValue(roomId, out var state) ? state : null;

    public IReadOnlyDictionary<Guid, DateTime> DrainActivity()
    {
        var snapshot = new Dictionary<Guid, DateTime>();
        foreach (var key in _pendingActivity.Keys.ToList())
        {
            if (_pendingActivity.TryRemove(key, out var ts))
            {
                snapshot[key] = ts;
            }
        }
        return snapshot;
    }

    public void Evict(Guid roomId)
    {
        _rooms.TryRemove(roomId, out _);
        _pendingActivity.TryRemove(roomId, out _);
    }
}

public sealed record MemberView(Guid Id, string Name, bool HasVoted, string? Vote, bool IsAfk);

public sealed class RoomState
{
    private readonly object _lock = new();
    private readonly Dictionary<Guid, string> _members = new();
    private readonly Dictionary<Guid, string> _votes = new();
    private readonly HashSet<Guid> _afk = new();
    private readonly int _countdownSeconds;
    private readonly double _revealThreshold;
    private readonly TimeSpan? _maxVoteDuration;
    private readonly int _maxMembers;

    private CancellationTokenSource? _countdownCts;
    private CancellationTokenSource? _voteDeadlineCts;

    public Guid Id { get; }
    public Scale Scale { get; }
    public int Round { get; private set; } = 1;
    public bool Revealed { get; private set; }
    public DateTime? CountdownDeadlineUtc { get; private set; }
    public DateTime? VoteDeadlineUtc { get; private set; }
    public int MaxMembers => _maxMembers;

    public event Action? Changed;

    public RoomState(Guid id, Scale scale, TimeSpan? maxVoteDuration, int countdownSeconds, double revealThreshold, int maxMembers)
    {
        Id = id;
        Scale = scale;
        _maxVoteDuration = maxVoteDuration;
        _countdownSeconds = countdownSeconds;
        _revealThreshold = revealThreshold;
        _maxMembers = maxMembers;
    }

    public bool Join(Guid memberId, string name)
    {
        lock (_lock)
        {
            if (!_members.ContainsKey(memberId) && _members.Count >= _maxMembers)
                return false;
            _members[memberId] = name;
        }
        Evaluate();
        RaiseChanged();
        return true;
    }

    public void Leave(Guid memberId)
    {
        lock (_lock)
        {
            _members.Remove(memberId);
            _votes.Remove(memberId);
            _afk.Remove(memberId);
            if (_votes.Count == 0)
            {
                CancelVoteDeadlineLocked();
            }
        }
        Evaluate();
        RaiseChanged();
    }

    public void SetAfk(Guid memberId, bool isAfk)
    {
        lock (_lock)
        {
            if (!_members.ContainsKey(memberId)) return;

            bool changed = isAfk ? _afk.Add(memberId) : _afk.Remove(memberId);
            if (!changed) return;

            if (isAfk)
            {
                _votes.Remove(memberId);
                if (_votes.Count == 0)
                {
                    CancelVoteDeadlineLocked();
                }
            }
        }
        Evaluate();
        RaiseChanged();
    }

    public void Vote(Guid memberId, string value)
    {
        CancellationTokenSource? deadlineToStart = null;

        lock (_lock)
        {
            if (Revealed) return;
            if (!_members.ContainsKey(memberId)) return;
            if (_afk.Contains(memberId)) return;

            bool firstVoteOfRound = _votes.Count == 0;
            _votes[memberId] = value;

            if (firstVoteOfRound && _maxVoteDuration is { } duration)
            {
                _voteDeadlineCts = new CancellationTokenSource();
                VoteDeadlineUtc = DateTime.UtcNow.Add(duration);
                deadlineToStart = _voteDeadlineCts;
            }
        }

        if (deadlineToStart is not null && _maxVoteDuration is { } d)
        {
            _ = RunVoteDeadlineAsync(d, deadlineToStart.Token);
        }

        Evaluate();
        RaiseChanged();
    }

    public void ResetRound()
    {
        lock (_lock)
        {
            Round++;
            Revealed = false;
            _votes.Clear();
            CancelCountdownLocked();
            CancelVoteDeadlineLocked();
        }
        RaiseChanged();
    }

    public IReadOnlyList<MemberView> Snapshot()
    {
        lock (_lock)
        {
            return _members
                .Select(kv =>
                {
                    var isAfk = _afk.Contains(kv.Key);
                    var hasVoted = !isAfk && _votes.ContainsKey(kv.Key);
                    var vote = Revealed && hasVoted ? _votes[kv.Key] : null;
                    return new MemberView(kv.Key, kv.Value, hasVoted, vote, isAfk);
                })
                .OrderBy(m => m.Name)
                .ToList();
        }
    }

    public string? GetMyVote(Guid memberId)
    {
        lock (_lock)
        {
            return _votes.TryGetValue(memberId, out var v) ? v : null;
        }
    }

    public VoteResults? ResultsIfRevealed()
    {
        lock (_lock)
        {
            if (!Revealed) return null;
            var votes = _members
                .Where(m => _votes.ContainsKey(m.Key))
                .Select(m => new MemberVote(m.Value, _votes[m.Key]))
                .ToList();
            return ResultCalculator.Compute(votes, ScaleProvider.Cards(Scale));
        }
    }

    private void Evaluate()
    {
        CancellationTokenSource? toStart = null;

        lock (_lock)
        {
            if (Revealed) return;

            int total = _members.Count(m => !_afk.Contains(m.Key));
            int voted = _votes.Count(v => _members.ContainsKey(v.Key) && !_afk.Contains(v.Key));

            if (total == 0)
            {
                CancelCountdownLocked();
                return;
            }

            if (voted == total)
            {
                CancelCountdownLocked();
                CancelVoteDeadlineLocked();
                Revealed = true;
            }
            else if ((double)voted / total >= _revealThreshold)
            {
                if (_countdownCts is null)
                {
                    _countdownCts = new CancellationTokenSource();
                    CountdownDeadlineUtc = DateTime.UtcNow.AddSeconds(_countdownSeconds);
                    toStart = _countdownCts;
                }
            }
            else
            {
                CancelCountdownLocked();
            }
        }

        if (toStart is not null)
        {
            _ = RunCountdownAsync(toStart.Token);
        }
    }

    private async Task RunCountdownAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(_countdownSeconds), token);
        }
        catch (TaskCanceledException) { return; }

        bool reveal = false;
        lock (_lock)
        {
            if (_countdownCts is not null && _countdownCts.Token == token && !Revealed)
            {
                Revealed = true;
                CountdownDeadlineUtc = null;
                _countdownCts.Dispose();
                _countdownCts = null;
                CancelVoteDeadlineLocked();
                reveal = true;
            }
        }
        if (reveal) RaiseChanged();
    }

    private async Task RunVoteDeadlineAsync(TimeSpan duration, CancellationToken token)
    {
        try
        {
            await Task.Delay(duration, token);
        }
        catch (TaskCanceledException) { return; }

        bool reveal = false;
        lock (_lock)
        {
            if (_voteDeadlineCts is not null && _voteDeadlineCts.Token == token && !Revealed)
            {
                Revealed = true;
                VoteDeadlineUtc = null;
                _voteDeadlineCts.Dispose();
                _voteDeadlineCts = null;
                CancelCountdownLocked();
                reveal = true;
            }
        }
        if (reveal) RaiseChanged();
    }

    private void CancelCountdownLocked()
    {
        if (_countdownCts is null) return;
        _countdownCts.Cancel();
        _countdownCts.Dispose();
        _countdownCts = null;
        CountdownDeadlineUtc = null;
    }

    private void CancelVoteDeadlineLocked()
    {
        if (_voteDeadlineCts is null) return;
        _voteDeadlineCts.Cancel();
        _voteDeadlineCts.Dispose();
        _voteDeadlineCts = null;
        VoteDeadlineUtc = null;
    }

    private void RaiseChanged() => Changed?.Invoke();
}
