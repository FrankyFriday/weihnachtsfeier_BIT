namespace WeihnachtsfeierBit.Data;

public class GameInstanceShow
{
    public Guid Id { get; set; }

    public Guid GameInstanceId { get; set; }

    public GameInstance GameInstance { get; set; } = null!;

    public Guid ShowId { get; set; }

    public Show Show { get; set; } = null!;

    public string Mode { get; set; } = "active"; // "active" or "passive"

    public int CurrentSlideIndex { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<GameInstanceEvent> Events { get; set; } = [];
}
