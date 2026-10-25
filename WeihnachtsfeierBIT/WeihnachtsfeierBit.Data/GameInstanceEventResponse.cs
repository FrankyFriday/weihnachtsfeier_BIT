using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WeihnachtsfeierBit.Data;

[Table("GameInstanceEventResponses")]
public class GameInstanceEventResponse
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid GameInstanceEventId { get; set; }
    public GameInstanceEvent GameInstanceEvent { get; set; } = null!;

    public Guid SlideId { get; set; }

    public Guid ParticipantId { get; set; }
    public Teilnehmer Participant { get; set; } = null!;

    [Column(TypeName = "jsonb")]
    public string ResponseJson { get; set; } = "{}";

    public DateTime SubmittedAtUtc { get; set; } = DateTime.UtcNow;
}
