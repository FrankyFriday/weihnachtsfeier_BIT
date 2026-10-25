using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WeihnachtsfeierBit.Data;

[Table("ChatGroupMembers")]
public class ChatGroupMember
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ChatGroupId { get; set; }
    public ChatGroup ChatGroup { get; set; } = null!;

    public Guid ParticipantId { get; set; }
    public Teilnehmer Participant { get; set; } = null!;

    public DateTime? LastReadAtUtc { get; set; }

    public DateTime JoinedAtUtc { get; set; } = DateTime.UtcNow;
}
