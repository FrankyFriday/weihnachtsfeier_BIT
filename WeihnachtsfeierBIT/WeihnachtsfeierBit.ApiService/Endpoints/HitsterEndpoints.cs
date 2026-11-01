using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using WeihnachtsfeierBit.ApiService.Auth;
using WeihnachtsfeierBit.ApiService.Contracts;
using WeihnachtsfeierBit.ApiService.Services;
using WeihnachtsfeierBit.Data;

namespace WeihnachtsfeierBit.ApiService.Endpoints;

public static class HitsterEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Lazy<HitsterSong[]> Playlist = new(LoadPlaylist);

    public static void Map(RouteGroupBuilder reader, RouteGroupBuilder admin)
    {
        reader.MapPost("/session/events/{eventId:guid}/hitster/{slideId:guid}/guess", GuessAsync);
        admin.MapPost("/session/events/{eventId:guid}/hitster/{slideId:guid}/start", StartAsync);
        admin.MapGet("/session/events/{eventId:guid}/hitster/{slideId:guid}/track", GetCurrentTrackAsync);
        admin.MapPost("/session/events/{eventId:guid}/hitster/{slideId:guid}/reveal", RevealAsync);
        admin.MapPost("/session/events/{eventId:guid}/hitster/{slideId:guid}/next", NextAsync);
        admin.MapPost("/session/spotify/auth", BeginSpotifyAuth);
        admin.MapPost("/session/spotify/token", GetSpotifyTokenAsync);
    }

    private static IResult BeginSpotifyAuth(
        IConfiguration configuration)
    {
        var clientId = configuration["Spotify:ClientId"];
        var redirectUri = configuration["Spotify:RedirectUri"];
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(redirectUri) ||
            !Uri.TryCreate(redirectUri, UriKind.Absolute, out var callback) || callback is null)
        {
            return InvalidSpotifyConfiguration();
        }

        if ((callback.Scheme != Uri.UriSchemeHttps &&
             !(callback.Scheme == Uri.UriSchemeHttp && callback.IsLoopback)) ||
            callback.AbsolutePath != "/admin/spotify-callback.html" ||
            !string.IsNullOrEmpty(callback.Query) ||
            !string.IsNullOrEmpty(callback.Fragment))
        {
            return InvalidSpotifyConfiguration();
        }

        var state = CreateRandomValue(24);
        var verifier = CreateRandomValue(48);
        var challenge = Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        return Results.Ok(new SpotifyAuthStartResponse(clientId, redirectUri, state, challenge, verifier));
    }

    private static IResult InvalidSpotifyConfiguration() =>
        Results.Json(
            new ApiError("Spotify ist nicht korrekt konfiguriert. SPOTIFY_REDIRECT_URI muss auf /admin/spotify-callback.html zeigen."),
            statusCode: StatusCodes.Status503ServiceUnavailable);

    private static async Task<IResult> GetSpotifyTokenAsync(
        SpotifyTokenRequest request,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        CancellationToken cancellationToken)
    {
        var clientId = configuration["Spotify:ClientId"];
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return Results.Json(new ApiError("SPOTIFY_CLIENT_ID fehlt."), statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        if (string.IsNullOrWhiteSpace(configuration["Spotify:RedirectUri"]))
        {
            return InvalidSpotifyConfiguration();
        }

        var form = new Dictionary<string, string>
        {
            ["client_id"] = clientId
        };
        if (string.Equals(request.GrantType, "authorization_code", StringComparison.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(request.Code) ||
                string.IsNullOrWhiteSpace(request.State) ||
                !IsValidCodeVerifier(request.CodeVerifier))
            {
                return Results.BadRequest(new ApiError("Spotify-Autorisierungscode oder PKCE-Daten sind ungültig."));
            }

            form["grant_type"] = "authorization_code";
            form["code"] = request.Code;
            form["redirect_uri"] = configuration["Spotify:RedirectUri"]!;
            form["code_verifier"] = request.CodeVerifier!;
        }
        else if (string.Equals(request.GrantType, "refresh_token", StringComparison.Ordinal) &&
                 !string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            form["grant_type"] = "refresh_token";
            form["refresh_token"] = request.RefreshToken;
        }
        else
        {
            return Results.BadRequest(new ApiError("Spotify-Token-Anfrage ist ungültig."));
        }

        using var response = await httpClientFactory.CreateClient()
            .PostAsync("https://accounts.spotify.com/api/token", new FormUrlEncodedContent(form), cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadFromJsonAsync<SpotifyTokenError>(cancellationToken);
            return Results.BadRequest(new ApiError(error?.ErrorDescription ?? error?.Error ?? "Spotify hat die Anmeldung abgelehnt."));
        }

        var token = await response.Content.ReadFromJsonAsync<SpotifyTokenResponse>(cancellationToken);
        if (token is null || string.IsNullOrWhiteSpace(token.AccessToken))
        {
            return Results.Json(new ApiError("Spotify hat kein gültiges Zugriffstoken zurückgegeben."), statusCode: StatusCodes.Status502BadGateway);
        }

        return Results.Ok(new SpotifyTokenResult(token.AccessToken, token.RefreshToken, token.ExpiresIn));
    }

    private static async Task<IResult> StartAsync(
        Guid projectId,
        Guid eventId,
        Guid slideId,
        WeihnachtsfeierDbContext db,
        IGameStateNotifier notifier,
        CancellationToken cancellationToken)
    {
        var gameEvent = await GetEventAsync(db, projectId, eventId, cancellationToken);
        if (gameEvent is null)
        {
            return Results.NotFound();
        }
        if (!TryGetActiveHitsterParameters(gameEvent, slideId, out var root, out var parameters))
        {
            return Results.BadRequest(new ApiError("Die aktuelle Slide ist keine gültige Hitster-Slide."));
        }

        var teams = await db.Teams.AsNoTracking()
            .Where(team => team.ProjektId == projectId &&
                db.Teilnehmer.Any(participant => participant.ProjektId == projectId && participant.TeamId == team.Id))
            .OrderBy(team => team.DisplayName)
            .Select(team => new HitsterTeamState { Id = team.Id, Name = team.DisplayName })
            .ToListAsync(cancellationToken);
        if (teams.Count < 2)
        {
            return Results.BadRequest(new ApiError("Hitster benötigt mindestens zwei Teams mit Teilnehmenden."));
        }

        var cardsToWin = 10;
        if (parameters["cardsToWin"] is JsonNode configuredCards &&
            (configuredCards is not JsonValue cardsValue || !cardsValue.TryGetValue<int>(out cardsToWin)))
        {
            return Results.BadRequest(new ApiError("Die Siegbedingung ist ungültig."));
        }
        if (cardsToWin is < 3 or > 20)
        {
            return Results.BadRequest(new ApiError("Die Siegbedingung muss zwischen 3 und 20 Karten liegen."));
        }
        if (Playlist.Value.Length < teams.Count + cardsToWin)
        {
            return Results.BadRequest(new ApiError("Die Playlist enthält zu wenige Songs für diese Runde."));
        }

        var deck = Shuffle(Playlist.Value);
        var teamOrder = Shuffle(teams);
        for (var index = 0; index < teamOrder.Count; index++)
        {
            teamOrder[index].Timeline.Add(deck[index]);
        }

        var state = new HitsterGameState
        {
            Started = true,
            Phase = "playing",
            CardsToWin = cardsToWin,
            TeamOrder = teamOrder.Select(team => team.Id).ToList(),
            Teams = teamOrder,
            Deck = deck,
            CurrentSongIndex = teamOrder.Count
        };
        parameters["game"] = JsonSerializer.SerializeToNode(state, JsonOptions);
        gameEvent.SlidesJson = root.ToJsonString(JsonOptions);
        await db.SaveChangesAsync(cancellationToken);
        await notifier.NotifyProjectChangedAsync(projectId, cancellationToken);
        return Results.Ok(HitsterGameService.PublicState(state));
    }

    private static async Task<IResult> GetCurrentTrackAsync(
        Guid projectId,
        Guid eventId,
        Guid slideId,
        WeihnachtsfeierDbContext db,
        CancellationToken cancellationToken)
    {
        var gameEvent = await GetEventAsync(db, projectId, eventId, cancellationToken);
        if (gameEvent is null)
        {
            return Results.NotFound();
        }
        if (!TryReadGame(gameEvent, slideId, out var state) || !state.Started || state.Finished ||
            state.Phase != "playing" || state.CurrentSongIndex >= state.Deck.Count)
        {
            return Results.Conflict(new ApiError("Es läuft gerade kein Hitster-Song."));
        }

        return Results.Ok(new HitsterTrackResponse(state.Deck[state.CurrentSongIndex].Id));
    }

    private static async Task<IResult> GuessAsync(
        Guid projectId,
        Guid eventId,
        Guid slideId,
        HitsterGuessRequest request,
        HttpContext httpContext,
        WeihnachtsfeierDbContext db,
        IGameStateNotifier notifier,
        CancellationToken cancellationToken)
    {
        var access = (ProjectAccess)httpContext.Items[RequireProjectAccessFilter.AccessItemKey]!;
        if (access.ParticipantId is not Guid participantId)
        {
            return Results.Forbid();
        }

        var gameEvent = await GetEventAsync(db, projectId, eventId, cancellationToken);
        if (gameEvent is null)
        {
            return Results.NotFound();
        }
        if (!TryReadGame(gameEvent, slideId, out var state) || !state.Started ||
            state.Phase != "playing" || state.Finished)
        {
            return Results.Conflict(new ApiError("Hitster wartet aktuell nicht auf eine Einordnung."));
        }

        var teamId = await db.Teilnehmer
            .Where(participant => participant.Id == participantId && participant.ProjektId == projectId)
            .Select(participant => participant.TeamId)
            .FirstOrDefaultAsync(cancellationToken);
        if (teamId is null || teamId != state.CurrentTeamId)
        {
            return Results.Forbid();
        }

        var team = state.Teams.FirstOrDefault(item => item.Id == teamId.Value);
        if (team is null)
        {
            return Results.Forbid();
        }
        if (request.Position < 0 || request.Position > team.Timeline.Count)
        {
            return Results.BadRequest(new ApiError("Die gewählte Position ist ungültig."));
        }
        if (state.GuessPosition.HasValue)
        {
            return Results.Conflict(new ApiError("Für diesen Zug wurde bereits eine Position gewählt."));
        }

        state.GuessPosition = request.Position;
        await SaveStateAsync(gameEvent, slideId, state, db, cancellationToken);
        await notifier.NotifyProjectChangedAsync(projectId, cancellationToken);
        return Results.Ok(HitsterGameService.PublicState(state));
    }

    private static async Task<IResult> RevealAsync(
        Guid projectId,
        Guid eventId,
        Guid slideId,
        WeihnachtsfeierDbContext db,
        IGameStateNotifier notifier,
        CancellationToken cancellationToken)
    {
        var gameEvent = await GetEventAsync(db, projectId, eventId, cancellationToken);
        if (gameEvent is null)
        {
            return Results.NotFound();
        }
        if (!TryReadGame(gameEvent, slideId, out var state) || state.Phase != "playing" ||
            !state.GuessPosition.HasValue || state.CurrentSongIndex >= state.Deck.Count)
        {
            return Results.Conflict(new ApiError("Es gibt noch keinen gültigen Tipp zum Aufdecken."));
        }

        var song = state.Deck[state.CurrentSongIndex];
        var team = state.Teams.FirstOrDefault(item => item.Id == state.CurrentTeamId);
        if (team is null || state.GuessPosition.Value > team.Timeline.Count)
        {
            return Results.Conflict(new ApiError("Der gespeicherte Hitster-Zug ist ungültig."));
        }

        var position = state.GuessPosition.Value;
        var before = position == 0 || team.Timeline[position - 1].Year <= song.Year;
        var after = position == team.Timeline.Count || song.Year <= team.Timeline[position].Year;
        state.LastGuessCorrect = before && after;
        if (state.LastGuessCorrect.Value)
        {
            team.Timeline.Insert(position, song);
        }

        state.Revealed = true;
        state.Phase = "revealed";
        await SaveStateAsync(gameEvent, slideId, state, db, cancellationToken);
        await notifier.NotifyProjectChangedAsync(projectId, cancellationToken);
        return Results.Ok(HitsterGameService.PublicState(state));
    }

    private static async Task<IResult> NextAsync(
        Guid projectId,
        Guid eventId,
        Guid slideId,
        WeihnachtsfeierDbContext db,
        IGameStateNotifier notifier,
        CancellationToken cancellationToken)
    {
        var gameEvent = await GetEventAsync(db, projectId, eventId, cancellationToken);
        if (gameEvent is null)
        {
            return Results.NotFound();
        }
        if (!TryReadGame(gameEvent, slideId, out var state) || state.Phase != "revealed")
        {
            return Results.Conflict(new ApiError("Decke zuerst den aktuellen Song auf."));
        }

        var winners = state.Teams.Where(team => team.Timeline.Count >= state.CardsToWin).ToList();
        if (winners.Count > 0)
        {
            state.Finished = true;
            state.Phase = "finished";
            state.WinnerTeamIds = winners.Select(team => team.Id).ToList();
        }
        else if (state.CurrentSongIndex + 1 >= state.Deck.Count)
        {
            var mostCards = state.Teams.Max(team => team.Timeline.Count);
            state.Finished = true;
            state.Phase = "finished";
            state.WinnerTeamIds = state.Teams
                .Where(team => team.Timeline.Count == mostCards)
                .Select(team => team.Id)
                .ToList();
        }
        else
        {
            state.CurrentSongIndex++;
            state.CurrentTeamIndex = (state.CurrentTeamIndex + 1) % state.TeamOrder.Count;
            state.GuessPosition = null;
            state.LastGuessCorrect = null;
            state.Revealed = false;
            state.Phase = "playing";
        }

        await SaveStateAsync(gameEvent, slideId, state, db, cancellationToken);
        await notifier.NotifyProjectChangedAsync(projectId, cancellationToken);
        return Results.Ok(HitsterGameService.PublicState(state));
    }

    private static Task<GameInstanceEvent?> GetEventAsync(
        WeihnachtsfeierDbContext db,
        Guid projectId,
        Guid eventId,
        CancellationToken cancellationToken) =>
        db.GameInstanceEvents.FirstOrDefaultAsync(
            item => item.Id == eventId && item.GameInstance.ProjektId == projectId &&
                item.Status == "active" && item.Mode == "active",
            cancellationToken);

    private static async Task SaveStateAsync(
        GameInstanceEvent gameEvent,
        Guid slideId,
        HitsterGameState state,
        WeihnachtsfeierDbContext db,
        CancellationToken cancellationToken)
    {
        if (!TryGetActiveHitsterParameters(gameEvent, slideId, out var root, out var parameters))
        {
            throw new InvalidOperationException("Die aktive Hitster-Slide konnte nicht aktualisiert werden.");
        }

        parameters["game"] = JsonSerializer.SerializeToNode(state, JsonOptions);
        gameEvent.SlidesJson = root.ToJsonString(JsonOptions);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static bool TryGetActiveHitsterParameters(
        GameInstanceEvent gameEvent,
        Guid slideId,
        out JsonArray root,
        out JsonObject parameters)
    {
        root = [];
        parameters = null!;
        try
        {
            root = JsonNode.Parse(gameEvent.SlidesJson) as JsonArray ?? [];
            var index = -1;
            for (var current = 0; current < root.Count; current++)
            {
                if (Guid.TryParse(root[current]?["slideId"]?.GetValue<string>(), out var currentId) && currentId == slideId)
                {
                    index = current;
                    break;
                }
            }

            var slide = index == gameEvent.CurrentSlideIndex && index >= 0 &&
                string.Equals(root[index]?["type"]?.GetValue<string>(), "hitster", StringComparison.OrdinalIgnoreCase)
                ? root[index] as JsonObject
                : null;
            parameters = slide?["parameters"] as JsonObject ?? null!;
            return parameters is not null;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException)
        {
            return false;
        }
    }

    private static bool TryReadGame(GameInstanceEvent gameEvent, Guid slideId, out HitsterGameState state)
    {
        state = new HitsterGameState();
        try
        {
            using var document = JsonDocument.Parse(gameEvent.SlidesJson);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var slides = document.RootElement.EnumerateArray().ToArray();
            if (gameEvent.CurrentSlideIndex < 0 || gameEvent.CurrentSlideIndex >= slides.Length)
            {
                return false;
            }

            var slide = slides[gameEvent.CurrentSlideIndex];
            if (!slide.TryGetProperty("slideId", out var slideIdValue) ||
                !slideIdValue.TryGetGuid(out var activeSlideId) ||
                activeSlideId != slideId ||
                !slide.TryGetProperty("type", out var type) ||
                !string.Equals(type.GetString(), "hitster", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (slide.ValueKind != JsonValueKind.Object ||
                !slide.TryGetProperty("parameters", out var parameters) ||
                !parameters.TryGetProperty("game", out var game))
            {
                return false;
            }

            state = game.Deserialize<HitsterGameState>(JsonOptions)!;
            return state is not null && state.TeamOrder.Count > 0 &&
                state.CurrentTeamIndex >= 0 && state.CurrentTeamIndex < state.TeamOrder.Count &&
                state.CurrentSongIndex >= 0 && state.CurrentSongIndex < state.Deck.Count;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            return false;
        }
    }

    private static List<T> Shuffle<T>(IEnumerable<T> items)
    {
        var shuffled = items.ToList();
        for (var index = shuffled.Count - 1; index > 0; index--)
        {
            var other = Random.Shared.Next(index + 1);
            (shuffled[index], shuffled[other]) = (shuffled[other], shuffled[index]);
        }

        return shuffled;
    }

    private static HitsterSong[] LoadPlaylist()
    {
        using var stream = typeof(HitsterEndpoints).Assembly.GetManifestResourceStream("WeihnachtsfeierBit.ApiService.hitster-songs.json")
            ?? throw new InvalidOperationException("Die Hitster-Playlist wurde nicht in die API eingebunden.");
        return JsonSerializer.Deserialize<HitsterSong[]>(stream, JsonOptions) is { Length: > 0 } songs
            ? songs
            : throw new InvalidOperationException("Die Hitster-Playlist ist leer oder ungültig.");
    }

    private static bool IsValidCodeVerifier(string? verifier) =>
        verifier is { Length: >= 43 and <= 128 } &&
        verifier.All(character =>
            char.IsAsciiLetterOrDigit(character) || character is '-' or '_');

    private static string CreateRandomValue(int byteCount) =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(byteCount))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}

public sealed record SpotifyAuthStartResponse(string ClientId, string RedirectUri, string State, string CodeChallenge, string CodeVerifier);
public sealed record SpotifyTokenRequest(string GrantType, string? Code, string? State, string? RefreshToken, string? CodeVerifier);
public sealed record SpotifyTokenResult(string AccessToken, string? RefreshToken, int ExpiresIn);
public sealed record SpotifyTokenResponse(string AccessToken, string? RefreshToken, int ExpiresIn);
public sealed record SpotifyTokenError(string? Error, string? ErrorDescription);
public sealed record HitsterGuessRequest(int Position);
public sealed record HitsterTrackResponse(string TrackId);
public sealed record HitsterSong(string Id, string Title, string Artist, int Year);

public sealed class HitsterTeamState
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<HitsterSong> Timeline { get; set; } = [];
}

public sealed class HitsterGameState
{
    public bool Started { get; set; }
    public string Phase { get; set; } = "waiting";
    public int CardsToWin { get; set; } = 10;
    public List<Guid> TeamOrder { get; set; } = [];
    public int CurrentTeamIndex { get; set; }
    public Guid CurrentTeamId => TeamOrder.Count == 0 ? Guid.Empty : TeamOrder[CurrentTeamIndex % TeamOrder.Count];
    public List<HitsterTeamState> Teams { get; set; } = [];
    public List<HitsterSong> Deck { get; set; } = [];
    public int CurrentSongIndex { get; set; }
    public int? GuessPosition { get; set; }
    public bool? LastGuessCorrect { get; set; }
    public bool Revealed { get; set; }
    public bool Finished { get; set; }
    public List<Guid> WinnerTeamIds { get; set; } = [];
}

public static class HitsterGameService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static JsonElement PublicState(HitsterGameState state)
    {
        var currentSong = state.Revealed && state.CurrentSongIndex < state.Deck.Count
            ? PublicSong(state.Deck[state.CurrentSongIndex])
            : null;
        return JsonSerializer.SerializeToElement(new
        {
            started = state.Started,
            phase = state.Phase,
            cardsToWin = state.CardsToWin,
            currentTeamId = state.Finished ? (Guid?)null : state.CurrentTeamId,
            currentTeamName = state.Finished ? null : state.Teams.FirstOrDefault(team => team.Id == state.CurrentTeamId)?.Name,
            round = state.CurrentSongIndex + 1,
            remainingSongs = Math.Max(0, state.Deck.Count - state.CurrentSongIndex),
            guessPosition = state.GuessPosition,
            lastGuessCorrect = state.LastGuessCorrect,
            finished = state.Finished,
            winnerTeamIds = state.WinnerTeamIds,
            winnerNames = state.Teams.Where(team => state.WinnerTeamIds.Contains(team.Id)).Select(team => team.Name),
            currentSong,
            teams = state.Teams.Select(team => new
            {
                team.Id,
                team.Name,
                timeline = team.Timeline.Select(PublicSong)
            })
        }, JsonOptions);
    }

    public static JsonElement PublicParameters(JsonElement parameters)
    {
        var result = new Dictionary<string, JsonElement>();
        if (parameters.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in new[] { "title", "instructions", "cardsToWin" })
            {
                if (parameters.TryGetProperty(property, out var value))
                {
                    result[property] = value.Clone();
                }
            }

            if (parameters.TryGetProperty("game", out var game))
            {
                try
                {
                    var state = game.Deserialize<HitsterGameState>(JsonOptions);
                    if (state is not null)
                    {
                        result["game"] = PublicState(state);
                    }
                }
                catch (JsonException)
                {
                }
            }
        }

        return JsonSerializer.SerializeToElement(result, JsonOptions);
    }

    private static object PublicSong(HitsterSong song) => new { song.Title, song.Artist, song.Year };
}
