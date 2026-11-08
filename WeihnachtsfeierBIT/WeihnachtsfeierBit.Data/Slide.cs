using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace WeihnachtsfeierBit.Data;

public abstract class Slide
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Guid Id { get; set; }

    public Guid ShowId { get; set; }

    public Show Show { get; set; } = null!;

    public int Position { get; set; }

    public string ParametersJson { get; private set; } = "{}";

    protected T GetParameters<T>() where T : SlideParameters, new()
    {
        return JsonSerializer.Deserialize<T>(ParametersJson, JsonOptions) ?? new T();
    }

    protected void SetParameters<T>(T parameters) where T : SlideParameters
    {
        ParametersJson = JsonSerializer.Serialize(parameters, JsonOptions);
    }

    internal abstract void UpdateParametersJson();
}

public sealed class PollSlide : Slide
{
    private PollSlideParameters? _parameters;

    [NotMapped]
    public PollSlideParameters Parameters
    {
        get => _parameters ??= GetParameters<PollSlideParameters>();
        set => _parameters = value;
    }

    internal override void UpdateParametersJson() => SetParameters(Parameters);
}

public sealed class AnnouncementSlide : Slide
{
    private AnnouncementSlideParameters? _parameters;

    [NotMapped]
    public AnnouncementSlideParameters Parameters
    {
        get => _parameters ??= GetParameters<AnnouncementSlideParameters>();
        set => _parameters = value;
    }

    internal override void UpdateParametersJson() => SetParameters(Parameters);
}

public sealed class QuizSlide : Slide
{
    private QuizSlideParameters? _parameters;

    [NotMapped]
    public QuizSlideParameters Parameters
    {
        get => _parameters ??= GetParameters<QuizSlideParameters>();
        set => _parameters = value;
    }

    internal override void UpdateParametersJson() => SetParameters(Parameters);
}

public sealed class RankingSlide : Slide
{
    private RankingSlideParameters? _parameters;

    [NotMapped]
    public RankingSlideParameters Parameters
    {
        get => _parameters ??= GetParameters<RankingSlideParameters>();
        set => _parameters = value;
    }

    internal override void UpdateParametersJson() => SetParameters(Parameters);
}

public sealed class EstimationSlide : Slide
{
    private EstimationSlideParameters? _parameters;

    [NotMapped]
    public EstimationSlideParameters Parameters
    {
        get => _parameters ??= GetParameters<EstimationSlideParameters>();
        set => _parameters = value;
    }

    internal override void UpdateParametersJson() => SetParameters(Parameters);
}

public sealed class BuzzerSlide : Slide
{
    private BuzzerSlideParameters? _parameters;

    [NotMapped]
    public BuzzerSlideParameters Parameters
    {
        get => _parameters ??= GetParameters<BuzzerSlideParameters>();
        set => _parameters = value;
    }

    internal override void UpdateParametersJson() => SetParameters(Parameters);
}

public sealed class HitsterSlide : Slide
{
    private HitsterSlideParameters? _parameters;

    [NotMapped]
    public HitsterSlideParameters Parameters
    {
        get => _parameters ??= GetParameters<HitsterSlideParameters>();
        set => _parameters = value;
    }

    internal override void UpdateParametersJson() => SetParameters(Parameters);
}

public sealed class TextSlide : Slide
{
    private TextSlideParameters? _parameters;

    [NotMapped]
    public TextSlideParameters Parameters
    {
        get => _parameters ??= GetParameters<TextSlideParameters>();
        set => _parameters = value;
    }

    internal override void UpdateParametersJson() => SetParameters(Parameters);
}

public sealed class WordCloudSlide : Slide
{
    private WordCloudSlideParameters? _parameters;

    [NotMapped]
    public WordCloudSlideParameters Parameters
    {
        get => _parameters ??= GetParameters<WordCloudSlideParameters>();
        set => _parameters = value;
    }

    internal override void UpdateParametersJson() => SetParameters(Parameters);
}

public sealed class SliderSlide : Slide
{
    private SliderSlideParameters? _parameters;

    [NotMapped]
    public SliderSlideParameters Parameters
    {
        get => _parameters ??= GetParameters<SliderSlideParameters>();
        set => _parameters = value;
    }

    internal override void UpdateParametersJson() => SetParameters(Parameters);
}

public sealed class RatingSlide : Slide
{
    private RatingSlideParameters? _parameters;

    [NotMapped]
    public RatingSlideParameters Parameters
    {
        get => _parameters ??= GetParameters<RatingSlideParameters>();
        set => _parameters = value;
    }

    internal override void UpdateParametersJson() => SetParameters(Parameters);
}

public sealed class OrderingSlide : Slide
{
    private OrderingSlideParameters? _parameters;

    [NotMapped]
    public OrderingSlideParameters Parameters
    {
        get => _parameters ??= GetParameters<OrderingSlideParameters>();
        set => _parameters = value;
    }

    internal override void UpdateParametersJson() => SetParameters(Parameters);
}

public sealed class CountdownSlide : Slide
{
    private CountdownSlideParameters? _parameters;

    [NotMapped]
    public CountdownSlideParameters Parameters
    {
        get => _parameters ??= GetParameters<CountdownSlideParameters>();
        set => _parameters = value;
    }

    internal override void UpdateParametersJson() => SetParameters(Parameters);
}

public sealed class PointAwardSlide : Slide
{
    private PointAwardSlideParameters? _parameters;

    [NotMapped]
    public PointAwardSlideParameters Parameters
    {
        get => _parameters ??= GetParameters<PointAwardSlideParameters>();
        set => _parameters = value;
    }

    internal override void UpdateParametersJson() => SetParameters(Parameters);
}

public sealed class JeopardySlide : Slide
{
    private JeopardySlideParameters? _parameters;

    [NotMapped]
    public JeopardySlideParameters Parameters
    {
        get => _parameters ??= GetParameters<JeopardySlideParameters>();
        set => _parameters = value;
    }

    internal override void UpdateParametersJson() => SetParameters(Parameters);
}

public abstract class SlideParameters
{
    public int SchemaVersion { get; set; } = 1;

    public string InternalName { get; set; } = string.Empty;

    public string AdminNotes { get; set; } = string.Empty;

    public bool IsDisabled { get; set; }

    public string TeamMode { get; set; } = "open_competition"; // "open_competition", or "team_exclusive"
}

public sealed class PollSlideParameters : SlideParameters
{
    public string Question { get; set; } = string.Empty;

    public List<string> Options { get; set; } = [];

    public bool AllowMultipleAnswers { get; set; }

    public bool HideResultsUntilRevealed { get; set; }

    public bool ShowPercentage { get; set; }

    public bool AllowChangeVote { get; set; } = true;
}

public sealed class AnnouncementSlideParameters : SlideParameters
{
    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;
}

public sealed class QuizSlideParameters : SlideParameters
{
    public string Question { get; set; } = string.Empty;

    public List<string> Answers { get; set; } = [];

    public List<int> CorrectAnswerIndices { get; set; } = [];

    public int? TimeLimitSeconds { get; set; }

    public int Points { get; set; } = 100;

    public bool EnableSpeedBonus { get; set; }

    public string Explanation { get; set; } = string.Empty;
}

public sealed class RankingSlideParameters : SlideParameters
{
    public string Title { get; set; } = string.Empty;

    public int? TopCount { get; set; }

    public bool AnimatePodium { get; set; }
}

public sealed class EstimationSlideParameters : SlideParameters
{
    public string Question { get; set; } = string.Empty;

    public double CorrectValue { get; set; }

    public string Unit { get; set; } = string.Empty;

    public int Points { get; set; } = 100;
}

public sealed class BuzzerSlideParameters : SlideParameters
{
    public string Question { get; set; } = string.Empty;

    public int Points { get; set; } = 100;

    public int? TimeLimitSeconds { get; set; }
}

public sealed class HitsterSlideParameters : SlideParameters
{
    public string Title { get; set; } = "Hitster";

    public string Instructions { get; set; } = "Ordnet die Songs in der Zeitleiste nach ihrem Erscheinungsjahr ein.";

    public int CardsToWin { get; set; } = 10;
}

public sealed class TextSlideParameters : SlideParameters
{
    public string Question { get; set; } = string.Empty;
    public string Placeholder { get; set; } = string.Empty;
    public int MaxLength { get; set; } = 200;
}

public sealed class WordCloudSlideParameters : SlideParameters
{
    public string Question { get; set; } = string.Empty;
    public int MaxWordsPerUser { get; set; } = 3;
}

public sealed class SliderSlideParameters : SlideParameters
{
    public string Question { get; set; } = string.Empty;
    public double Min { get; set; } = 0;
    public double Max { get; set; } = 100;
    public double Step { get; set; } = 1;
    public string Unit { get; set; } = string.Empty;
}

public sealed class RatingSlideParameters : SlideParameters
{
    public string Question { get; set; } = string.Empty;
    public int MaxStars { get; set; } = 5;
}

public sealed class OrderingSlideParameters : SlideParameters
{
    public string Question { get; set; } = string.Empty;
    public List<string> Items { get; set; } = [];
}

public sealed class CountdownSlideParameters : SlideParameters
{
    public string Title { get; set; } = string.Empty;
    public int Seconds { get; set; } = 60;
    public string Message { get; set; } = string.Empty;
}

public sealed class PointAwardSlideParameters : SlideParameters
{
    public string Title { get; set; } = string.Empty;
    public string DefaultReason { get; set; } = string.Empty;
    public int DefaultDeltaPoints { get; set; } = 10;
}

public sealed class JeopardySlideParameters : SlideParameters
{
    public string Title { get; set; } = "Jeopardy Board";
    public List<JeopardyCategory> Categories { get; set; } = [];
}

public sealed class JeopardyCategory
{
    public string Name { get; set; } = string.Empty;
    public List<JeopardyClue> Clues { get; set; } = [];
}

public sealed class JeopardyClue
{
    public int Value { get; set; } = 100;
    public List<Guid> TargetSlideIds { get; set; } = [];
}
