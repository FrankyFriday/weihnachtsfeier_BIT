namespace WeihnachtsfeierBit.Data;

public class Projekt
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public List<ShowProjektZuo> ShowProjektZuo { get; set; } = [];
}
