using Microsoft.AspNetCore.Components.Server.Circuits;

namespace StoryPoints.Services;

/// <summary>
/// Scoped per Blazor circuit. A room page registers its membership here after joining;
/// the circuit lifecycle then drives presence: members are removed from the live room as
/// soon as the connection drops, and re-added if it comes back (transient network loss).
/// </summary>
public sealed class CircuitMemberTracker : CircuitHandler
{
    private readonly RoomCoordinator _coordinator;
    private readonly object _lock = new();
    private readonly Dictionary<(Guid RoomId, Guid MemberId), string> _memberships = new();

    public CircuitMemberTracker(RoomCoordinator coordinator) => _coordinator = coordinator;

    public void Track(Guid roomId, Guid memberId, string name)
    {
        lock (_lock) _memberships[(roomId, memberId)] = name;
    }

    public void Untrack(Guid roomId, Guid memberId)
    {
        lock (_lock) _memberships.Remove((roomId, memberId));
    }

    public override Task OnConnectionDownAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        foreach (var ((roomId, memberId), _) in Snapshot())
        {
            _coordinator.TryGet(roomId)?.Leave(memberId);
        }
        return Task.CompletedTask;
    }

    public override Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        // No-op on the first connection (nothing tracked yet); re-joins after a reconnection.
        foreach (var ((roomId, memberId), name) in Snapshot())
        {
            _coordinator.TryGet(roomId)?.Join(memberId, name);
        }
        return Task.CompletedTask;
    }

    private KeyValuePair<(Guid RoomId, Guid MemberId), string>[] Snapshot()
    {
        lock (_lock) return _memberships.ToArray();
    }
}
