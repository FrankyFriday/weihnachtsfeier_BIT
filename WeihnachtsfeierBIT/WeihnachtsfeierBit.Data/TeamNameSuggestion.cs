using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WeihnachtsfeierBit.Data;

[Table("TeamNameSuggestions")]
public class TeamNameSuggestion
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TeamId { get; set; }
    public Team Team { get; set; } = null!;

    public Guid ParticipantId { get; set; }
    public Teilnehmer Participant { get; set; } = null!;

    public string SuggestedName { get; set; } = string.Empty;
    public int VoteCount { get; set; } = 1;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<TeamNameVote> Votes { get; set; } = [];
}
