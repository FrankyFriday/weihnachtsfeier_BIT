using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WeihnachtsfeierBit.Data;

[Table("SongWishVotes")]
public class SongWishVote
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SongWishId { get; set; }
    public SongWish SongWish { get; set; } = null!;

    public Guid ParticipantId { get; set; }
    public Teilnehmer Participant { get; set; } = null!;
}
