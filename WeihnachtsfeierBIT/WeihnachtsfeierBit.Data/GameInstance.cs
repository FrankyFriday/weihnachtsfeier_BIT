namespace WeihnachtsfeierBit.Data;

public class GameInstance
{
    public Guid Id { get; set; }
    public Guid ProjektId { get; set; }
    public required Projekt Projekt { get; set; }
    public required string AccessCode { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<Sitzung> Sitzungen { get; set; } = [];
    public List<GameInstanceShow> Shows { get; set; } = [];
    public List<GameInstanceEvent> Events { get; set; } = [];
}
