using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using WeihnachtsfeierBit.Data;

namespace WeihnachtsfeierBit.ApiService.Services;

public sealed class QuestionWorkflowBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<QuestionWorkflowBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await AdvanceDueQuestionPhasesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Fehler im QuestionWorkflowBackgroundService");
            }

            await Task.Delay(CheckInterval, stoppingToken);
        }
    }

    private async Task AdvanceDueQuestionPhasesAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WeihnachtsfeierDbContext>();
        var notifier = scope.ServiceProvider.GetRequiredService<IGameStateNotifier>();
        var activeEvents = await dbContext.GameInstanceEvents
            .Include(gameEvent => gameEvent.GameInstance)
            .Where(gameEvent => gameEvent.Status == "active" && gameEvent.Mode == "active")
            .ToListAsync(cancellationToken);

        var changedProjects = new HashSet<Guid>();
        var now = DateTimeOffset.UtcNow;

        foreach (var gameEvent in activeEvents)
        {
            if (JsonNode.Parse(gameEvent.SlidesJson) is not JsonArray slides
                || gameEvent.CurrentSlideIndex < 0
                || gameEvent.CurrentSlideIndex >= slides.Count
                || slides[gameEvent.CurrentSlideIndex] is not JsonObject currentSlide
                || !QuestionWorkflowService.IsQuestionSlide(currentSlide))
            {
                continue;
            }

            var workflow = QuestionWorkflowService.GetWorkflowState(currentSlide);
            var changed = false;
            if (gameEvent.GameInstance.IsPaused)
            {
                if (!ReadBool(workflow, "sessionPaused"))
                {
                    workflow["sessionPaused"] = true;
                    workflow["sessionTimerWasPaused"] = ReadBool(workflow, "timerPaused");
                    if (!ReadBool(workflow, "timerPaused"))
                    {
                        workflow["remainingSeconds"] = QuestionWorkflowService.GetRemainingSeconds(currentSlide, now);
                        workflow["timerPaused"] = true;
                    }
                    changed = true;
                }
            }
            else if (ReadBool(workflow, "sessionPaused"))
            {
                var wasPaused = ReadBool(workflow, "sessionTimerWasPaused");
                workflow["sessionPaused"] = false;
                workflow["sessionTimerWasPaused"] = false;
                if (!wasPaused)
                {
                    var remainingSeconds = ReadInt(workflow, "remainingSeconds") ?? 0;
                    workflow["timerPaused"] = false;
                    workflow["timerEndsAtUtc"] = remainingSeconds > 0
                        ? now.AddSeconds(remainingSeconds).ToString("O")
                        : null;
                }
                changed = true;
            }

            if (!gameEvent.GameInstance.IsPaused)
            {
                changed |= QuestionWorkflowService.TickActiveSlide(currentSlide, now);
            }

            if (changed)
            {
                gameEvent.SlidesJson = slides.ToJsonString();
                changedProjects.Add(gameEvent.GameInstance.ProjektId);
            }
        }

        if (changedProjects.Count == 0)
        {
            return;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        foreach (var projectId in changedProjects)
        {
            await notifier.NotifyProjectChangedAsync(projectId, cancellationToken);
        }
    }

    private static int? ReadInt(JsonObject value, string propertyName) =>
        value[propertyName] is JsonValue jsonValue && jsonValue.TryGetValue<int>(out var number)
            ? number
            : null;

    private static bool ReadBool(JsonObject value, string propertyName) =>
        value[propertyName] is JsonValue jsonValue && jsonValue.TryGetValue<bool>(out var result) && result;
}
