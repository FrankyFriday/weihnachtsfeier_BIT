using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using WeihnachtsfeierBit.ApiService.Auth;
using WeihnachtsfeierBit.ApiService.Contracts;
using WeihnachtsfeierBit.ApiService.Services;
using WeihnachtsfeierBit.Data;
using PantomimeAdminData = WeihnachtsfeierBit.Framework.PantomimeAdminData;
using PantomimeModeratorTeamData = WeihnachtsfeierBit.Framework.PantomimeModeratorTeamData;
using PantomimePerformerData = WeihnachtsfeierBit.Framework.PantomimePerformerData;

namespace WeihnachtsfeierBit.ApiService.Endpoints;

public static class PantomimeEndpoints
{
    private static readonly ConcurrentDictionary<(Guid EventId, Guid SlideId), SemaphoreSlim> GameLocks = new();

    public static void Map(RouteGroupBuilder reader, RouteGroupBuilder admin)
    {
        reader.MapGet("/session/events/{eventId:guid}/slides/{slideId:guid}/pantomime/current-term", GetCurrentTermAsync)
            .WithName("GetPantomimeCurrentTerm");
        reader.MapPost("/session/events/{eventId:guid}/slides/{slideId:guid}/pantomime/skip", SkipCurrentTermAsync)
            .WithName("SkipPantomimeCurrentTerm");
        admin.MapGet("/session/events/{eventId:guid}/slides/{slideId:guid}/pantomime", GetPantomimeAsync)
            .WithName("GetPantomime");
        admin.MapPost("/session/events/{eventId:guid}/slides/{slideId:guid}/pantomime/admin/{action}", UpdatePantomimeAsync)
            .WithName("UpdatePantomime");
    }

    public sealed record TeamAssignmentRequest(Guid TeamId, Guid PerformerId);

    public sealed record AdminActionRequest(
        TeamAssignmentRequest[]? Assignments,
        int? RoundDurationSeconds,
        Guid? TeamId,
        int? ExpectedTermIndex);

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
        var terms = PantomimeGameService.GetDistinctTerms(parameters["terms"] as JsonArray);
        var termCount = terms.Length;
        var maxSkipsPerPerformer = parameters["maxSkipsPerPerformer"]?.GetValue<int>() ?? 0;
        var rawState = parameters["state"] as JsonObject ?? new JsonObject();
        var state = JsonSerializer.SerializeToElement(rawState);
        return Results.Ok(new PantomimeAdminData(
            parameters["category"]?.GetValue<string>() ?? string.Empty,
            termCount,
            parameters["roundDurationSeconds"]?.GetValue<int>() ?? 60,
            maxSkipsPerPerformer,
            JsonSerializer.SerializeToElement(PantomimeGameService.PublicState(state)),
            GetModeratorTeamTerms(rawState, terms, maxSkipsPerPerformer)));
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

        if (action is not ("start" or "pause" or "resume" or "end" or "correct" or "skip" or "free-skip"))
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
                    var maxSkipsPerPerformer = parameters["maxSkipsPerPerformer"]?.GetValue<int>() ?? 0;
                    if (maxSkipsPerPerformer is < 0 or > 100)
                    {
                        return Results.BadRequest(new ApiError("Das Skip-Limit muss zwischen 0 und 100 liegen."));
                    }
                    if (!PantomimeGameService.TryGetPointsPerCorrectTerm(parameters, out _))
                    {
                        return Results.BadRequest(new ApiError("Die Punkte pro erratenem Begriff müssen zwischen 0 und 1000 liegen."));
                    }

                    parameters["roundDurationSeconds"] = duration;
                    state = PantomimeGameService.CreateRoundState(
                        gameAssignments,
                        terms.Length,
                        duration,
                        now,
                        maxSkipsPerPerformer: maxSkipsPerPerformer);
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
                case "correct":
                case "skip":
                case "free-skip":
                    if (!PantomimeGameService.ReadBool(state, "isRunning"))
                    {
                        return Results.Conflict(new ApiError("Es läuft keine Pantomime-Runde."));
                    }

                    if (request.TeamId is not Guid teamId)
                    {
                        return Results.BadRequest(new ApiError("Wähle ein Team für die Pantomime-Aktion aus."));
                    }

                    var teamState = (state["teamStates"] as JsonArray)?
                        .OfType<JsonObject>()
                        .FirstOrDefault(item => PantomimeGameService.ReadGuid(item, "teamId") == teamId);
                    var termIndex = teamState is null ? null : PantomimeGameService.GetCurrentTermIndex(teamState);
                    if (teamState is null || termIndex is null || termIndex < 0 || termIndex >= terms.Length)
                    {
                        return Results.Conflict(new ApiError("Für dieses Team gibt es keinen aktiven Begriff."));
                    }
                    if (request.ExpectedTermIndex != termIndex)
                    {
                        return Results.Conflict(new ApiError("Der Begriff dieses Teams hat sich bereits geändert. Aktualisiere die Regie und prüfe den neuen Begriff."));
                    }

                    if (action is "skip" or "free-skip")
                    {
                        var maxSkips = parameters["maxSkipsPerPerformer"]?.GetValue<int>() ?? 0;
                        if (!PantomimeGameService.TrySkipCurrentTerm(
                                state,
                                teamState,
                                terms.Length,
                                maxSkips,
                                freeSkip: action == "free-skip"))
                        {
                            return Results.Conflict(new ApiError("Das Skip-Limit für diese darstellende Person ist erreicht."));
                        }

                        break;
                    }

                    if (!PantomimeGameService.TryGetPointsPerCorrectTerm(parameters, out var pointsPerCorrectTerm))
                    {
                        return Results.BadRequest(new ApiError("Die Punkte pro erratenem Begriff müssen zwischen 0 und 1000 liegen."));
                    }

                    var teamEntity = await dbContext.Teams
                        .FirstOrDefaultAsync(item => item.Id == teamId && item.ProjektId == projectId, cancellationToken);
                    if (teamEntity is null)
                    {
                        return Results.Conflict(new ApiError("Das Team existiert nicht mehr."));
                    }

                    teamEntity.Punkte = checked(teamEntity.Punkte + pointsPerCorrectTerm);
                    dbContext.TeamPointLogs.Add(new TeamPointLog
                    {
                        ProjektId = projectId,
                        TeamId = teamEntity.Id,
                        DeltaPoints = pointsPerCorrectTerm,
                        NewTotalPoints = teamEntity.Punkte,
                        Reason = "Pantomime-Begriff vom Moderator als erraten markiert",
                        CreatedAtUtc = now
                    });
                    teamState["correctCount"] = PantomimeGameService.ReadInt(teamState, "correctCount") + 1;
                    PantomimeGameService.AssignNextTerm(state, teamState);
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
            var maxSkipsPerPerformer = parameters["maxSkipsPerPerformer"]?.GetValue<int>() ?? 0;
            return Results.Ok(new PantomimePerformerData(
                parameters["category"]?.GetValue<string>() ?? string.Empty,
                PantomimeGameService.ReadString(teamState, "teamName"),
                currentTerm,
                PantomimeGameService.ReadBool(state, "isRunning"),
                PantomimeGameService.ReadBool(state, "isPaused"),
                PantomimeGameService.ReadBool(state, "isEnded"),
                PantomimeGameService.RemainingSeconds(state, now),
                PantomimeGameService.ReadInt(teamState, "correctCount"),
                PantomimeGameService.SkipsRemaining(teamState, maxSkipsPerPerformer)));
        }
        finally
        {
            gameLock.Release();
        }
    }

    private static async Task<IResult> SkipCurrentTermAsync(
        Guid projectId,
        Guid eventId,
        Guid slideId,
        HttpContext httpContext,
        ParticipantPermissionService permissionService,
        WeihnachtsfeierDbContext dbContext,
        IGameStateNotifier gameStateNotifier,
        CancellationToken cancellationToken)
    {
        var access = GetAccess(httpContext);
        if (!access.IsParticipant || access.ParticipantId is not Guid participantId)
        {
            return Results.Forbid();
        }
        if (!await permissionService.AllowsAsync(
                access,
                WeihnachtsfeierBit.Framework.ParticipantPermissionKeys.IsMuted,
                requireInteraction: false,
                cancellationToken))
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
                return Results.Conflict(new ApiError("Für dich gibt es keinen aktiven Begriff zum Überspringen."));
            }

            var maxSkipsPerPerformer = parameters["maxSkipsPerPerformer"]?.GetValue<int>() ?? 0;
            if (!PantomimeGameService.TrySkipCurrentTerm(state, teamState, terms.Length, maxSkipsPerPerformer))
            {
                return Results.Conflict(new ApiError("Dein Skip-Limit für diese Runde ist erreicht."));
            }

            await SaveRoundStateAsync(gameEvent, slides!, slide, parameters, state, dbContext, gameStateNotifier, projectId, cancellationToken);
            return Results.Ok();
        }
        finally
        {
            gameLock.Release();
        }
    }

    private static PantomimeModeratorTeamData[] GetModeratorTeamTerms(
        JsonObject state,
        string[] terms,
        int maxSkipsPerPerformer) =>
        (state["teamStates"] as JsonArray)?
            .OfType<JsonObject>()
            .Select(team =>
            {
                var termIndex = PantomimeGameService.GetCurrentTermIndex(team);
                var term = termIndex is int index && index >= 0 && index < terms.Length
                    ? terms[index]
                    : null;
                return new PantomimeModeratorTeamData(
                    PantomimeGameService.ReadGuid(team, "teamId") ?? Guid.Empty,
                    PantomimeGameService.ReadString(team, "teamName"),
                    PantomimeGameService.ReadGuid(team, "performerId") ?? Guid.Empty,
                    PantomimeGameService.ReadString(team, "performerName"),
                    termIndex,
                    term,
                    PantomimeGameService.ReadInt(team, "correctCount"),
                    PantomimeGameService.SkipsRemaining(team, maxSkipsPerPerformer));
            })
            .ToArray() ?? [];

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
