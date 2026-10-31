using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WeihnachtsfeierBit.Data;

[Table("TeamPointLogs")]
public class TeamPointLog
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProjektId { get; set; }
    public Projekt Projekt { get; set; } = null!;

    public Guid TeamId { get; set; }
    public Team Team { get; set; } = null!;

    public int DeltaPoints { get; set; }
    public int NewTotalPoints { get; set; }
    public string Reason { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
