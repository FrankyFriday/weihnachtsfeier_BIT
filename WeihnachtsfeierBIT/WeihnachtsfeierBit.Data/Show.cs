namespace WeihnachtsfeierBit.Data;

public class Show
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "Neue Show";
    public List<ShowProjektZuo> ShowProjektZuo { get; set; } = [];
    public List<Slide> Slides { get; set; } = [];
}
