using WeihnachtsfeierBit.Framework;

namespace WeihnachtsfeierBit.AdminClient.Models;

public record TabItem(string Id, string Title, string? StatusIcon, bool IsBeamerActive, EventState Event);
