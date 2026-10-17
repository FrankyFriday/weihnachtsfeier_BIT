namespace WeihnachtsfeierBit.Data;

public sealed class Sitzung
{
    public Guid Id { get; set; }

    public Guid TeilnehmerId { get; set; }

    public Teilnehmer Teilnehmer { get; set; } = null!;

    public Guid? GameInstanceId { get; set; }

    public GameInstance? GameInstance { get; set; }

    public required string TokenHash { get; set; }

    public DateTime ErstelltAmUtc { get; set; } = DateTime.UtcNow;

    public DateTime LetzteAktivitaetAmUtc { get; set; } = DateTime.UtcNow;

    public DateTime LaeuftAbAmUtc { get; set; }

    public DateTime? WiderrufenAmUtc { get; set; }

    public string? UserAgent { get; set; }
}