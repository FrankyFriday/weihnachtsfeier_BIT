using System.Text.Json;
using Microsoft.AspNetCore.Components;
using WeihnachtsfeierBit.Framework;

namespace WeihnachtsfeierBit.ParticipantClient.Components.CanvasSlides;

public abstract class CanvasSlideRendererBase : ComponentBase
{
    [Parameter, EditorRequired]
    public CanvasEventState Event { get; set; } = null!;

    [Parameter]
    public TeamSummary[] Teams { get; set; } = [];

    [Parameter]
    public TeamPointLogDto[] PointLogs { get; set; } = [];

    protected JsonElement Parameters => Event.Parameters;
}
