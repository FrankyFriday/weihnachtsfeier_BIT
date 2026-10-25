using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WeihnachtsfeierBit.Data;

[Table("AdminChatReads")]
public class AdminChatRead
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ChatGroupId { get; set; }
    public ChatGroup ChatGroup { get; set; } = null!;

    public Guid ProjektAdminId { get; set; }
    public ProjektAdmin ProjektAdmin { get; set; } = null!;

    public DateTime LastReadAtUtc { get; set; } = DateTime.UtcNow;
}
