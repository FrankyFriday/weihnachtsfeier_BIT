using System.Net.Http.Json;

namespace WeihnachtsfeierBit.AdminClient;

public sealed class AdminApiClient(HttpClient httpClient)
{
    public async Task<AdminRegistrationResponse> RegisterAdminAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync("/api/admin-accounts", new
        {
            username,
            password
        }, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<AdminRegistrationResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Die API hat keine Adminregistrierung zurückgegeben.");
    }

    public async Task<AdminSessionResponse> LoginAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync("/api/admin-sessions", new { username, password }, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<AdminSessionResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Die API hat keine Admin-Sitzung zurückgegeben.");
    }

    public async Task LogoutAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, "/api/admin-sessions/current");
        request.Headers.Authorization = new("Bearer", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}

public sealed record AdminRegistrationResponse(AdminAccountResponse Admin, AdminSessionResponse Session);
public sealed record AdminAccountResponse(Guid Id, string Benutzername);
public sealed record AdminSessionResponse(Guid SessionId, string AccessToken, DateTime ExpiresAtUtc, string Role, Guid? ProjectId, string DisplayName);