using WeihnachtsfeierBit.Data;

namespace WeihnachtsfeierBit.ApiService.Services;

public static class SessionEventOrderService
{
    public static int NextPosition(IEnumerable<GameInstanceEvent> existingEvents)
    {
        ArgumentNullException.ThrowIfNull(existingEvents);
        return existingEvents.Select(gameEvent => (int?)gameEvent.Position).Max() + 1 ?? 0;
    }

    /// <summary>
    /// Applies the requested order to the open events. Open events missing from the request keep
    /// their relative order behind the requested ones, so a concurrently added event is not lost.
    /// Returns an error message when the request is invalid; positions are then left unchanged.
    /// </summary>
    public static string? TryApplyOrder(IReadOnlyList<GameInstanceEvent> openEvents, IReadOnlyList<Guid> orderedEventIds)
    {
        ArgumentNullException.ThrowIfNull(openEvents);
        ArgumentNullException.ThrowIfNull(orderedEventIds);

        if (orderedEventIds.Distinct().Count() != orderedEventIds.Count)
        {
            return "Jedes Event darf nur einmal vorkommen.";
        }

        var eventsById = openEvents.ToDictionary(gameEvent => gameEvent.Id);
        if (orderedEventIds.Any(eventId => !eventsById.ContainsKey(eventId)))
        {
            return "Mindestens ein Event gehört nicht zu dieser Session oder ist bereits beendet.";
        }

        var requested = orderedEventIds.Select(eventId => eventsById[eventId]);
        var remaining = openEvents
            .Where(gameEvent => !orderedEventIds.Contains(gameEvent.Id))
            .OrderBy(gameEvent => gameEvent.Position)
            .ThenBy(gameEvent => gameEvent.StartedAtUtc)
            .ToList();

        var position = 0;
        foreach (var gameEvent in requested.Concat(remaining))
        {
            gameEvent.Position = position++;
        }

        return null;
    }
}
