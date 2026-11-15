using System.Text.Json;

namespace WeihnachtsfeierBit.Framework;

public static class ParticipantPermissionKeys
{
    public const string CanSendChatMessages = "can_send_chat";
    public const string CanReadChat = "can_read_chat";
    public const string CanCreateChats = "can_create_chats";
    public const string CanSuggestTeamNames = "can_suggest_team_names";
    public const string CanVoteTeamNames = "can_vote_team_names";
    public const string CanSuggestSongs = "can_suggest_songs";
    public const string CanVoteSongs = "can_vote_songs";
    public const string IsMuted = "is_muted";
    public const string CanChangeName = "can_change_name";

    public static readonly Dictionary<string, (string Label, string Category)> Metadata = new(StringComparer.OrdinalIgnoreCase)
    {
        [CanSendChatMessages] = ("Chat-Nachrichten schreiben", "Chat"),
        [CanReadChat] = ("Chat lesen / anzeigen", "Chat"),
        [CanCreateChats] = ("Neue Chats erstellen", "Chat"),
        [CanSuggestTeamNames] = ("Teamnamen vorschlagen", "Teamevents"),
        [CanVoteTeamNames] = ("Teamnamen abstimmen", "Teamevents"),
        [CanSuggestSongs] = ("Musikwünsche einreichen", "Musik-Jukebox"),
        [CanVoteSongs] = ("Musikwünsche abstimmen", "Musik-Jukebox"),
        [IsMuted] = ("Sprechrecht & Interaktion erlaubt", "Teilnahme"),
        [CanChangeName] = ("Eigenen Namen ändern", "Profil")
    };

    public static Dictionary<string, bool> DefaultPermissions() => new(StringComparer.OrdinalIgnoreCase)
    {
        [CanSendChatMessages] = true,
        [CanReadChat] = true,
        [CanCreateChats] = true,
        [CanSuggestTeamNames] = true,
        [CanVoteTeamNames] = true,
        [CanSuggestSongs] = true,
        [CanVoteSongs] = true,
        [IsMuted] = true,
        [CanChangeName] = true
    };

    public static Dictionary<string, bool> ParsePermissionsJson(string? json)
    {
        var dict = DefaultPermissions();
        if (string.IsNullOrWhiteSpace(json)) return dict;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.True) dict[prop.Name] = true;
                    else if (prop.Value.ValueKind == JsonValueKind.False) dict[prop.Name] = false;
                }
            }
        }
        catch (JsonException)
        {
        }

        return dict;
    }

    public static Dictionary<string, bool?> ParseNullablePermissionsJson(string? json)
    {
        var dict = new Dictionary<string, bool?>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(json)) return dict;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.True) dict[prop.Name] = true;
                    else if (prop.Value.ValueKind == JsonValueKind.False) dict[prop.Name] = false;
                    else if (prop.Value.ValueKind == JsonValueKind.Null) dict[prop.Name] = null;
                }
            }
        }
        catch (JsonException)
        {
        }

        return dict;
    }

    public static Dictionary<string, bool> ResolveEffectivePermissions(string? globalJson, string? participantJson)
    {
        var defaults = DefaultPermissions();
        var globals = ParseNullablePermissionsJson(globalJson);
        var overrides = ParseNullablePermissionsJson(participantJson);

        var effective = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in Metadata.Keys)
        {
            bool fallback = defaults.TryGetValue(key, out var d) && d;
            bool globalVal = globals.TryGetValue(key, out var g) && g.HasValue ? g.Value : fallback;
            bool effectiveVal = overrides.TryGetValue(key, out var o) && o.HasValue ? o.Value : globalVal;
            effective[key] = effectiveVal;
        }

        return effective;
    }
}
