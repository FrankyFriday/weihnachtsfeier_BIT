using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace WeihnachtsfeierBit.ApiService.Services;

public sealed class SpotifyAuthFlowStore
{
    private readonly ConcurrentDictionary<string, SpotifyAuthFlow> _flows = new(StringComparer.Ordinal);

    public (string State, string Challenge) Create(string redirectUri)
    {
        var verifier = CreateRandomValue(48);
        var state = CreateRandomValue(24);
        var challenge = Convert.ToBase64String(SHA256.HashData(System.Text.Encoding.ASCII.GetBytes(verifier)))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        var now = DateTimeOffset.UtcNow;
        foreach (var flow in _flows.Where(item => item.Value.ExpiresAtUtc <= now).ToArray())
        {
            _flows.TryRemove(flow.Key, out _);
        }

        _flows[state] = new SpotifyAuthFlow(verifier, redirectUri, now.AddMinutes(10));
        return (state, challenge);
    }

    public SpotifyAuthFlow? Consume(string state)
    {
        if (!_flows.TryRemove(state, out var flow) || flow.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            return null;
        }

        return flow;
    }

    private static string CreateRandomValue(int byteCount) =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(byteCount))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}

public sealed record SpotifyAuthFlow(string Verifier, string RedirectUri, DateTimeOffset ExpiresAtUtc);
