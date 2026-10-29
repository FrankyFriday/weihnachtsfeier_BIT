using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WeihnachtsfeierBit.Data;

[Table("TeamNameVotes")]
public class TeamNameVote
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SuggestionId { get; set; }
    public TeamNameSuggestion Suggestion { get; set; } = null!;

    public Guid ParticipantId { get; set; }
    public Teilnehmer Participant { get; set; } = null!;
}
