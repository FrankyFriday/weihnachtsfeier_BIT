using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WeihnachtsfeierBit.Data;

[Table("ChatUserReads")]
public class ChatUserRead
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ChatGroupId { get; set; }
    public ChatGroup ChatGroup { get; set; } = null!;

    public Guid? ParticipantId { get; set; }
    public Teilnehmer? Participant { get; set; }

    public Guid? ProjektAdminId { get; set; }
    public ProjektAdmin? ProjektAdmin { get; set; }

    public DateTime LastReadAtUtc { get; set; } = DateTime.UtcNow;
}
