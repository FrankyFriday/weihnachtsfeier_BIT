using System.ComponentModel.DataAnnotations.Schema;

namespace WeihnachtsfeierBit.Data;

public class GameInstanceEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid GameInstanceId { get; set; }

    public GameInstance GameInstance { get; set; } = null!;

    public Guid? SourceShowId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Mode { get; set; } = "active"; // "active" or "passive"

    public string Status { get; set; } = "active"; // "active", "scheduled", or "completed"

    public string NavigationMode { get; set; } = "host_controlled"; // "host_controlled" or "self_paced"

    public int CurrentSlideIndex { get; set; } = 0;

    public bool IsHidden { get; set; }

    public DateTime? ScheduledStartUtc { get; set; }

    public bool AutoStartOnSchedule { get; set; }

    [Column(TypeName = "jsonb")]
    public string SlidesJson { get; set; } = "[]";

    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? ClosedAtUtc { get; set; }

    public ICollection<GameInstanceEventResponse> Responses { get; set; } = new List<GameInstanceEventResponse>();
}
