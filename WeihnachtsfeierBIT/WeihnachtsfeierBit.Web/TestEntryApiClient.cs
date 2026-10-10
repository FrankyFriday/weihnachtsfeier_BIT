using System.Net.Http.Json;

namespace WeihnachtsfeierBit.Web;

public sealed class TestEntryApiClient(HttpClient httpClient)
{
    public async Task<TestEntryDto[]> GetEntriesAsync(CancellationToken cancellationToken = default)
    {
        return await httpClient.GetFromJsonAsync<TestEntryDto[]>("/test-entries", cancellationToken) ?? [];
    }

    public async Task<TestEntryDto> CreateEntryAsync(string name, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync("/test-entries", new CreateTestEntryRequest(name), cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<TestEntryDto>(cancellationToken)
            ?? throw new InvalidOperationException("Die API hat keinen Tabelleneintrag zurückgegeben.");
    }
}

public sealed record TestEntryDto(int Id, string Name, DateTime CreatedAtUtc);

internal sealed record CreateTestEntryRequest(string Name);