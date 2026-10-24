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

public abstract class SlideParameters
{
    public int SchemaVersion { get; set; } = 1;

    public string InternalName { get; set; } = string.Empty;

    public string AdminNotes { get; set; } = string.Empty;

    public bool IsDisabled { get; set; }
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
