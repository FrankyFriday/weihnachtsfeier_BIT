using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WeihnachtsfeierBit.Data;

[Table("SongWishes")]
public class SongWish
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProjektId { get; set; }
    public Projekt Projekt { get; set; } = null!;

    public Guid ParticipantId { get; set; }
    public Teilnehmer Participant { get; set; } = null!;

    public string SongTitle { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public int VoteCount { get; set; } = 1;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<SongWishVote> Votes { get; set; } = [];
}
