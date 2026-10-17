namespace WeihnachtsfeierBit.Data;

public class TeamInstance
{
    public Guid Id { get; set; }
    public Guid GameInstanceId { get; set; }
    public required GameInstance GameInstance { get; set; }
    public Guid TeamId { get; set; }
    public required Team Team { get; set; }
    public int Punkte { get; set; }
}
