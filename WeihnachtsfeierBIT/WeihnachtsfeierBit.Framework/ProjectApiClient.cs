using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace WeihnachtsfeierBit.Framework;

public sealed class ProjectApiClient(HttpClient httpClient)
{
    public async Task<ProjectSummary[]> GetProjectsAsync(CancellationToken cancellationToken = default)
    {
        return await httpClient.GetFromJsonAsync<ProjectSummary[]>("/api/projects", cancellationToken) ?? [];
    }

    public async Task<ProjectSummary[]> GetAdminProjectsAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, "/api/admin/projects", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ProjectSummary[]>(cancellationToken) ?? [];
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
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new HttpRequestException("Das angeforderte Projekt wurde nicht gefunden oder existiert nicht mehr (404).", null, System.Net.HttpStatusCode.NotFound);
        }
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ProjectOverviewResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Die API hat keine Projektübersicht zurückgegeben.");
    }

    public async Task<ProjectResponse> CreateProjectAsync(CreateProjectRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync("/api/projects", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ProjectResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Die API hat kein Projekt zurückgegeben.");
    }

    public async Task<ProjectResponse> CreateProjectAsync(string accessToken, string name, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Post, "/api/admin/projects", accessToken, new CreateProjectRequest(name));
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ProjectResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Die API hat kein Projekt zurückgegeben.");
    }

    public async Task RenameProjectAsync(Guid projectId, RenameProjectRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PutAsJsonAsync($"/api/projects/{projectId}", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<ProjectResponse> RenameProjectAsync(string accessToken, Guid projectId, string name, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Put, $"/api/projects/{projectId}", accessToken, new RenameProjectRequest(name));
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ProjectResponse>(cancellationToken)
            ?? new ProjectResponse(projectId, name);
    }

    public async Task DeleteProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.DeleteAsync($"/api/projects/{projectId}", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task DeleteProjectAsync(string accessToken, Guid projectId, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Delete, $"/api/projects/{projectId}", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
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

    public async Task<AdminShow> CreateAdminShowAsync(string accessToken, string name, CancellationToken cancellationToken = default)
    {
        return await CreateAdminShowAsync(accessToken, new CreateGlobalShowRequest(name), cancellationToken);
    }

    public async Task<AdminShow> CreateAdminShowAsync(
        string accessToken,
        CreateGlobalShowRequest request,
        CancellationToken cancellationToken = default)
    {
        using var requestMessage = CreateRequest(HttpMethod.Post, "/api/admin/shows", accessToken, request);
        using var response = await httpClient.SendAsync(requestMessage, cancellationToken);
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
        SlideWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        using var requestMessage = CreateRequest(HttpMethod.Post, $"/api/admin/shows/{showId}/slides", accessToken, request);
        using var response = await httpClient.SendAsync(requestMessage, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<SlideSummary>(cancellationToken)
            ?? throw new InvalidOperationException("Die API hat keine Slide zurückgegeben.");
    }

    public async Task UpdateAdminShowSlideAsync(
        string accessToken,
        Guid showId,
        Guid slideId,
        SlideWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        using var requestMessage = CreateRequest(HttpMethod.Put, $"/api/admin/shows/{showId}/slides/{slideId}", accessToken, request);
        using var response = await httpClient.SendAsync(requestMessage, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task ReorderAdminShowSlidesAsync(
        string accessToken,
        Guid showId,
        Guid[] slideIds,
        CancellationToken cancellationToken = default)
    {
        using var requestMessage = CreateRequest(
            HttpMethod.Put,
            $"/api/admin/shows/{showId}/slide-positions",
            accessToken,
            new ReorderSlidesRequest(slideIds));
        using var response = await httpClient.SendAsync(requestMessage, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
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

    public async Task<AdminShow> UpdateAdminShowAsync(string accessToken, Guid showId, string name, CancellationToken cancellationToken = default)
    {
        using var requestMessage = CreateRequest(HttpMethod.Put, $"/api/admin/shows/{showId}", accessToken, new CreateGlobalShowRequest(name));
        using var response = await httpClient.SendAsync(requestMessage, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<AdminShow>(cancellationToken)
            ?? new AdminShow(showId, name, 0);
    }

    public async Task UpdateAdminShowNameAsync(
        string accessToken,
        Guid showId,
        CreateGlobalShowRequest request,
        CancellationToken cancellationToken = default)
    {
        using var requestMessage = CreateRequest(HttpMethod.Put, $"/api/admin/shows/{showId}", accessToken, request);
        using var response = await httpClient.SendAsync(requestMessage, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task DeleteAdminShowAsync(string accessToken, Guid showId, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Delete, $"/api/admin/shows/{showId}", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<AdminShow> DuplicateAdminShowAsync(string accessToken, Guid showId, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Post, $"/api/admin/shows/{showId}/duplicate", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<AdminShow>(cancellationToken)
            ?? throw new InvalidOperationException("Die API hat keine duplizierte Show zurückgegeben.");
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

    public async Task<ResolveCodeResponse> ResolveCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var cleanCode = code.Trim();
        return await httpClient.GetFromJsonAsync<ResolveCodeResponse>($"/api/game-instances/by-code/{cleanCode}", cancellationToken)
            ?? throw new InvalidOperationException("Die API hat keinen Session-Code aufgelöst.");
    }

    public async Task<ParticipantSessionResponse> LoginByCodeAsync(string code, int number, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync("/api/participant-sessions/by-code", new { code, number }, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ParticipantSessionResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Die API hat keine Sitzung zurückgegeben.");
    }

    public async Task<ParticipantSummary> GetMyProfileAsync(string accessToken, Guid projectId, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, $"/api/projects/{projectId}/participants/me", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ParticipantSummary>(cancellationToken)
            ?? throw new InvalidOperationException("Die API hat kein Profil zurückgegeben.");
    }

    public async Task<ParticipantSummary> UpdateMyNameAsync(string accessToken, Guid projectId, string name, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Put, $"/api/projects/{projectId}/participants/me", accessToken, new { name });
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ParticipantSummary>(cancellationToken)
            ?? new ParticipantSummary(Guid.Empty, 0, name, null);
    }

    public async Task<ParticipantSummary[]> GetParticipantsAsync(string accessToken, Guid projectId, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, $"/api/projects/{projectId}/participants", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ParticipantSummary[]>(cancellationToken) ?? [];
    }

    public async Task<ParticipantSummary> CreateParticipantAsync(string accessToken, Guid projectId, int number, string? name, Guid? teamId, CancellationToken cancellationToken = default)
    {
        return await CreateParticipantAsync(accessToken, projectId, new CreateParticipantRequest(number, name, teamId), cancellationToken);
    }

    public async Task<ParticipantSummary> CreateParticipantAsync(string accessToken, Guid projectId, CreateParticipantRequest request, CancellationToken cancellationToken = default)
    {
        using var requestMessage = CreateRequest(HttpMethod.Post, $"/api/projects/{projectId}/participants", accessToken, request);
        using var response = await httpClient.SendAsync(requestMessage, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ParticipantSummary>(cancellationToken)
            ?? throw new InvalidOperationException("Die API hat keinen Teilnehmer zurückgegeben.");
    }

    public async Task UpdateParticipantAsync(string accessToken, Guid projectId, Guid participantId, int? number, string? name, Guid? teamId, CancellationToken cancellationToken = default)
    {
        await UpdateParticipantAsync(accessToken, projectId, participantId, new UpdateParticipantRequest(name, teamId, number), cancellationToken);
    }

    public async Task UpdateParticipantAsync(string accessToken, Guid projectId, Guid participantId, UpdateParticipantRequest request, CancellationToken cancellationToken = default)
    {
        using var requestMessage = CreateRequest(HttpMethod.Put, $"/api/projects/{projectId}/participants/{participantId}", accessToken, request);
        using var response = await httpClient.SendAsync(requestMessage, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task DeleteParticipantAsync(string accessToken, Guid projectId, Guid participantId, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Delete, $"/api/projects/{projectId}/participants/{participantId}", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<GenerateParticipantsResponse> GenerateParticipantsAsync(string accessToken, Guid projectId, int count, int digits, Guid? teamId, CancellationToken cancellationToken = default)
    {
        return await GenerateParticipantsAsync(accessToken, projectId, new GenerateParticipantsRequest(count, digits, teamId), cancellationToken);
    }

    public async Task<GenerateParticipantsResponse> GenerateParticipantsAsync(string accessToken, Guid projectId, GenerateParticipantsRequest request, CancellationToken cancellationToken = default)
    {
        using var requestMessage = CreateRequest(HttpMethod.Post, $"/api/projects/{projectId}/participants/generate", accessToken, request);
        using var response = await httpClient.SendAsync(requestMessage, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<GenerateParticipantsResponse>(cancellationToken)
            ?? new GenerateParticipantsResponse(0, []);
    }

    public async Task<RandomizeTeamsResponse> RandomizeTeamsAsync(string accessToken, Guid projectId, bool onlyUnassigned, CancellationToken cancellationToken = default)
    {
        return await RandomizeTeamsAsync(accessToken, projectId, new RandomizeTeamsRequest(onlyUnassigned), cancellationToken);
    }

    public async Task<RandomizeTeamsResponse> RandomizeTeamsAsync(string accessToken, Guid projectId, RandomizeTeamsRequest request, CancellationToken cancellationToken = default)
    {
        using var requestMessage = CreateRequest(HttpMethod.Post, $"/api/projects/{projectId}/participants/randomize-teams", accessToken, request);
        using var response = await httpClient.SendAsync(requestMessage, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<RandomizeTeamsResponse>(cancellationToken)
            ?? new RandomizeTeamsResponse(0);
    }

    public async Task<TeamSummary[]> GetTeamsAsync(string accessToken, Guid projectId, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, $"/api/projects/{projectId}/teams", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<TeamSummary[]>(cancellationToken) ?? [];
    }

    public async Task<TeamSummary> CreateTeamAsync(string accessToken, Guid projectId, string displayName, string color, CancellationToken cancellationToken = default)
    {
        return await CreateTeamAsync(accessToken, projectId, new CreateTeamRequest(displayName, color), cancellationToken);
    }

    public async Task<TeamSummary> CreateTeamAsync(string accessToken, Guid projectId, CreateTeamRequest request, CancellationToken cancellationToken = default)
    {
        using var requestMessage = CreateRequest(HttpMethod.Post, $"/api/projects/{projectId}/teams", accessToken, request);
        using var response = await httpClient.SendAsync(requestMessage, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<TeamSummary>(cancellationToken)
            ?? throw new InvalidOperationException("Die API hat kein Team zurückgegeben.");
    }

    public async Task UpdateTeamAsync(string accessToken, Guid projectId, Guid teamId, string displayName, string color, int points, CancellationToken cancellationToken = default)
    {
        await UpdateTeamAsync(accessToken, projectId, teamId, new UpdateTeamRequest(displayName, color, points), cancellationToken);
    }

    public async Task UpdateTeamAsync(string accessToken, Guid projectId, Guid teamId, UpdateTeamRequest request, CancellationToken cancellationToken = default)
    {
        using var requestMessage = CreateRequest(HttpMethod.Put, $"/api/projects/{projectId}/teams/{teamId}", accessToken, request);
        using var response = await httpClient.SendAsync(requestMessage, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task UpdateTeamPointsAsync(string accessToken, Guid projectId, Guid teamId, int points, CancellationToken cancellationToken = default)
    {
        using var requestMessage = CreateRequest(HttpMethod.Put, $"/api/projects/{projectId}/teams/{teamId}/points", accessToken, new UpdateTeamPointsRequest(points));
        using var response = await httpClient.SendAsync(requestMessage, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task DeleteTeamAsync(string accessToken, Guid projectId, Guid teamId, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Delete, $"/api/projects/{projectId}/teams/{teamId}", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
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
            ?? throw new InvalidOperationException("Die API hat keine GameInstance zurückgegeben.");
    }

    public async Task EndGameInstanceAsync(string accessToken, Guid projectId, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Delete, $"/api/projects/{projectId}/game-instance", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task StartSessionShowAsync(string accessToken, Guid projectId, Guid showId, string mode, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Post, $"/api/projects/{projectId}/session/shows", accessToken, new { showId, mode });
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task CreateSpontaneousEventAsync(string accessToken, Guid projectId, string slideType, string mode, JsonElement parameters, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Post, $"/api/projects/{projectId}/session/events", accessToken, new { slideType, mode, parameters });
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task UpdateEventModeAsync(string accessToken, Guid projectId, Guid eventId, string mode, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Put, $"/api/projects/{projectId}/session/events/{eventId}/mode", accessToken, new { mode });
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task UpdateEventAsync(string accessToken, Guid projectId, Guid eventId, int currentSlideIndex, string mode, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Put, $"/api/projects/{projectId}/session/events/{eventId}", accessToken, new { currentSlideIndex, mode });
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task CloseEventAsync(string accessToken, Guid projectId, Guid eventId, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Put, $"/api/projects/{projectId}/session/events/{eventId}/close", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task SubmitEventResponseAsync(string accessToken, Guid projectId, Guid eventId, Guid slideId, JsonElement responseData, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Post, $"/api/projects/{projectId}/session/events/{eventId}/slides/{slideId}/responses", accessToken, new { responseData });
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task SetCanvasEventAsync(string accessToken, Guid projectId, Guid? eventId, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Put, $"/api/projects/{projectId}/session/canvas", accessToken, new { eventId });
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<ProjectGameState> GetSessionStateAsync(string accessToken, Guid projectId, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, $"/api/projects/{projectId}/session/state", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ProjectGameState>(cancellationToken)
            ?? throw new InvalidOperationException("Die API hat keinen Session-Zustand zurückgegeben.");
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
public sealed record ReorderSlidesRequest(Guid[] SlideIds);
public sealed record ResolveCodeResponse(Guid GameInstanceId, Guid ProjektId, string ProjectName, string Code);
public sealed record GameInstanceSummary(Guid Id, Guid ProjektId, string AccessCode, DateTime CreatedAtUtc);
public sealed record ParticipantState(Guid Id, int Number, string? Name);
public sealed record TeamState(Guid Id, string DisplayName, string Color, int Points, int Rank, int ParticipantCount, ParticipantState[] Members);

public sealed record EventSlideState(
    Guid SlideId,
    string Type,
    int Position,
    JsonElement Parameters);

public sealed record EventResponseState(
    Guid Id,
    Guid SlideId,
    Guid ParticipantId,
    int ParticipantNumber,
    string? ParticipantName,
    string? TeamName,
    string? TeamColor,
    JsonElement ResponseData,
    DateTime SubmittedAtUtc);

public sealed record EventState(
    Guid EventId,
    Guid? SourceShowId,
    string Title,
    string Mode,
    string Status,
    string NavigationMode,
    int CurrentSlideIndex,
    EventSlideState[] Slides,
    EventResponseState[] Responses,
    DateTime StartedAtUtc);

public sealed record ProjectGameState(
    Guid ProjectId,
    string ProjectName,
    TeamState[] Teams,
    EventState? ActiveEvent,
    EventState[] PassiveEvents,
    Guid? ActiveCanvasEventId,
    DateTime UpdatedAtUtc);
