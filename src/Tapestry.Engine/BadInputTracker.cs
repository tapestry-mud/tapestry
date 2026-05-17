using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;

namespace Tapestry.Engine;

public sealed record BadInputEntry(string Verb, int Count, DateTime FirstSeen, DateTime LastSeen);

public class BadInputTracker
{
    private readonly ILogger<BadInputTracker> _logger;
    private readonly Counter<long>? _metric;
    private readonly ConcurrentDictionary<string, BadInputEntry> _entries = new();

    public BadInputTracker(ILogger<BadInputTracker> logger, Counter<long>? metric)
    {
        _logger = logger;
        _metric = metric;
    }

    public void Record(string verb, string fullInput, string playerName, string? roomId)
    {
        var key = verb.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(key)) { return; }

        var now = DateTime.UtcNow;
        _entries.AddOrUpdate(
            key,
            _ => new BadInputEntry(key, 1, now, now),
            (_, existing) => existing with { Count = existing.Count + 1, LastSeen = now });

        _logger.LogInformation(
            "bad_input: verb={Verb} input={FullInput} player={PlayerName} room={RoomId}",
            key, fullInput, playerName, roomId ?? "");

        _metric?.Add(1, new KeyValuePair<string, object?>("verb", key));
    }

    public void Clear()
    {
        _entries.Clear();
    }

    public IReadOnlyList<BadInputEntry> GetEntries()
    {
        return _entries.Values
            .OrderByDescending(e => e.Count)
            .ToList();
    }
}
