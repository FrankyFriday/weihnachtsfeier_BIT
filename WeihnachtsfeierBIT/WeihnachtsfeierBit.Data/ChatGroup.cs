using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WeihnachtsfeierBit.Data;

[Table("ChatGroups")]
public class ChatGroup
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProjektId { get; set; }
    public Projekt Projekt { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public string Type { get; set; } = "global"; // "global", "team", "admin_support", "direct", "custom"

    public Guid? AssociatedTeamId { get; set; }
    public Team? AssociatedTeam { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<ChatGroupMember> Members { get; set; } = [];
    public List<ChatMessage> Messages { get; set; } = [];
}
