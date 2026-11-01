namespace WeihnachtsfeierBit.Data;

public class Show
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "Neue Show";
    public Guid? ErstellerAdminId { get; set; }
    public ProjektAdmin? ErstellerAdmin { get; set; }
    public List<ShowProjektZuo> ShowProjektZuo { get; set; } = [];
    public List<ProjektAdminShow> Admins { get; set; } = [];
    public List<Slide> Slides { get; set; } = [];
}
