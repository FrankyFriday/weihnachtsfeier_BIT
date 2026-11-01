namespace WeihnachtsfeierBit.ApiService.Auth;

public sealed record ProjectAccess(Guid ProjectId, Guid SessionId, DateTime ExpiresAtUtc, Guid? ParticipantId, Guid? AdminId)
{
    public bool IsAdmin => AdminId.HasValue;
}