namespace WeihnachtsfeierBit.AdminClient;

public sealed class ProjectShowLinksState
{
    public event Action<Guid, object>? LinksChanged;

    public void NotifyChanged(Guid projectId, object source) => LinksChanged?.Invoke(projectId, source);
}