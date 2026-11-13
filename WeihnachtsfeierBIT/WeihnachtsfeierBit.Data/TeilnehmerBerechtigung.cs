using System.ComponentModel.DataAnnotations.Schema;

namespace WeihnachtsfeierBit.Data;

public class TeilnehmerBerechtigung
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TeilnehmerId { get; set; }

    public Teilnehmer Teilnehmer { get; set; } = null!;

    [Column(TypeName = "jsonb")]
    public string PermissionsJson { get; set; } = "{}";
}
