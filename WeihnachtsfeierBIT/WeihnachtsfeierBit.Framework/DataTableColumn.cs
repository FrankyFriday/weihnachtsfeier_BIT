using Microsoft.AspNetCore.Components;

namespace WeihnachtsfeierBit.Framework;

public sealed class DataTableColumn<TItem>
{
    public string Heading { get; }
    public Func<TItem, object?> Value { get; }

    public DataTableColumn(string heading, Func<TItem, object?> value)
    {
        Heading = heading;
        Value = value;
    }

    public DataTableColumn(string heading, Func<TItem, RenderFragment> render)
    {
        Heading = heading;
        Value = item => render(item);
    }
}
