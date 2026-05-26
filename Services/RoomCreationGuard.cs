using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace StoryPoints.Services;

public enum CreationCheckResult
{
    Allowed,
    RateLimited,
    GlobalCapReached
}

public sealed class RoomCreationGuard
{
    private readonly IOptionsMonitor<RoomLimitOptions> _options;
    private readonly ConcurrentDictionary<string, Queue<DateTime>> _perIp = new();

    public RoomCreationGuard(IOptionsMonitor<RoomLimitOptions> options)
    {
        _options = options;
    }

    public CreationCheckResult CanCreate(string clientKey, int currentRoomCount)
    {
        var opts = _options.CurrentValue;

        if (currentRoomCount >= opts.MaxTotalRooms)
        {
            return CreationCheckResult.GlobalCapReached;
        }

        var bucket = _perIp.GetOrAdd(clientKey, _ => new Queue<DateTime>());
        lock (bucket)
        {
            var cutoff = DateTime.UtcNow.AddHours(-1);
            while (bucket.Count > 0 && bucket.Peek() < cutoff)
            {
                bucket.Dequeue();
            }

            if (bucket.Count >= opts.MaxCreationsPerHourPerIp)
            {
                return CreationCheckResult.RateLimited;
            }
        }

        return CreationCheckResult.Allowed;
    }

    public void RegisterCreation(string clientKey)
    {
        var bucket = _perIp.GetOrAdd(clientKey, _ => new Queue<DateTime>());
        lock (bucket)
        {
            bucket.Enqueue(DateTime.UtcNow);
        }
    }
}
