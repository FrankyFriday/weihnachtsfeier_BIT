using System.Text.Json;

namespace WeihnachtsfeierBit.Framework;

public static class ParticipantPermissionKeys
{
    public const string CanSendChatMessages = "can_send_chat";
    public const string CanReadChat = "can_read_chat";
    public const string CanSuggestTeamNames = "can_suggest_team_names";
    public const string CanVoteTeamNames = "can_vote_team_names";
    public const string CanSuggestSongs = "can_suggest_songs";
    public const string CanVoteSongs = "can_vote_songs";
    public const string IsMuted = "is_muted";

    public static readonly Dictionary<string, (string Label, string Category)> Metadata = new(StringComparer.OrdinalIgnoreCase)
    {
        [CanSendChatMessages] = ("Chat-Nachrichten schreiben", "Chat"),
        [CanReadChat] = ("Chat lesen / anzeigen", "Chat"),
        [CanSuggestTeamNames] = ("Teamnamen vorschlagen", "Teamevents"),
        [CanVoteTeamNames] = ("Teamnamen abstimmen", "Teamevents"),
        [CanSuggestSongs] = ("Musikwünsche einreichen", "Musik-Jukebox"),
        [CanVoteSongs] = ("Musikwünsche abstimmen", "Musik-Jukebox"),
        [IsMuted] = ("Teilnehmer komplett stummschalten", "Sicherheit")
    };

    public static Dictionary<string, bool> DefaultPermissions() => new(StringComparer.OrdinalIgnoreCase)
    {
        [CanSendChatMessages] = true,
        [CanReadChat] = true,
        [CanSuggestTeamNames] = true,
        [CanVoteTeamNames] = true,
        [CanSuggestSongs] = true,
        [CanVoteSongs] = true,
        [IsMuted] = false
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
}
