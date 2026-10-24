namespace WeihnachtsfeierBit.Data;

public class GameInstanceEvent
{
    public Guid Id { get; set; }

    public Guid GameInstanceId { get; set; }

    public GameInstance GameInstance { get; set; } = null!;

    public Guid? GameInstanceShowId { get; set; }

    public GameInstanceShow? GameInstanceShow { get; set; }

    public Guid? SourceSlideId { get; set; }

    public string SlideType { get; set; } = "announcement";

    public string Title { get; set; } = string.Empty;

    public string Mode { get; set; } = "active"; // "active" or "passive"

    public string Status { get; set; } = "active"; // "active" or "completed"

    public string ParametersJson { get; set; } = "{}";

    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? ClosedAtUtc { get; set; }

    public ICollection<GameInstanceEventResponse> Responses { get; set; } = new List<GameInstanceEventResponse>();
}
