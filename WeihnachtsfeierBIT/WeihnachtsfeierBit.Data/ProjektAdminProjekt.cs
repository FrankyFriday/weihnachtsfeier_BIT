namespace WeihnachtsfeierBit.Data;

public sealed class ProjektAdminProjekt
{
    public Guid ProjektAdminId { get; set; }

    public ProjektAdmin ProjektAdmin { get; set; } = null!;

    public Guid ProjektId { get; set; }

    public Projekt Projekt { get; set; } = null!;

    public DateTime HinzugefuegtAmUtc { get; set; } = DateTime.UtcNow;
}