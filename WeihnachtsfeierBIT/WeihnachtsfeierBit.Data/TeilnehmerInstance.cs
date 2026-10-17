namespace WeihnachtsfeierBit.Data;

public class TeilnehmerInstance
{
    public Guid Id { get; set; }
    public Guid GameInstanceId { get; set; }
    public required GameInstance GameInstance { get; set; }
    public Guid TeilnehmerId { get; set; }
    public required Teilnehmer Teilnehmer { get; set; }
    public Guid? TeamId { get; set; }
    public Team? Team { get; set; }
}
