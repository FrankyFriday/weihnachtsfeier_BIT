namespace WeihnachtsfeierBit.Data;

public sealed class TestEntry
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}