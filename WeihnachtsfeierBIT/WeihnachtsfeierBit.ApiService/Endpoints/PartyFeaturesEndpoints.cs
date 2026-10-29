using Microsoft.EntityFrameworkCore;
using WeihnachtsfeierBit.ApiService.Auth;
using WeihnachtsfeierBit.ApiService.Contracts;
using WeihnachtsfeierBit.ApiService.Services;
using WeihnachtsfeierBit.Data;

namespace WeihnachtsfeierBit.ApiService.Endpoints;

public static class PartyFeaturesEndpoints
{
    public static void Map(RouteGroupBuilder reader, RouteGroupBuilder admin)
    {
        // Team Name Suggestions
        reader.MapGet("/teams/my/suggestions", GetMyTeamNameSuggestionsAsync).WithName("GetMyTeamNameSuggestions");
        reader.MapPost("/teams/my/suggestions", SubmitTeamNameSuggestionAsync).WithName("SubmitTeamNameSuggestion");
        reader.MapPost("/teams/my/suggestions/{suggestionId:guid}/vote", ToggleTeamNameVoteAsync).WithName("ToggleTeamNameVote");

        // Song Wishes
        reader.MapGet("/song-wishes", GetSongWishesAsync).WithName("GetSongWishes");
        reader.MapPost("/song-wishes", SubmitSongWishAsync).WithName("SubmitSongWish");
        reader.MapPost("/song-wishes/{wishId:guid}/vote", ToggleSongWishVoteAsync).WithName("ToggleSongWishVote");
        admin.MapDelete("/song-wishes/{wishId:guid}", DeleteSongWishAsync).WithName("DeleteSongWish");
    }

    // TEAM NAME SUGGESTIONS
    private static async Task<IResult> GetMyTeamNameSuggestionsAsync(
        Guid projectId,
        HttpContext httpContext,
        ProjectAccessService accessService,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var token = ProjectAccessService.GetBearerToken(httpContext.Request);
        var access = await accessService.AuthenticateAsync(projectId, token, cancellationToken);
        if (access is null || access.ParticipantId is null) return Results.Unauthorized();

        var participant = await dbContext.Teilnehmer.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == access.ParticipantId.Value, cancellationToken);

        if (participant?.TeamId is null)
        {
            return Results.BadRequest(new ApiError("Du bist keinem Team zugewiesen."));
        }

        var teamMemberCount = await dbContext.Teilnehmer.CountAsync(t => t.TeamId == participant.TeamId.Value, cancellationToken);
        int requiredVotes = teamMemberCount > 0 ? (int)Math.Ceiling(teamMemberCount * 0.5) : 1;

        var suggestions = await dbContext.TeamNameSuggestions.AsNoTracking()
            .Where(s => s.TeamId == participant.TeamId.Value)
            .Include(s => s.Participant)
            .Include(s => s.Votes)
            .OrderByDescending(s => s.Votes.Count)
            .ThenBy(s => s.CreatedAtUtc)
            .Select(s => new TeamNameSuggestionDto(
                s.Id,
                s.SuggestedName,
                s.Participant.Name ?? $"Teilnehmer #{s.Participant.Nummer}",
                s.Votes.Count,
                requiredVotes,
                s.Votes.Any(v => v.ParticipantId == access.ParticipantId.Value),
                s.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return Results.Ok(suggestions);
    }

    private static async Task<IResult> SubmitTeamNameSuggestionAsync(
        Guid projectId,
        SubmitTeamNameSuggestionRequest request,
        HttpContext httpContext,
        ProjectAccessService accessService,
        WeihnachtsfeierDbContext dbContext,
        IGameStateNotifier gameStateNotifier,
        CancellationToken cancellationToken)
    {
        var token = ProjectAccessService.GetBearerToken(httpContext.Request);
        var access = await accessService.AuthenticateAsync(projectId, token, cancellationToken);
        if (access is null || access.ParticipantId is null) return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.SuggestedName))
        {
            return Results.BadRequest(new ApiError("Bitte gib einen Namensvorschlag ein."));
        }

        var participant = await dbContext.Teilnehmer
            .FirstOrDefaultAsync(t => t.Id == access.ParticipantId.Value, cancellationToken);

        if (participant?.TeamId is null)
        {
            return Results.BadRequest(new ApiError("Du bist keinem Team zugewiesen."));
        }

        var suggestion = new TeamNameSuggestion
        {
            TeamId = participant.TeamId.Value,
            ParticipantId = participant.Id,
            SuggestedName = request.SuggestedName.Trim(),
            VoteCount = 1
        };

        dbContext.TeamNameSuggestions.Add(suggestion);
        dbContext.TeamNameVotes.Add(new TeamNameVote { Suggestion = suggestion, ParticipantId = participant.Id });

        await dbContext.SaveChangesAsync(cancellationToken);

        await CheckAndApplySuggestionThresholdAsync(suggestion, dbContext, gameStateNotifier, projectId, cancellationToken);

        return Results.Ok();
    }

    private static async Task<IResult> ToggleTeamNameVoteAsync(
        Guid projectId,
        Guid suggestionId,
        HttpContext httpContext,
        ProjectAccessService accessService,
        WeihnachtsfeierDbContext dbContext,
        IGameStateNotifier gameStateNotifier,
        CancellationToken cancellationToken)
    {
        var token = ProjectAccessService.GetBearerToken(httpContext.Request);
        var access = await accessService.AuthenticateAsync(projectId, token, cancellationToken);
        if (access is null || access.ParticipantId is null) return Results.Unauthorized();

        var suggestion = await dbContext.TeamNameSuggestions
            .Include(s => s.Votes)
            .FirstOrDefaultAsync(s => s.Id == suggestionId, cancellationToken);

        if (suggestion is null) return Results.NotFound();

        var existingVote = suggestion.Votes.FirstOrDefault(v => v.ParticipantId == access.ParticipantId.Value);
        if (existingVote is not null)
        {
            dbContext.TeamNameVotes.Remove(existingVote);
            suggestion.VoteCount = Math.Max(0, suggestion.Votes.Count - 1);
        }
        else
        {
            dbContext.TeamNameVotes.Add(new TeamNameVote { SuggestionId = suggestionId, ParticipantId = access.ParticipantId.Value });
            suggestion.VoteCount = suggestion.Votes.Count + 1;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        await CheckAndApplySuggestionThresholdAsync(suggestion, dbContext, gameStateNotifier, projectId, cancellationToken);

        return Results.Ok();
    }

    private static async Task CheckAndApplySuggestionThresholdAsync(
        TeamNameSuggestion suggestion,
        WeihnachtsfeierDbContext dbContext,
        IGameStateNotifier gameStateNotifier,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var teamId = suggestion.TeamId;
        int memberCount = await dbContext.Teilnehmer.CountAsync(t => t.TeamId == teamId, cancellationToken);
        int threshold = memberCount > 0 ? (int)Math.Ceiling(memberCount * 0.5) : 1;

        if (suggestion.Votes.Count >= threshold)
        {
            var team = await dbContext.Teams.FirstOrDefaultAsync(t => t.Id == teamId, cancellationToken);
            if (team is not null)
            {
                team.DisplayName = suggestion.SuggestedName;
            }

            var allSuggestions = await dbContext.TeamNameSuggestions
                .Where(s => s.TeamId == teamId)
                .ToListAsync(cancellationToken);

            dbContext.TeamNameSuggestions.RemoveRange(allSuggestions);
            await dbContext.SaveChangesAsync(cancellationToken);
            await gameStateNotifier.NotifyProjectChangedAsync(projectId, cancellationToken);
        }
    }

    // SONG WISHES
    private static async Task<IResult> GetSongWishesAsync(
        Guid projectId,
        HttpContext httpContext,
        ProjectAccessService accessService,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var token = ProjectAccessService.GetBearerToken(httpContext.Request);
        var access = await accessService.AuthenticateAsync(projectId, token, cancellationToken);
        Guid? currentParticipantId = access?.ParticipantId;

        var wishes = await dbContext.SongWishes.AsNoTracking()
            .Where(sw => sw.ProjektId == projectId)
            .Include(sw => sw.Participant)
            .Include(sw => sw.Votes)
            .OrderByDescending(sw => sw.Votes.Count)
            .ThenBy(sw => sw.CreatedAtUtc)
            .Select(sw => new SongWishDto(
                sw.Id,
                sw.SongTitle,
                sw.Artist,
                sw.Participant.Name ?? $"Teilnehmer #{sw.Participant.Nummer}",
                sw.Votes.Count,
                currentParticipantId != null && sw.Votes.Any(v => v.ParticipantId == currentParticipantId.Value),
                sw.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return Results.Ok(wishes);
    }

    private static async Task<IResult> SubmitSongWishAsync(
        Guid projectId,
        SubmitSongWishRequest request,
        HttpContext httpContext,
        ProjectAccessService accessService,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var token = ProjectAccessService.GetBearerToken(httpContext.Request);
        var access = await accessService.AuthenticateAsync(projectId, token, cancellationToken);
        if (access is null || access.ParticipantId is null) return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.SongTitle))
        {
            return Results.BadRequest(new ApiError("Bitte gib einen Songtitel ein."));
        }

        var wish = new SongWish
        {
            ProjektId = projectId,
            ParticipantId = access.ParticipantId.Value,
            SongTitle = request.SongTitle.Trim(),
            Artist = request.Artist?.Trim() ?? string.Empty,
            VoteCount = 1
        };

        dbContext.SongWishes.Add(wish);
        dbContext.SongWishVotes.Add(new SongWishVote { SongWish = wish, ParticipantId = access.ParticipantId.Value });

        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Ok();
    }

    private static async Task<IResult> ToggleSongWishVoteAsync(
        Guid projectId,
        Guid wishId,
        HttpContext httpContext,
        ProjectAccessService accessService,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var token = ProjectAccessService.GetBearerToken(httpContext.Request);
        var access = await accessService.AuthenticateAsync(projectId, token, cancellationToken);
        if (access is null || access.ParticipantId is null) return Results.Unauthorized();

        var wish = await dbContext.SongWishes
            .Include(sw => sw.Votes)
            .FirstOrDefaultAsync(sw => sw.Id == wishId && sw.ProjektId == projectId, cancellationToken);

        if (wish is null) return Results.NotFound();

        var existingVote = wish.Votes.FirstOrDefault(v => v.ParticipantId == access.ParticipantId.Value);
        if (existingVote is not null)
        {
            dbContext.SongWishVotes.Remove(existingVote);
            wish.VoteCount = Math.Max(0, wish.Votes.Count - 1);
        }
        else
        {
            dbContext.SongWishVotes.Add(new SongWishVote { SongWishId = wishId, ParticipantId = access.ParticipantId.Value });
            wish.VoteCount = wish.Votes.Count + 1;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Ok();
    }

    private static async Task<IResult> DeleteSongWishAsync(
        Guid projectId,
        Guid wishId,
        HttpContext httpContext,
        AdminSessionAccessService sessionAccess,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (await sessionAccess.GetAdminIdAsync(httpContext, cancellationToken) is null)
        {
            return Results.Unauthorized();
        }

        var wish = await dbContext.SongWishes
            .FirstOrDefaultAsync(sw => sw.Id == wishId && sw.ProjektId == projectId, cancellationToken);

        if (wish is null) return Results.NotFound();

        dbContext.SongWishes.Remove(wish);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Ok();
    }
}

public sealed record SubmitTeamNameSuggestionRequest(string SuggestedName);
public sealed record TeamNameSuggestionDto(Guid Id, string SuggestedName, string SubmittedBy, int Votes, int RequiredVotes, bool HasVoted, DateTime CreatedAtUtc);

public sealed record SubmitSongWishRequest(string SongTitle, string? Artist);
public sealed record SongWishDto(Guid Id, string SongTitle, string Artist, string SubmittedBy, int Votes, bool HasVoted, DateTime CreatedAtUtc);
