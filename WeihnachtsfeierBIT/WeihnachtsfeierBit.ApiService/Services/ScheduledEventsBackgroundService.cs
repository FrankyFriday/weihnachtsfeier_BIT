using Microsoft.EntityFrameworkCore;
using WeihnachtsfeierBit.Data;

namespace WeihnachtsfeierBit.ApiService.Services;

public sealed class ScheduledEventsBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<ScheduledEventsBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndStartScheduledEventsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Fehler im ScheduledEventsBackgroundService");
            }

            await Task.Delay(CheckInterval, stoppingToken);
        }
    }

    private async Task CheckAndStartScheduledEventsAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WeihnachtsfeierDbContext>();
        var gameStateNotifier = scope.ServiceProvider.GetRequiredService<IGameStateNotifier>();

        var now = DateTime.UtcNow;
        var dueEvents = await dbContext.GameInstanceEvents
            .Include(e => e.GameInstance)
            .Where(e => e.Status == "scheduled" && e.AutoStartOnSchedule && e.ScheduledStartUtc <= now)
            .ToListAsync(cancellationToken);

        if (dueEvents.Count == 0) return;

        foreach (var ev in dueEvents)
        {
            logger.LogInformation("Starte geplantes Event '{Title}' (ID: {EventId}) automatisch.", ev.Title, ev.Id);
            ev.Status = "active";
            ev.StartedAtUtc = now;

            if (ev.Mode == "active")
            {
                var otherEvents = await dbContext.GameInstanceEvents
                    .Where(e => e.GameInstanceId == ev.GameInstanceId && e.Id != ev.Id && e.Status == "active")
                    .ToListAsync(cancellationToken);

                foreach (var other in otherEvents)
                {
                    other.Mode = "passive";
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await gameStateNotifier.NotifyProjectChangedAsync(ev.GameInstance.ProjektId, cancellationToken);
        }
    }
}
