namespace WeihnachtsfeierBit.ApiService.Auth;

public sealed record ProjectAccess(Guid ProjectId, Guid? ParticipantId, Guid? AdminId)
{
    public bool IsAdmin => AdminId.HasValue;
}