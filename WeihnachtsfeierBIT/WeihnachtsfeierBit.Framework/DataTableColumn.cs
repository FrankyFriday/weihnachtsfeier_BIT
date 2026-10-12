namespace WeihnachtsfeierBit.Framework;

public sealed record DataTableColumn<TItem>(string Heading, Func<TItem, string?> Value);