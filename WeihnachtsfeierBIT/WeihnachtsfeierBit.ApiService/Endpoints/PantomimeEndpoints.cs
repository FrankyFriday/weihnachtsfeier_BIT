using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using WeihnachtsfeierBit.ApiService.Auth;
using WeihnachtsfeierBit.ApiService.Contracts;
using WeihnachtsfeierBit.ApiService.Services;
using WeihnachtsfeierBit.Data;
using PantomimeAdminData = WeihnachtsfeierBit.Framework.PantomimeAdminData;
using PantomimePerformerData = WeihnachtsfeierBit.Framework.PantomimePerformerData;

namespace WeihnachtsfeierBit.ApiService.Endpoints;

public static class PantomimeEndpoints
{
    private static readonly ConcurrentDictionary<(Guid EventId, Guid SlideId), SemaphoreSlim> GameLocks = new();

    public static void Map(RouteGroupBuilder reader, RouteGroupBuilder admin)
    {
        reader.MapGet("/session/events/{eventId:guid}/slides/{slideId:guid}/pantomime/current-term", GetCurrentTermAsync)
            .WithName("GetPantomimeCurrentTerm");
        reader.MapPost("/session/events/{eventId:guid}/slides/{slideId:guid}/pantomime/correct", MarkTermCorrectAsync)
            .WithName("MarkPantomimeTermCorrect");
        admin.MapGet("/session/events/{eventId:guid}/slides/{slideId:guid}/pantomime", GetPantomimeAsync)
            .WithName("GetPantomime");
        admin.MapPost("/session/events/{eventId:guid}/slides/{slideId:guid}/pantomime/admin/{action}", UpdatePantomimeAsync)
            .WithName("UpdatePantomime");
    }

    public sealed record TeamAssignmentRequest(Guid TeamId, Guid PerformerId);

    public sealed record AdminActionRequest(
        TeamAssignmentRequest[]? Assignments,
        int? RoundDurationSeconds);

    private static async Task<IResult> GetPantomimeAsync(
        Guid projectId,
        Guid eventId,
        Guid slideId,
        HttpContext httpContext,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var access = GetAccess(httpContext);
        if (!access.IsAdmin || !access.CanControlSession)
        {
            return Results.Forbid();
        }

        var gameEvent = await dbContext.GameInstanceEvents.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == eventId && item.GameInstance.ProjektId == projectId, cancellationToken);
        if (gameEvent is null)
        {
            return Results.NotFound();
        }

        var slide = FindPantomimeSlide(gameEvent.SlidesJson, slideId);
        if (slide is null)
        {
            return Results.NotFound();
        }

        var parameters = slide["parameters"] as JsonObject ?? new JsonObject();
        var termCount = PantomimeGameService.GetDistinctTerms(parameters["terms"] as JsonArray).Length;
        var state = JsonSerializer.SerializeToElement(parameters["state"] ?? new JsonObject());
        return Results.Ok(new PantomimeAdminData(
            parameters["category"]?.GetValue<string>() ?? string.Empty,
            termCount,
            parameters["roundDurationSeconds"]?.GetValue<int>() ?? 60,
            JsonSerializer.SerializeToElement(PantomimeGameService.PublicState(state))));
    }

    private static async Task<IResult> UpdatePantomimeAsync(
        Guid projectId,
        Guid eventId,
        Guid slideId,
        string action,
        AdminActionRequest request,
        HttpContext httpContext,
        WeihnachtsfeierDbContext dbContext,
        IGameStateNotifier gameStateNotifier,
        CancellationToken cancellationToken)
    {
        var access = GetAccess(httpContext);
        if (!access.IsAdmin || !access.CanControlSession)
        {
            return Results.Forbid();
        }

        if (action is not ("start" or "pause" or "resume" or "end"))
        {
            return Results.BadRequest(new ApiError("Unbekannte Pantomime-Aktion."));
        }

        var gameLock = GameLocks.GetOrAdd((eventId, slideId), _ => new SemaphoreSlim(1, 1));
        await gameLock.WaitAsync(cancellationToken);
        try
        {
            var gameEvent = await dbContext.GameInstanceEvents
                .FirstOrDefaultAsync(item => item.Id == eventId && item.GameInstance.ProjektId == projectId, cancellationToken);
            if (gameEvent is null)
            {
                return Results.NotFound();
            }

            var slides = JsonNode.Parse(gameEvent.SlidesJson) as JsonArray;
            var slide = slides is null ? null : FindPantomimeSlide(slides, slideId);
            if (slide is null)
            {
                return Results.NotFound();
            }

            var parameters = slide["parameters"] as JsonObject ?? new JsonObject();
            var terms = PantomimeGameService.GetDistinctTerms(parameters["terms"] as JsonArray);
            if (terms.Length == 0)
            {
                return Results.BadRequest(new ApiError("Für diese Pantomime-Slide sind keine Begriffe hinterlegt."));
            }

            var state = parameters["state"] as JsonObject ?? new JsonObject();
            var now = DateTime.UtcNow;
            var expired = PantomimeGameService.ExpireIfNeeded(state, now);
            if (expired && action != "start")
            {
                await SaveRoundStateAsync(gameEvent, slides!, slide, parameters, state, dbContext, gameStateNotifier, projectId, cancellationToken);
                return action == "end"
                    ? Results.Ok()
                    : Results.Conflict(new ApiError("Die Pantomime-Runde ist abgelaufen."));
            }

            switch (action)
            {
                case "start":
                    if (PantomimeGameService.ReadBool(state, "isRunning") || PantomimeGameService.ReadBool(state, "isPaused"))
                    {
                        return Results.Conflict(new ApiError("Die Pantomime-Runde läuft bereits."));
                    }

                    var teams = await dbContext.Teams.AsNoTracking()
                        .Where(team => team.ProjektId == projectId)
                        .OrderBy(team => team.DisplayName)
                        .ToArrayAsync(cancellationToken);
                    if (teams.Length == 0
                        || request.Assignments is null
                        || request.Assignments.Length != teams.Length
                        || request.Assignments.Select(assignment => assignment.TeamId).Distinct().Count() != teams.Length)
                    {
                        return Results.BadRequest(new ApiError("Wähle für jedes Team genau eine darstellende Person aus."));
                    }

                    var assignmentsByTeam = request.Assignments.ToDictionary(assignment => assignment.TeamId);
                    if (teams.Any(team => !assignmentsByTeam.ContainsKey(team.Id)))
                    {
                        return Results.BadRequest(new ApiError("Für jedes Projekt-Team muss eine darstellende Person ausgewählt werden."));
                    }

                    var performerIds = request.Assignments.Select(assignment => assignment.PerformerId).Distinct().ToArray();
                    var participants = await dbContext.Teilnehmer.AsNoTracking()
                        .Where(participant => participant.ProjektId == projectId && performerIds.Contains(participant.Id))
                        .ToDictionaryAsync(participant => participant.Id, cancellationToken);

                    var gameAssignments = new List<PantomimeTeamAssignment>(teams.Length);
                    foreach (var team in teams)
                    {
                        var assignment = assignmentsByTeam[team.Id];
                        if (!participants.TryGetValue(assignment.PerformerId, out var performer)
                            || performer.TeamId != team.Id)
                        {
                            return Results.BadRequest(new ApiError("Jede darstellende Person muss dem zugehörigen Projekt-Team angehören."));
                        }

                        gameAssignments.Add(new PantomimeTeamAssignment(
                            team.Id,
                            team.DisplayName,
                            performer.Id,
                            string.IsNullOrWhiteSpace(performer.Name) ? $"Person {performer.Nummer}" : performer.Name));
                    }

                    var duration = request.RoundDurationSeconds ?? parameters["roundDurationSeconds"]?.GetValue<int>() ?? 60;
                    if (duration is < 10 or > 600)
                    {
                        return Results.BadRequest(new ApiError("Die Rundendauer muss zwischen 10 und 600 Sekunden liegen."));
                    }

                    parameters["roundDurationSeconds"] = duration;
                    state = PantomimeGameService.CreateRoundState(gameAssignments, terms.Length, duration, now);
                    break;
                case "pause":
                    if (!PantomimeGameService.ReadBool(state, "isRunning"))
                    {
                        return Results.Conflict(new ApiError("Es läuft keine Pantomime-Runde."));
                    }

                    var remainingSeconds = PantomimeGameService.RemainingSeconds(state, now);
                    if (remainingSeconds == 0)
                    {
                        PantomimeGameService.EndRound(state);
                        break;
                    }

                    state["remainingSeconds"] = remainingSeconds;
                    state["isRunning"] = false;
                    state["isPaused"] = true;
                    break;
                case "resume":
                    if (!PantomimeGameService.ReadBool(state, "isPaused"))
                    {
                        return Results.Conflict(new ApiError("Die Pantomime-Runde ist nicht pausiert."));
                    }

                    var secondsToResume = PantomimeGameService.ReadInt(state, "remainingSeconds");
                    if (secondsToResume <= 0)
                    {
                        PantomimeGameService.EndRound(state);
                        break;
                    }

                    state["deadlineUtc"] = now.AddSeconds(secondsToResume);
                    state["isPaused"] = false;
                    state["isRunning"] = true;
                    break;
                case "end":
                    if (!PantomimeGameService.ReadBool(state, "isRunning") && !PantomimeGameService.ReadBool(state, "isPaused"))
                    {
                        return Results.Conflict(new ApiError("Es läuft keine Pantomime-Runde."));
                    }

                    PantomimeGameService.EndRound(state);
                    break;
            }

            await SaveRoundStateAsync(gameEvent, slides!, slide, parameters, state, dbContext, gameStateNotifier, projectId, cancellationToken);
            return Results.Ok();
        }
        finally
        {
            gameLock.Release();
        }
    }

    private static async Task<IResult> GetCurrentTermAsync(
        Guid projectId,
        Guid eventId,
        Guid slideId,
        HttpContext httpContext,
        WeihnachtsfeierDbContext dbContext,
        IGameStateNotifier gameStateNotifier,
        CancellationToken cancellationToken)
    {
        var access = GetAccess(httpContext);
        if (!access.IsParticipant || access.ParticipantId is not Guid participantId)
        {
            return Results.Forbid();
        }

        var gameLock = GameLocks.GetOrAdd((eventId, slideId), _ => new SemaphoreSlim(1, 1));
        await gameLock.WaitAsync(cancellationToken);
        try
        {
            var gameEvent = await dbContext.GameInstanceEvents
                .FirstOrDefaultAsync(item => item.Id == eventId && item.GameInstance.ProjektId == projectId, cancellationToken);
            if (gameEvent is null)
            {
                return Results.NotFound();
            }

            var slides = JsonNode.Parse(gameEvent.SlidesJson) as JsonArray;
            var slide = slides is null ? null : FindPantomimeSlide(slides, slideId);
            if (slide is null)
            {
                return Results.NotFound();
            }

            var parameters = slide["parameters"] as JsonObject ?? new JsonObject();
            var terms = PantomimeGameService.GetDistinctTerms(parameters["terms"] as JsonArray);
            var state = parameters["state"] as JsonObject ?? new JsonObject();
            var expired = PantomimeGameService.ExpireIfNeeded(state, DateTime.UtcNow);
            var teamState = PantomimeGameService.FindPerformerTeam(state, participantId);
            if (teamState is null)
            {
                return Results.Forbid();
            }

            var currentTermIndex = PantomimeGameService.GetCurrentTermIndex(teamState);
            string? currentTerm = currentTermIndex is int index && index >= 0 && index < terms.Length
                && (PantomimeGameService.ReadBool(state, "isRunning") || PantomimeGameService.ReadBool(state, "isPaused"))
                    ? terms[index]
                    : null;

            if (expired)
            {
                await SaveRoundStateAsync(gameEvent, slides!, slide, parameters, state, dbContext, gameStateNotifier, projectId, cancellationToken);
            }

            var now = DateTime.UtcNow;
            return Results.Ok(new PantomimePerformerData(
                parameters["category"]?.GetValue<string>() ?? string.Empty,
                PantomimeGameService.ReadString(teamState, "teamName"),
                currentTerm,
                PantomimeGameService.ReadBool(state, "isRunning"),
                PantomimeGameService.ReadBool(state, "isPaused"),
                PantomimeGameService.ReadBool(state, "isEnded"),
                PantomimeGameService.RemainingSeconds(state, now),
                PantomimeGameService.ReadInt(teamState, "correctCount")));
        }
        finally
        {
            gameLock.Release();
        }
    }

    private static async Task<IResult> MarkTermCorrectAsync(
        Guid projectId,
        Guid eventId,
        Guid slideId,
        HttpContext httpContext,
        WeihnachtsfeierDbContext dbContext,
        IGameStateNotifier gameStateNotifier,
        CancellationToken cancellationToken)
    {
        var access = GetAccess(httpContext);
        if (!access.IsParticipant || access.ParticipantId is not Guid participantId)
        {
            return Results.Forbid();
        }

        var gameLock = GameLocks.GetOrAdd((eventId, slideId), _ => new SemaphoreSlim(1, 1));
        await gameLock.WaitAsync(cancellationToken);
        try
        {
            var gameEvent = await dbContext.GameInstanceEvents
                .FirstOrDefaultAsync(item => item.Id == eventId && item.GameInstance.ProjektId == projectId, cancellationToken);
            if (gameEvent is null)
            {
                return Results.NotFound();
            }

            var slides = JsonNode.Parse(gameEvent.SlidesJson) as JsonArray;
            var slide = slides is null ? null : FindPantomimeSlide(slides, slideId);
            if (slide is null)
            {
                return Results.NotFound();
            }

            var parameters = slide["parameters"] as JsonObject ?? new JsonObject();
            var terms = PantomimeGameService.GetDistinctTerms(parameters["terms"] as JsonArray);
            var state = parameters["state"] as JsonObject ?? new JsonObject();
            var now = DateTime.UtcNow;
            if (PantomimeGameService.ExpireIfNeeded(state, now))
            {
                await SaveRoundStateAsync(gameEvent, slides!, slide, parameters, state, dbContext, gameStateNotifier, projectId, cancellationToken);
                return Results.Conflict(new ApiError("Die Pantomime-Runde ist abgelaufen."));
            }

            if (!PantomimeGameService.ReadBool(state, "isRunning"))
            {
                return Results.Conflict(new ApiError("Die Pantomime-Runde läuft nicht."));
            }

            var teamState = PantomimeGameService.FindPerformerTeam(state, participantId);
            var termIndex = teamState is null ? null : PantomimeGameService.GetCurrentTermIndex(teamState);
            if (teamState is null || termIndex is null || termIndex < 0 || termIndex >= terms.Length)
            {
                return Results.Forbid();
            }

            var teamId = PantomimeGameService.ReadGuid(teamState, "teamId");
            var team = teamId is Guid id
                ? await dbContext.Teams.FirstOrDefaultAsync(item => item.Id == id && item.ProjektId == projectId, cancellationToken)
                : null;
            if (team is null)
            {
                return Results.Conflict(new ApiError("Das Team der darstellenden Person existiert nicht mehr."));
            }

            team.Punkte = checked(team.Punkte + 1);
            dbContext.TeamPointLogs.Add(new TeamPointLog
            {
                ProjektId = projectId,
                TeamId = team.Id,
                DeltaPoints = 1,
                NewTotalPoints = team.Punkte,
                Reason = "Pantomime-Begriff erraten",
                CreatedAtUtc = now
            });
            teamState["correctCount"] = PantomimeGameService.ReadInt(teamState, "correctCount") + 1;
            PantomimeGameService.AssignNextTerm(state, teamState);
            await SaveRoundStateAsync(gameEvent, slides!, slide, parameters, state, dbContext, gameStateNotifier, projectId, cancellationToken);
            return Results.Ok();
        }
        finally
        {
            gameLock.Release();
        }
    }

    private static async Task SaveRoundStateAsync(
        GameInstanceEvent gameEvent,
        JsonArray slides,
        JsonObject slide,
        JsonObject parameters,
        JsonObject state,
        WeihnachtsfeierDbContext dbContext,
        IGameStateNotifier gameStateNotifier,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        parameters["state"] = state;
        slide["parameters"] = parameters;
        gameEvent.SlidesJson = slides.ToJsonString();
        await dbContext.SaveChangesAsync(cancellationToken);
        await gameStateNotifier.NotifyProjectChangedAsync(projectId, cancellationToken);
    }

    private static ProjectAccess GetAccess(HttpContext httpContext) =>
        (ProjectAccess)httpContext.Items[RequireProjectAccessFilter.AccessItemKey]!;

    private static JsonObject? FindPantomimeSlide(string slidesJson, Guid slideId) =>
        JsonNode.Parse(slidesJson) is JsonArray slides ? FindPantomimeSlide(slides, slideId) : null;

    private static JsonObject? FindPantomimeSlide(JsonArray slides, Guid slideId)
    {
        var slide = PantomimeGameService.FindSlide(slides, slideId);
        return string.Equals(slide?["type"]?.GetValue<string>(), "pantomime", StringComparison.OrdinalIgnoreCase)
            ? slide
            : null;
    }
}
