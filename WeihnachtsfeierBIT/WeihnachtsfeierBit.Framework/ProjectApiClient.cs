using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace WeihnachtsfeierBit.Framework;

public sealed class ProjectApiClient(HttpClient httpClient)
{
    public async Task<ProjectSummary[]> GetAdminProjectsAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, "/api/admin/projects", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ProjectSummary[]>(cancellationToken) ?? [];
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
            ?? throw new InvalidOperationException("Die API hat keine Projektdaten zurückgegeben.");
    }

    public async Task<ProjectOverviewResponse> GetProjectOverviewAsync(
        string accessToken,
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, $"/api/projects/{projectId}/overview", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ProjectOverviewResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Die API hat keine Projektübersicht zurückgegeben.");
    }

    public async Task<ProjectResponse> RenameProjectAsync(
        string accessToken,
        Guid projectId,
        string name,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Put, $"/api/projects/{projectId}", accessToken, new RenameProjectRequest(name));
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ProjectResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Die API hat keine aktualisierten Projektdaten zurückgegeben.");
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

    public async Task<AdminShow[]> GetAdminShowsAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, "/api/admin/shows", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<AdminShow[]>(cancellationToken) ?? [];
    }

    public async Task<AdminShow> CreateAdminShowAsync(string accessToken, string name, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Post, "/api/admin/shows", accessToken, new CreateGlobalShowRequest(name));
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<AdminShow>(cancellationToken)
            ?? throw new InvalidOperationException("Die API hat keine Show zurückgegeben.");
    }

    public async Task<AdminShow> GetAdminShowAsync(string accessToken, Guid showId, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, $"/api/admin/shows/{showId}", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<AdminShow>(cancellationToken)
            ?? throw new InvalidOperationException("Die API hat keine Show zurückgegeben.");
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

    public async Task<ParticipantSessionResponse> LoginParticipantAsync(Guid projectId, int number, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/projects/{projectId}/participant-sessions")
        {
            Content = JsonContent.Create(new { number })
        };
        using var response = await httpClient.SendAsync(request, cancellationToken);
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
        using var request = CreateRequest(HttpMethod.Put, $"/api/projects/{projectId}/participants/me", accessToken, new UpdateParticipantRequest(name, null, null));
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ParticipantSummary>(cancellationToken)
            ?? throw new InvalidOperationException("Die API hat keine aktualisierten Profildaten zurückgegeben.");
    }

    public async Task<ParticipantSummary[]> GetParticipantsAsync(
        string accessToken,
        Guid projectId,
        CancellationToken cancellationToken = default)
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
        string? name,
        Guid? teamId,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Post, $"/api/projects/{projectId}/participants", accessToken, new CreateParticipantRequest(number, name, teamId));
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ParticipantSummary>(cancellationToken)
            ?? throw new InvalidOperationException("Die API hat keinen Teilnehmer zurückgegeben.");
    }

    public async Task<ParticipantSummary> UpdateParticipantAsync(
        string accessToken,
        Guid projectId,
        Guid participantId,
        int? number,
        string? name,
        Guid? teamId,
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

    public async Task<GenerateParticipantsResponse> GenerateParticipantsAsync(
        string accessToken,
        Guid projectId,
        int count,
        int digits,
        Guid? teamId,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Post, $"/api/projects/{projectId}/participants/generate", accessToken, new GenerateParticipantsRequest(count, digits, teamId));
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<GenerateParticipantsResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Die API hat kein Ergebnis der Generierung zurückgegeben.");
    }

    public async Task<RandomizeTeamsResponse> RandomizeTeamsAsync(
        string accessToken,
        Guid projectId,
        bool onlyUnassigned,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Post, $"/api/projects/{projectId}/participants/randomize-teams", accessToken, new RandomizeTeamsRequest(onlyUnassigned));
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<RandomizeTeamsResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Die API hat kein Ergebnis der Zufallsverteilung zurückgegeben.");
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

    public async Task<SlideSummary[]> GetSlidesAsync(
        string accessToken,
        Guid projectId,
        Guid showId,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, $"/api/projects/{projectId}/shows/{showId}/slides", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<SlideSummary[]>(cancellationToken) ?? [];
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
public sealed record SlideSummary(Guid Id, Guid ShowId, string Type, int Position, JsonElement Parameters);
public sealed record ParticipantState(Guid Id, int Number, string? Name);
public sealed record TeamState(Guid Id, string DisplayName, string Color, int Points, int Rank, int ParticipantCount, ParticipantState[] Members);
public sealed record ProjectGameState(Guid ProjectId, string ProjectName, TeamState[] Teams, DateTime UpdatedAtUtc);