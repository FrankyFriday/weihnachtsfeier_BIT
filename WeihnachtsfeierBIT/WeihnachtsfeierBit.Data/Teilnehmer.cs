namespace WeihnachtsfeierBit.Data;

public class Teilnehmer
{
    public Guid Id { get; set; }
    public int Nummer { get; set; }
    public string? Name { get; set; }
    public Team? Team { get; set; }
    public required Projekt Projekt { get; set; }
}
