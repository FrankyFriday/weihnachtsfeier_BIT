namespace WeihnachtsfeierBit.Data;

public class Team
{
    public Guid Id { get; set; }
    public required string DisplayName { get; set; }
    public required string Color { get; set; }
    public List<Teilnehmer> Teilnehmer { get; set; } = [];
    public Guid ProjektId { get; set; }
    public required Projekt Projekt { get; set; }
}
