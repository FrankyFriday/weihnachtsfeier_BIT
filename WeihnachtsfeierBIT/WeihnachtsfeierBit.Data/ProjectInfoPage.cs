using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WeihnachtsfeierBit.Data;

[Table("ProjectInfoPages")]
public class ProjectInfoPage
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProjektId { get; set; }
    public Projekt Projekt { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public string Icon { get; set; } = "info";
    public int Position { get; set; } = 0;
    public bool IsVisible { get; set; } = true;

    public string ContentMarkdown { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
