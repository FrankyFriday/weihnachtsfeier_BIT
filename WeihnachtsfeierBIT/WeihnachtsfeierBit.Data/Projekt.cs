namespace WeihnachtsfeierBit.Data;

public class Projekt
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public Guid? ErstellerAdminId { get; set; }
    public ProjektAdmin? ErstellerAdmin { get; set; }
    public List<ShowProjektZuo> ShowProjektZuo { get; set; } = [];
    public List<ProjektAdminProjekt> Admins { get; set; } = [];
}
