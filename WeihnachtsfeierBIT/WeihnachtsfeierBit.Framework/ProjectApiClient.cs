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

    public async Task<AdminAccessSessionSummary[]> GetAdminSessionsAsync(string accessToken, Guid projectId, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, $"/api/projects/{projectId}/admin-sessions", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<AdminAccessSessionSummary[]>(cancellationToken) ?? [];
    }

    public async Task RevokeAdminSessionAsync(string accessToken, Guid projectId, Guid sessionId, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Delete, $"/api/projects/{projectId}/admin-sessions/{sessionId}", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<SessionSummary[]> GetParticipantSessionsAsync(
        string accessToken,
        Guid projectId,
        Guid participantId,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, $"/api/projects/{projectId}/participants/{participantId}/sessions", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<SessionSummary[]>(cancellationToken) ?? [];
    }

    public async Task RevokeParticipantSessionAsync(
        string accessToken,
        Guid projectId,
        Guid participantId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(
            HttpMethod.Delete,
            $"/api/projects/{projectId}/participants/{participantId}/sessions/{sessionId}",
            accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
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

    public async Task AwardTeamPointsAsync(string accessToken, Guid projectId, Guid teamId, int deltaPoints, string? reason, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Post, $"/api/projects/{projectId}/teams/{teamId}/award-points", accessToken, new { deltaPoints, reason });
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task SetSlideStateAsync(string accessToken, Guid projectId, Guid eventId, Guid slideId, object state, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Put, $"/api/projects/{projectId}/session/events/{eventId}/slides/{slideId}/state", accessToken, new { state });
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<PantomimeAdminData> GetPantomimeAsync(string accessToken, Guid projectId, Guid eventId, Guid slideId, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, $"/api/projects/{projectId}/session/events/{eventId}/slides/{slideId}/pantomime", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<PantomimeAdminData>(cancellationToken)
            ?? throw new InvalidOperationException("Die Pantomime-Daten konnten nicht geladen werden.");
    }

    public async Task UpdatePantomimeAsync(
        string accessToken,
        Guid projectId,
        Guid eventId,
        Guid slideId,
        string action,
        PantomimeAssignment[]? assignments = null,
        int? roundDurationSeconds = null,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(
            HttpMethod.Post,
            $"/api/projects/{projectId}/session/events/{eventId}/slides/{slideId}/pantomime/admin/{Uri.EscapeDataString(action)}",
            accessToken,
            new { assignments, roundDurationSeconds });
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<PantomimePerformerData> GetPantomimeCurrentTermAsync(
        string accessToken,
        Guid projectId,
        Guid eventId,
        Guid slideId,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(
            HttpMethod.Get,
            $"/api/projects/{projectId}/session/events/{eventId}/slides/{slideId}/pantomime/current-term",
            accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<PantomimePerformerData>(cancellationToken)
            ?? throw new InvalidOperationException("Der aktuelle Pantomime-Begriff konnte nicht geladen werden.");
    }

    public async Task MarkPantomimeTermCorrectAsync(
        string accessToken,
        Guid projectId,
        Guid eventId,
        Guid slideId,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(
            HttpMethod.Post,
            $"/api/projects/{projectId}/session/events/{eventId}/slides/{slideId}/pantomime/correct",
            accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<TeamPointLogDto[]> GetPointLogsAsync(string accessToken, Guid projectId, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, $"/api/projects/{projectId}/point-logs", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<TeamPointLogDto[]>(cancellationToken) ?? [];
    }

    public async Task UpdatePointLogAsync(string accessToken, Guid projectId, Guid logId, int deltaPoints, string? reason, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Put, $"/api/projects/{projectId}/point-logs/{logId}", accessToken, new { deltaPoints, reason });
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task DeletePointLogAsync(string accessToken, Guid projectId, Guid logId, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Delete, $"/api/projects/{projectId}/point-logs/{logId}", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
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

    public async Task<GameInstanceSummary> StartGameInstanceAsync(string accessToken, Guid projectId, string? preferredAccessCode = null, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Post, $"/api/projects/{projectId}/game-instance", accessToken, new
        {
            PreferredAccessCode = string.IsNullOrWhiteSpace(preferredAccessCode) ? null : preferredAccessCode.Trim()
        });
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

    public async Task StartSessionShowAsync(
        string accessToken,
        Guid projectId,
        Guid showId,
        string mode,
        string? title = null,
        string? subtitle = null,
        string navigationMode = "host_controlled",
        bool isHidden = false,
        DateTime? scheduledStartUtc = null,
        bool autoStartOnSchedule = false,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Post, $"/api/projects/{projectId}/session/shows", accessToken, new
        {
            showId,
            mode,
            title,
            subtitle,
            navigationMode,
            isHidden,
            scheduledStartUtc,
            autoStartOnSchedule
        });
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task CreateSpontaneousEventAsync(
        string accessToken,
        Guid projectId,
        string slideType,
        string mode,
        JsonElement parameters,
        string? title = null,
        string? subtitle = null,
        string navigationMode = "host_controlled",
        bool isHidden = false,
        DateTime? scheduledStartUtc = null,
        bool autoStartOnSchedule = false,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Post, $"/api/projects/{projectId}/session/events", accessToken, new
        {
            slideType,
            mode,
            title,
            subtitle,
            navigationMode,
            isHidden,
            scheduledStartUtc,
            autoStartOnSchedule,
            parameters
        });
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task ToggleSessionPauseAsync(string accessToken, Guid projectId, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Post, $"/api/projects/{projectId}/session/toggle-pause", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task UpdateEventModeAsync(string accessToken, Guid projectId, Guid eventId, string mode, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Put, $"/api/projects/{projectId}/session/events/{eventId}/mode", accessToken, new { mode });
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task UpdateEventAsync(
        string accessToken,
        Guid projectId,
        Guid eventId,
        string? title = null,
        string? subtitle = null,
        string? navigationMode = null,
        DateTime? scheduledStartUtc = null,
        bool? autoStartOnSchedule = null,
        int? currentSlideIndex = null,
        string? mode = null,
        bool? isHidden = null,
        string? status = null,
        DateTime? startedAtUtc = null,
        int? accumulatedElapsedSeconds = null,
        bool? isPaused = null,
        DateTime? lastResumedAtUtc = null,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Put, $"/api/projects/{projectId}/session/events/{eventId}", accessToken, new
        {
            title,
            subtitle,
            navigationMode,
            scheduledStartUtc,
            autoStartOnSchedule,
            currentSlideIndex,
            mode,
            isHidden,
            status,
            startedAtUtc,
            accumulatedElapsedSeconds,
            isPaused,
            lastResumedAtUtc
        });
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

    public async Task<CanvasGameState> GetCanvasStateAsync(string accessCode, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync($"/api/game-instances/by-code/{Uri.EscapeDataString(accessCode)}/canvas", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<CanvasGameState>(cancellationToken)
            ?? throw new InvalidOperationException("Die API hat keinen Beamer-Zustand zurückgegeben.");
    }

    public async Task<ProjectGameState> GetSessionStateAsync(string accessToken, Guid projectId, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, $"/api/projects/{projectId}/session/state", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ProjectGameState>(cancellationToken)
            ?? throw new InvalidOperationException("Die API hat keinen Session-Zustand zurückgegeben.");
    }

    public async Task<ChatGroupSummary[]> GetChatGroupsAsync(string accessToken, Guid projectId, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, $"/api/projects/{projectId}/chat/groups", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ChatGroupSummary[]>(cancellationToken) ?? [];
    }

    public async Task<ChatContactDto[]> GetChatContactsAsync(string accessToken, Guid projectId, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, $"/api/projects/{projectId}/chat/participants", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ChatContactDto[]>(cancellationToken) ?? [];
    }

    public async Task<ChatMessageResponse[]> GetChatMessagesAsync(string accessToken, Guid projectId, Guid groupId, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, $"/api/projects/{projectId}/chat/groups/{groupId}/messages", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ChatMessageResponse[]>(cancellationToken) ?? [];
    }

    public async Task SendChatMessageAsync(string accessToken, Guid projectId, Guid groupId, string messageText, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Post, $"/api/projects/{projectId}/chat/groups/{groupId}/messages", accessToken, new { messageText });
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task MarkGroupAsReadAsync(string accessToken, Guid projectId, Guid groupId, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Post, $"/api/projects/{projectId}/chat/groups/{groupId}/read", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<ChatGroupSummary> GetOrCreateDirectChatAsync(string accessToken, Guid projectId, Guid targetParticipantId, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Post, $"/api/projects/{projectId}/chat/direct/{targetParticipantId}", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ChatGroupSummary>(cancellationToken)
            ?? throw new InvalidOperationException("Direktchat konnte nicht erstellt werden.");
    }

    public async Task<ChatGroupSummary> CreateCustomGroupAsync(string accessToken, Guid projectId, string name, Guid[] participantIds, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Post, $"/api/projects/{projectId}/chat/groups", accessToken, new { name, participantIds });
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ChatGroupSummary>(cancellationToken)
            ?? throw new InvalidOperationException("Gruppe konnte nicht erstellt werden.");
    }

    // INFO PAGES
    public async Task<ProjectInfoPageDto[]> GetInfoPagesAsync(string accessToken, Guid projectId, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, $"/api/projects/{projectId}/info-pages", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ProjectInfoPageDto[]>(cancellationToken) ?? [];
    }

    public async Task<ProjectInfoPageDto> CreateInfoPageAsync(string accessToken, Guid projectId, string title, string icon, int position, bool isVisible, string contentMarkdown, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Post, $"/api/projects/{projectId}/info-pages", accessToken, new { title, icon, position, isVisible, contentMarkdown });
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ProjectInfoPageDto>(cancellationToken)
            ?? throw new InvalidOperationException("Info-Seite konnte nicht erstellt werden.");
    }

    public async Task UpdateInfoPageAsync(string accessToken, Guid projectId, Guid id, string title, string icon, int position, bool isVisible, string contentMarkdown, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Put, $"/api/projects/{projectId}/info-pages/{id}", accessToken, new { title, icon, position, isVisible, contentMarkdown });
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task DeleteInfoPageAsync(string accessToken, Guid projectId, Guid id, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Delete, $"/api/projects/{projectId}/info-pages/{id}", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    // TEAM NAME SUGGESTIONS
    public async Task<TeamNameSuggestionDto[]> GetMyTeamNameSuggestionsAsync(string accessToken, Guid projectId, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, $"/api/projects/{projectId}/teams/my/suggestions", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) return [];
        return await response.Content.ReadFromJsonAsync<TeamNameSuggestionDto[]>(cancellationToken) ?? [];
    }

    public async Task SubmitTeamNameSuggestionAsync(string accessToken, Guid projectId, string suggestedName, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Post, $"/api/projects/{projectId}/teams/my/suggestions", accessToken, new { suggestedName });
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task ToggleTeamNameVoteAsync(string accessToken, Guid projectId, Guid suggestionId, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Post, $"/api/projects/{projectId}/teams/my/suggestions/{suggestionId}/vote", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    // SONG WISHES
    public async Task<SongWishDto[]> GetSongWishesAsync(string accessToken, Guid projectId, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, $"/api/projects/{projectId}/song-wishes", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<SongWishDto[]>(cancellationToken) ?? [];
    }

    public async Task SubmitSongWishAsync(string accessToken, Guid projectId, string songTitle, string? artist, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Post, $"/api/projects/{projectId}/song-wishes", accessToken, new { songTitle, artist });
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task ToggleSongWishVoteAsync(string accessToken, Guid projectId, Guid wishId, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Post, $"/api/projects/{projectId}/song-wishes/{wishId}/vote", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task DeleteSongWishAsync(string accessToken, Guid projectId, Guid wishId, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Delete, $"/api/projects/{projectId}/song-wishes/{wishId}", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<SpotifyAuthStartResponse> BeginHitsterSpotifyAuthAsync(string accessToken, Guid projectId, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Post, $"/api/projects/{projectId}/session/spotify/auth", accessToken, new { });
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<SpotifyAuthStartResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Die API hat keinen Spotify-Anmeldevorgang gestartet.");
    }

    public async Task<SpotifyTokenResult> ExchangeHitsterSpotifyTokenAsync(
        string accessToken,
        Guid projectId,
        string grantType,
        string? code = null,
        string? state = null,
        string? refreshToken = null,
        string? codeVerifier = null,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(
            HttpMethod.Post,
            $"/api/projects/{projectId}/session/spotify/token",
            accessToken,
            new { grantType, code, state, refreshToken, codeVerifier });
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<SpotifyTokenResult>(cancellationToken)
            ?? throw new InvalidOperationException("Spotify hat kein Zugriffstoken zurückgegeben.");
    }

    public async Task HitsterAdminActionAsync(
        string accessToken,
        Guid projectId,
        Guid eventId,
        Guid slideId,
        string action,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(
            HttpMethod.Post,
            $"/api/projects/{projectId}/session/events/{eventId}/hitster/{slideId}/{action}",
            accessToken,
            new { });
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<string> GetHitsterTrackIdAsync(
        string accessToken,
        Guid projectId,
        Guid eventId,
        Guid slideId,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(
            HttpMethod.Get,
            $"/api/projects/{projectId}/session/events/{eventId}/hitster/{slideId}/track",
            accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<HitsterTrackResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Die API hat keinen Spotify-Track zurückgegeben.");
        return result.TrackId;
    }

    public async Task<HitsterVoteResponse> SubmitHitsterVoteAsync(
        string accessToken,
        Guid projectId,
        Guid eventId,
        Guid slideId,
        int position,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(
            HttpMethod.Post,
            $"/api/projects/{projectId}/session/events/{eventId}/hitster/{slideId}/vote",
            accessToken,
            new { position });
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<HitsterVoteResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Die Hitster-Abstimmung wurde nicht bestätigt.");
    }

    // ADMIN SHARING
    public async Task<AdminDto[]> SearchAdminsAsync(string accessToken, string query, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, $"/api/admin/admins/search?q={Uri.EscapeDataString(query)}", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<AdminDto[]>(cancellationToken) ?? [];
    }

    public async Task<AdminShareListResponse> GetProjectAdminsAsync(string accessToken, Guid projectId, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, $"/api/admin/projects/{projectId}/admins", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<AdminShareListResponse>(cancellationToken) ?? new AdminShareListResponse(false, []);
    }

    public async Task InviteProjectAdminAsync(string accessToken, Guid projectId, string username, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Post, $"/api/admin/projects/{projectId}/admins", accessToken, new { username });
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task RemoveProjectAdminAsync(string accessToken, Guid projectId, Guid adminId, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Delete, $"/api/admin/projects/{projectId}/admins/{adminId}", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task UpdateProjectAdminPermissionsAsync(
        string accessToken,
        Guid projectId,
        Guid adminId,
        bool canControlSession,
        bool canManageParticipants,
        bool canEditProjectSettings,
        bool canManageShows,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Put, $"/api/admin/projects/{projectId}/admins/{adminId}/permissions", accessToken, new
        {
            CanControlSession = canControlSession,
            CanManageParticipants = canManageParticipants,
            CanEditProjectSettings = canEditProjectSettings,
            CanManageShows = canManageShows
        });
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<AdminShareListResponse> GetShowAdminsAsync(string accessToken, Guid showId, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, $"/api/admin/shows/{showId}/admins", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<AdminShareListResponse>(cancellationToken) ?? new AdminShareListResponse(false, []);
    }

    public async Task InviteShowAdminAsync(string accessToken, Guid showId, string username, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Post, $"/api/admin/shows/{showId}/admins", accessToken, new { username });
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task RemoveShowAdminAsync(string accessToken, Guid showId, Guid adminId, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Delete, $"/api/admin/shows/{showId}/admins/{adminId}", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<ParticipantSummary> UpdateParticipantPermissionsAsync(
        string accessToken, Guid projectId, Guid participantId, Dictionary<string, bool> permissions, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Put, $"/api/projects/{projectId}/participants/{participantId}/permissions", accessToken, new { permissions });
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ParticipantSummary>(cancellationToken)
            ?? new ParticipantSummary(participantId, 0, null, null);
    }

    public async Task AdminSubmitResponseAsync(
        string accessToken, Guid projectId, Guid eventId, Guid slideId, Guid participantId, JsonElement responseData, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Post, $"/api/projects/{projectId}/session/events/{eventId}/slides/{slideId}/responses/override", accessToken, new { participantId, responseData });
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task AdminUpdateResponseAsync(
        string accessToken, Guid projectId, Guid eventId, Guid responseId, JsonElement responseData, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Put, $"/api/projects/{projectId}/session/events/{eventId}/responses/{responseId}", accessToken, new { responseData });
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task AdminDeleteResponseAsync(
        string accessToken, Guid projectId, Guid eventId, Guid responseId, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Delete, $"/api/projects/{projectId}/session/events/{eventId}/responses/{responseId}", accessToken);
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
public sealed record ProjectOverviewResponse(
    Guid Id,
    string Name,
    int ShowCount,
    int ParticipantCount,
    int TeamCount,
    ProjectPermissionsDto? Permissions = null);
public sealed record CreateProjectRequest(string Name);
public sealed record RenameProjectRequest(string Name);
public sealed record ApiError(string Message);
public sealed record ProjectShow(Guid Id, string Name);
public sealed record AdminShow(Guid Id, string Name, int ProjectCount);
public sealed record CreateGlobalShowRequest(string Name);
public sealed record LinkShowRequest(Guid ShowId);
public record ParticipantSessionResponse(
    Guid SessionId,
    string AccessToken,
    DateTime ExpiresAtUtc,
    string Role,
    Guid? ProjectId,
    string DisplayName,
    Guid? ParticipantId = null,
    Guid? TeamId = null);
public sealed record ParticipantPermissions(Dictionary<string, bool> Values)
{
    public bool HasPermission(string key)
    {
        if (Values.TryGetValue(ParticipantPermissionKeys.IsMuted, out var muted) && muted) return false;
        return !Values.TryGetValue(key, out var allowed) || allowed;
    }
}

public sealed record ParticipantSummary(Guid Id, int Number, string? Name, Guid? TeamId, ParticipantPermissions? Permissions = null);
public sealed record AdminAccessSessionSummary(
    Guid Id,
    Guid ProjektAdminId,
    string Username,
    DateTime ErstelltAmUtc,
    DateTime LetzteAktivitaetAmUtc,
    DateTime LaeuftAbAmUtc,
    string? UserAgent,
    bool IsCurrent);
public sealed record SessionSummary(
    Guid Id,
    Guid UserId,
    DateTime CreatedAtUtc,
    DateTime LastActivityAtUtc,
    DateTime ExpiresAtUtc,
    string? UserAgent);
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
public sealed record ParticipantState(Guid Id, int Number, string? Name, ParticipantPermissions? Permissions = null);
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

public sealed record EventNavigationContext(
    int OriginSlideIndex,
    int[] SequenceSlideIndices,
    int CurrentSequenceIndex,
    string? ContextTitle,
    Guid? ActiveTeamId,
    string? ActiveTeamName,
    string? ActiveCellKey);

public sealed record EventState(
    Guid EventId,
    Guid? SourceShowId,
    string Title,
    string? Subtitle,
    string Mode,
    string Status,
    string NavigationMode,
    int CurrentSlideIndex,
    bool IsHidden,
    DateTime? ScheduledStartUtc,
    bool AutoStartOnSchedule,
    EventSlideState[] Slides,
    EventResponseState[] Responses,
    DateTime StartedAtUtc,
    int AccumulatedElapsedSeconds = 0,
    bool IsPaused = false,
    DateTime? LastResumedAtUtc = null,
    Guid? ActiveTeamId = null,
    EventNavigationContext? NavigationContext = null);

public sealed record PantomimeAdminData(string Category, int TermCount, int RoundDurationSeconds, JsonElement State);

public sealed record PantomimeAssignment(Guid TeamId, Guid PerformerId);

public sealed record PantomimePerformerData(
    string Category,
    string TeamName,
    string? Term,
    bool IsRunning,
    bool IsPaused,
    bool IsEnded,
    int RemainingSeconds,
    int CorrectCount);

public sealed record ProjectGameState(
    Guid ProjectId,
    string ProjectName,
    TeamState[] Teams,
    EventState? ActiveEvent,
    EventState[] PassiveEvents,
    EventState[] ScheduledEvents,
    Guid? ActiveCanvasEventId,
    DateTime UpdatedAtUtc,
    bool IsPaused = false,
    bool IsEnded = false,
    Guid? ActiveTeamId = null);
public sealed record CanvasTeamState(Guid Id, string DisplayName, int Points, int Rank);
public sealed record CanvasEventState(Guid EventId, string SlideType, string Title, JsonElement Parameters);
public sealed record CanvasGameState(
    Guid ProjectId,
    string ProjectName,
    CanvasTeamState[] Teams,
    CanvasEventState? Event,
    DateTime UpdatedAtUtc,
    TeamPointLogDto[] PointLogs);

public sealed record ChatGroupSummary(
    Guid Id,
    string Name,
    string Type,
    Guid? AssociatedTeamId,
    int MemberCount,
    string? LastMessageText,
    DateTime? LastMessageAtUtc,
    int UnreadCount);

public sealed record ChatMessageResponse(
    Guid Id,
    Guid ChatGroupId,
    Guid? SenderParticipantId,
    Guid? SenderAdminId,
    string SenderName,
    string? TeamColor,
    string MessageText,
    bool IsAdminSender,
    DateTime SentAtUtc);

public sealed record ChatContactDto(Guid Id, string Name, string Kind);

public sealed record ProjectInfoPageDto(Guid Id, Guid ProjektId, string Title, string Icon, int Position, bool IsVisible, string ContentMarkdown, DateTime CreatedAtUtc);
public sealed record TeamNameSuggestionDto(Guid Id, string SuggestedName, string SubmittedBy, int Votes, int RequiredVotes, bool HasVoted, DateTime CreatedAtUtc);
public sealed record SongWishDto(Guid Id, string SongTitle, string Artist, string SubmittedBy, int Votes, bool HasVoted, DateTime CreatedAtUtc);
public sealed record TeamPointLogDto(Guid Id, Guid TeamId, string TeamName, string TeamColor, int DeltaPoints, int NewTotalPoints, string Reason, DateTime CreatedAtUtc);
public sealed record ProjectPermissionsDto(
    bool IsCreator = true,
    bool CanControlSession = true,
    bool CanManageParticipants = true,
    bool CanEditProjectSettings = true,
    bool CanManageShows = true);
public sealed record AdminDto(Guid Id, string Username);
public sealed record InviteAdminRequest(string Username);
public sealed record AdminShareItemDto(
    Guid AdminId,
    string Username,
    bool IsCreator,
    DateTime AddedAtUtc,
    bool CanControlSession = true,
    bool CanManageParticipants = true,
    bool CanEditProjectSettings = false,
    bool CanManageShows = false);
public sealed record AdminShareListResponse(bool CanManage, List<AdminShareItemDto> Admins);
public sealed record SpotifyAuthStartResponse(string ClientId, string RedirectUri, string State, string Challenge, string Verifier);
public sealed record SpotifyTokenResult(string AccessToken, string? RefreshToken, int ExpiresIn);
public sealed record HitsterTrackResponse(string TrackId);
public sealed record HitsterVoteResponse(int MyVotePosition);
