namespace WeihnachtsfeierBit.Data;

public class Show
{
    public Guid Id { get; set; }
    public List<ShowProjektZuo> ShowProjektZuo { get; set; } = [];
}
