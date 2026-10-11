namespace WeihnachtsfeierBit.Data;

public sealed class ProjektAdmin
{
    public Guid Id { get; set; }

    public required string Benutzername { get; set; }

    public required string BenutzernameNormalisiert { get; set; }

    public required string PasswortHash { get; set; }

    public DateTime ErstelltAmUtc { get; set; } = DateTime.UtcNow;

    public List<ProjektAdminProjekt> Projekte { get; set; } = [];

    public List<ProjektAdminSitzung> Sitzungen { get; set; } = [];
}