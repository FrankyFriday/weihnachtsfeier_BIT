namespace WeihnachtsfeierBit.ApiService.Auth;

public sealed record ProjectAccess(
    Guid ProjectId,
    Guid SessionId,
    DateTime ExpiresAtUtc,
    Guid? ParticipantId,
    Guid? AdminId,
    bool IsCreator = false,
    bool CanControlSession = true,
    bool CanManageParticipants = true,
    bool CanEditProjectSettings = false,
    bool CanManageShows = false)
{
    public bool IsAdmin => AdminId.HasValue;
    public bool IsParticipant => ParticipantId.HasValue;
}
