namespace WeihnachtsfeierBit.Data;

public sealed class ProjektAdminSitzung
{
    public Guid Id { get; set; }

    public Guid ProjektAdminId { get; set; }

    public ProjektAdmin ProjektAdmin { get; set; } = null!;

    public required string TokenHash { get; set; }

    public DateTime ErstelltAmUtc { get; set; } = DateTime.UtcNow;

    public DateTime LetzteAktivitaetAmUtc { get; set; } = DateTime.UtcNow;

    public DateTime LaeuftAbAmUtc { get; set; }

    public DateTime? WiderrufenAmUtc { get; set; }

    public string? UserAgent { get; set; }
}