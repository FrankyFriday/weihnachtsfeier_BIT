using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace WeihnachtsfeierBit.Framework;

public class ProjectApiClient(HttpClient httpClient)
{
    public async Task<ProjectSummary[]> GetAdminProjectsAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, "/api/admin/projects", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ProjectSummary[]>(cancellationToken) ?? [];
    }

    public async Task<AdminShow[]> GetAdminShowsAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, "/api/admin/shows", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<AdminShow[]>(cancellationToken) ?? [];
    }

    public async Task<AdminShow> GetAdminShowAsync(string accessToken, Guid showId, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, $"/api/admin/shows/{showId}", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<AdminShow>(cancellationToken)
            ?? throw new InvalidOperationException("Die API hat keine Show zurückgegeben.");
    }

    public async Task<SlideSummary[]> GetAdminShowSlidesAsync(
        string accessToken,
        Guid showId,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, $"/api/admin/shows/{showId}/slides", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<SlideSummary[]>(cancellationToken) ?? [];
    }

    public async Task<SlideSummary> CreateAdminShowSlideAsync(
        string accessToken,
        Guid showId,
        SlideWriteRequest slide,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Post, $"/api/admin/shows/{showId}/slides", accessToken, slide);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<SlideSummary>(cancellationToken)
            ?? throw new InvalidOperationException("Die API hat keinen Slide zurückgegeben.");
    }

    public async Task<SlideSummary> UpdateAdminShowSlideAsync(
        string accessToken,
        Guid showId,
        Guid slideId,
        SlideWriteRequest slide,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Put, $"/api/admin/shows/{showId}/slides/{slideId}", accessToken, slide);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<SlideSummary>(cancellationToken)
            ?? throw new InvalidOperationException("Die API hat keinen aktualisierten Slide zurückgegeben.");
    }

    public async Task DeleteAdminShowSlideAsync(
        string accessToken,
        Guid showId,
        Guid slideId,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Delete, $"/api/admin/shows/{showId}/slides/{slideId}", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<AdminShow> CreateAdminShowAsync(string accessToken, string name, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Post, "/api/admin/shows", accessToken, new CreateGlobalShowRequest(name));
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<AdminShow>(cancellationToken)
            ?? throw new InvalidOperationException("Die API hat keine Show zurückgegeben.");
    }

    public async Task<ProjectSummary> CreateProjectAsync(string accessToken, string name, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Post, "/api/admin/projects", accessToken, new CreateProjectRequest(name));
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ProjectSummary>(cancellationToken)
            ?? throw new InvalidOperationException("Die API hat kein Projekt zurückgegeben.");
    }

    public async Task<ProjectResponse> GetProjectAsync(string accessToken, Guid projectId, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, $"/api/projects/{projectId}", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ProjectResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Die API hat kein Projekt zurückgegeben.");
    }

    public async Task<ProjectOverviewResponse> GetProjectOverviewAsync(string accessToken, Guid projectId, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, $"/api/projects/{projectId}/overview", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ProjectOverviewResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Die API hat keine Projekt-Übersicht zurückgegeben.");
    }

    public async Task<ProjectResponse> RenameProjectAsync(string accessToken, Guid projectId, string name, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Put, $"/api/projects/{projectId}", accessToken, new RenameProjectRequest(name));
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await GetProjectAsync(accessToken, projectId, cancellationToken);
    }

    public async Task DeleteProjectAsync(string accessToken, Guid projectId, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Delete, $"/api/projects/{projectId}", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<ProjectShow[]> GetShowsAsync(string accessToken, Guid projectId, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, $"/api/projects/{projectId}/shows", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ProjectShow[]>(cancellationToken) ?? [];
    }

    public async Task LinkShowAsync(string accessToken, Guid projectId, Guid showId, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Post, $"/api/projects/{projectId}/shows", accessToken, new LinkShowRequest(showId));
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task UnlinkShowAsync(string accessToken, Guid projectId, Guid showId, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Delete, $"/api/projects/{projectId}/shows/{showId}", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<ParticipantSummary> GetMyProfileAsync(string accessToken, Guid projectId, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, $"/api/projects/{projectId}/participants/me", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ParticipantSummary>(cancellationToken)
            ?? throw new InvalidOperationException("Die API hat kein Profil zurückgegeben.");
    }

    public async Task<ParticipantSummary> UpdateMyNameAsync(string accessToken, Guid projectId, string? name, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Put, $"/api/projects/{projectId}/participants/me", accessToken, new { name });
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ParticipantSummary>(cancellationToken)
            ?? throw new InvalidOperationException("Die API hat kein aktualisiertes Profil zurückgegeben.");
    }

    public async Task<ParticipantSummary[]> GetParticipantsAsync(string accessToken, Guid projectId, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, $"/api/projects/{projectId}/participants", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ParticipantSummary[]>(cancellationToken) ?? [];
    }

    public async Task<ParticipantSummary> CreateParticipantAsync(
        string accessToken,
        Guid projectId,
        int number,
        string? name = null,
        Guid? teamId = null,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Post, $"/api/projects/{projectId}/participants", accessToken, new CreateParticipantRequest(number, name, teamId));
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ParticipantSummary>(cancellationToken)
            ?? throw new InvalidOperationException("Die API hat keinen Teilnehmer zurückgegeben.");
    }

    public async Task<GenerateParticipantsResponse> GenerateParticipantsAsync(
        string accessToken,
        Guid projectId,
        int count,
        int digits = 2,
        Guid? teamId = null,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Post, $"/api/projects/{projectId}/participants/generate", accessToken, new GenerateParticipantsRequest(count, digits, teamId));
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<GenerateParticipantsResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Die API hat keine generierten Teilnehmer zurückgegeben.");
    }

    public async Task<RandomizeTeamsResponse> RandomizeTeamsAsync(
        string accessToken,
        Guid projectId,
        bool onlyUnassigned = false,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Post, $"/api/projects/{projectId}/participants/randomize-teams", accessToken, new RandomizeTeamsRequest(onlyUnassigned));
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<RandomizeTeamsResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Die API hat kein Ergebnis für die Zufallsverteilung zurückgegeben.");
    }

    public async Task<ParticipantSummary> UpdateParticipantAsync(
        string accessToken,
        Guid projectId,
        Guid participantId,
        int? number = null,
        string? name = null,
        Guid? teamId = null,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Put, $"/api/projects/{projectId}/participants/{participantId}", accessToken, new UpdateParticipantRequest(name, teamId, number));
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ParticipantSummary>(cancellationToken)
            ?? throw new InvalidOperationException("Die API hat keine aktualisierten Teilnehmerdaten zurückgegeben.");
    }

    public async Task DeleteParticipantAsync(
        string accessToken,
        Guid projectId,
        Guid participantId,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Delete, $"/api/projects/{projectId}/participants/{participantId}", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<TeamSummary[]> GetTeamsAsync(string accessToken, Guid projectId, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, $"/api/projects/{projectId}/teams", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<TeamSummary[]>(cancellationToken) ?? [];
    }

    public async Task<TeamSummary> CreateTeamAsync(
        string accessToken,
        Guid projectId,
        string displayName,
        string color,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Post, $"/api/projects/{projectId}/teams", accessToken, new CreateTeamRequest(displayName, color));
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<TeamSummary>(cancellationToken)
            ?? throw new InvalidOperationException("Die API hat kein Team zurückgegeben.");
    }

    public async Task<TeamSummary> UpdateTeamAsync(
        string accessToken,
        Guid projectId,
        Guid teamId,
        string displayName,
        string color,
        int points,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Put, $"/api/projects/{projectId}/teams/{teamId}", accessToken, new UpdateTeamRequest(displayName, color, points));
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<TeamSummary>(cancellationToken)
            ?? throw new InvalidOperationException("Die API hat keine aktualisierten Teamdaten zurückgegeben.");
    }

    public async Task<TeamSummary> UpdateTeamPointsAsync(
        string accessToken,
        Guid projectId,
        Guid teamId,
        int points,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Put, $"/api/projects/{projectId}/teams/{teamId}/points", accessToken, new UpdateTeamPointsRequest(points));
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<TeamSummary>(cancellationToken)
            ?? throw new InvalidOperationException("Die API hat keine aktualisierten TeamPunkte zurückgegeben.");
    }

    public async Task DeleteTeamAsync(
        string accessToken,
        Guid projectId,
        Guid teamId,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Delete, $"/api/projects/{projectId}/teams/{teamId}", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<ResolveCodeResponse> ResolveCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/game-instances/by-code/{code.Trim()}");
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ResolveCodeResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Die API hat keine Code-Auflösung zurückgegeben.");
    }

    public async Task<ParticipantSessionResponse> LoginByCodeAsync(string code, int number, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/participant-sessions/by-code")
        {
            Content = JsonContent.Create(new { code = code.Trim(), number })
        };
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ParticipantSessionResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Die API hat keine Sitzung zurückgegeben.");
    }

    public async Task<GameInstanceSummary?> GetActiveGameInstanceAsync(string accessToken, Guid projectId, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, $"/api/projects/{projectId}/game-instance", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<GameInstanceSummary>(cancellationToken);
    }

    public async Task<GameInstanceSummary> StartGameInstanceAsync(string accessToken, Guid projectId, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Post, $"/api/projects/{projectId}/game-instance", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<GameInstanceSummary>(cancellationToken)
            ?? throw new InvalidOperationException("Die API hat keine neue GameInstance zurückgegeben.");
    }

    public async Task EndGameInstanceAsync(string accessToken, Guid projectId, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Delete, $"/api/projects/{projectId}/game-instance", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string path, string accessToken, object? content = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (content is not null)
        {
            request.Content = JsonContent.Create(content);
        }

        return request;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        ApiError? error;
        try
        {
            error = await response.Content.ReadFromJsonAsync<ApiError>(cancellationToken);
        }
        catch (JsonException)
        {
            error = null;
        }

        var message = error?.Message ?? $"Die Anfrage ist fehlgeschlagen ({(int)response.StatusCode}).";
        throw new HttpRequestException(message, null, response.StatusCode);
    }
}

public sealed record ProjectSummary(Guid Id, string Name);
public sealed record ProjectResponse(Guid Id, string Name);
public sealed record ProjectOverviewResponse(Guid Id, string Name, int ShowCount, int ParticipantCount, int TeamCount);
public sealed record CreateProjectRequest(string Name);
public sealed record RenameProjectRequest(string Name);
public sealed record ApiError(string Message);
public sealed record ProjectShow(Guid Id, string Name);
public sealed record AdminShow(Guid Id, string Name, int ProjectCount);
public sealed record CreateGlobalShowRequest(string Name);
public sealed record LinkShowRequest(Guid ShowId);
public record ParticipantSessionResponse(Guid SessionId, string AccessToken, DateTime ExpiresAtUtc, string Role, Guid? ProjectId, string DisplayName);
public sealed record ParticipantSummary(Guid Id, int Number, string? Name, Guid? TeamId);
public sealed record CreateParticipantRequest(int Number, string? Name, Guid? TeamId);
public sealed record UpdateParticipantRequest(string? Name, Guid? TeamId, int? Number);
public sealed record GenerateParticipantsRequest(int Count, int Digits, Guid? TeamId);
public sealed record GenerateParticipantsResponse(int CreatedCount, ParticipantSummary[] Participants);
public sealed record RandomizeTeamsRequest(bool OnlyUnassigned);
public sealed record RandomizeTeamsResponse(int AssignedCount);
public sealed record TeamSummary(Guid Id, string DisplayName, string Color, int Points);
public sealed record CreateTeamRequest(string DisplayName, string Color);
public sealed record UpdateTeamRequest(string DisplayName, string Color, int Points);
public sealed record UpdateTeamPointsRequest(int Points);
public sealed record SlideSummary(Guid Id, Guid ShowId, string Type, int Position, JsonElement Parameters);
public sealed record SlideWriteRequest(string Type, int Position, JsonElement Parameters);
public sealed record ParticipantState(Guid Id, int Number, string? Name);
public sealed record TeamState(Guid Id, string DisplayName, string Color, int Points, int Rank, int ParticipantCount, ParticipantState[] Members);
public sealed record ProjectGameState(Guid ProjectId, string ProjectName, TeamState[] Teams, DateTime UpdatedAtUtc);
public sealed record ResolveCodeResponse(Guid GameInstanceId, Guid ProjektId, string ProjectName, string Code);
public sealed record GameInstanceSummary(Guid Id, Guid ProjektId, string AccessCode, DateTime CreatedAtUtc);
