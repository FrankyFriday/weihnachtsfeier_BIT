namespace WeihnachtsfeierBit.Data;

public class GameInstance
{
    public Guid Id { get; set; }
    public Guid ProjektId { get; set; }
    public required Projekt Projekt { get; set; }
    public string? AccessCode { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? EndedAtUtc { get; set; }

    public List<TeamInstance> TeamInstances { get; set; } = [];
    public List<TeilnehmerInstance> TeilnehmerInstances { get; set; } = [];
    public List<Sitzung> Sitzungen { get; set; } = [];
}
