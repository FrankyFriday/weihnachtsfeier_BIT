using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WeihnachtsfeierBit.Data;

[Table("ChatMessages")]
public class ChatMessage
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ChatGroupId { get; set; }
    public ChatGroup ChatGroup { get; set; } = null!;

    public Guid? SenderParticipantId { get; set; }
    public Teilnehmer? SenderParticipant { get; set; }

    public Guid? SenderAdminId { get; set; }
    public ProjektAdmin? SenderAdmin { get; set; }

    public string SenderName { get; set; } = string.Empty;
    public string? TeamColor { get; set; }

    public string MessageText { get; set; } = string.Empty;

    public DateTime SentAtUtc { get; set; } = DateTime.UtcNow;
}
