using WeihnachtsfeierBit.Framework;

namespace WeihnachtsfeierBit.ApiService.Tests;

public sealed class ParticipantPermissionKeysTests
{
    private static readonly System.Text.Json.JsonSerializerOptions WebJsonOptions =
        new(System.Text.Json.JsonSerializerDefaults.Web);

    [Fact]
    public void ResolveEffectivePermissions_AppliesChatCreationDefaultAndParticipantOverride()
    {
        var globalPermissions = System.Text.Json.JsonSerializer.Serialize(
            new Dictionary<string, bool?> { [ParticipantPermissionKeys.CanCreateChats] = false });

        var inherited = ParticipantPermissionKeys.ResolveEffectivePermissions(globalPermissions, null);
        var forced = ParticipantPermissionKeys.ResolveEffectivePermissions(
            globalPermissions,
            System.Text.Json.JsonSerializer.Serialize(
                new Dictionary<string, bool?> { [ParticipantPermissionKeys.CanCreateChats] = true }));

        Assert.False(inherited[ParticipantPermissionKeys.CanCreateChats]);
        Assert.True(forced[ParticipantPermissionKeys.CanCreateChats]);
    }

    [Fact]
    public void ParticipantPermissions_DeserializesFlatApiPermissionMap()
    {
        var participant = System.Text.Json.JsonSerializer.Deserialize<ParticipantSummary>(
            "{\"id\":\"00000000-0000-0000-0000-000000000001\",\"number\":1,\"name\":\"Test\",\"teamId\":null,\"permissions\":{\"can_read_chat\":false,\"is_muted\":false}}",
            WebJsonOptions);

        Assert.NotNull(participant?.Permissions);
        Assert.False(participant.Permissions.HasPermission(ParticipantPermissionKeys.CanReadChat));
        Assert.False(participant.Permissions.HasPermission(ParticipantPermissionKeys.IsMuted));
    }

    [Fact]
    public void ParticipantPermissions_RoundTripsNestedGameStateShape()
    {
        var original = new ParticipantPermissions(new Dictionary<string, bool>
        {
            [ParticipantPermissionKeys.CanReadChat] = false,
            [ParticipantPermissionKeys.IsMuted] = false
        });

        var json = System.Text.Json.JsonSerializer.Serialize(original, WebJsonOptions);
        var restored = System.Text.Json.JsonSerializer.Deserialize<ParticipantPermissions>(json, WebJsonOptions);

        Assert.NotNull(restored);
        Assert.False(restored.HasPermission(ParticipantPermissionKeys.CanReadChat));
        Assert.False(restored.HasPermission(ParticipantPermissionKeys.IsMuted));
    }
}
