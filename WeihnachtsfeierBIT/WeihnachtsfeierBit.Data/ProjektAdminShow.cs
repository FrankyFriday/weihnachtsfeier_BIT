namespace WeihnachtsfeierBit.Data;

public sealed class ProjektAdminShow
{
    public Guid ProjektAdminId { get; set; }

    public ProjektAdmin ProjektAdmin { get; set; } = null!;

    public Guid ShowId { get; set; }

    public Show Show { get; set; } = null!;

    public DateTime HinzugefuegtAmUtc { get; set; } = DateTime.UtcNow;
}
