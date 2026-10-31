using System.Text.Json;

namespace WeihnachtsfeierBit.AdminClient.Models;

public sealed class RankingEntryModel
{
    public string Label { get; set; } = string.Empty;
    public int Score { get; set; }
}

public sealed class EditableSlide
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Guid Id { get; }
    public string Type { get; }
    public int Position { get; set; }
    public string InternalName { get; set; } = string.Empty;
    public string AdminNotes { get; set; } = string.Empty;
    public bool IsDisabled { get; set; }
    public string? ErrorMessage { get; set; }

    // Announcement
    public string AnnouncementTitle { get; set; } = string.Empty;
    public string AnnouncementMessage { get; set; } = string.Empty;

    // Poll
    public string PollQuestion { get; set; } = string.Empty;
    public List<string> PollOptions { get; set; } = [];
    public bool PollAllowMultiple { get; set; }
    public bool PollHideResultsUntilRevealed { get; set; }
    public bool PollShowPercentage { get; set; }
    public bool PollAllowChangeVote { get; set; } = true;

    // Quiz
    public string QuizQuestion { get; set; } = string.Empty;
    public List<string> QuizAnswers { get; set; } = [];
    public List<int> QuizCorrectAnswerIndices { get; set; } = [];
    public int? QuizTimeLimitSeconds { get; set; }
    public int QuizPoints { get; set; } = 100;
    public bool QuizEnableSpeedBonus { get; set; }
    public string QuizExplanation { get; set; } = string.Empty;

    // Ranking
    public string RankingTitle { get; set; } = "Zwischenstand";
    public int? RankingTopCount { get; set; }
    public bool RankingAnimatePodium { get; set; }
    public string RankingChartType { get; set; } = "bar_chart";

    // Estimation
    public string EstimationQuestion { get; set; } = string.Empty;
    public double EstimationCorrectValue { get; set; }
    public string EstimationUnit { get; set; } = string.Empty;
    public int EstimationPoints { get; set; } = 100;

    // Buzzer
    public string BuzzerQuestion { get; set; } = string.Empty;
    public int BuzzerPoints { get; set; } = 100;
    public int? BuzzerTimeLimitSeconds { get; set; }

    public EditableSlide(Guid id, string type, int position, JsonElement parameters)
    {
        Id = id;
        Type = type;
        Position = position;
        InternalName = ReadString(parameters, "internalName");
        AdminNotes = ReadString(parameters, "adminNotes");
        IsDisabled = ReadBool(parameters, "isDisabled");

        switch (type)
        {
            case "announcement":
                AnnouncementTitle = ReadString(parameters, "title");
                AnnouncementMessage = ReadString(parameters, "message");
                break;
            case "poll":
                PollQuestion = ReadString(parameters, "question");
                PollOptions = ReadStringList(parameters, "options");
                PollAllowMultiple = ReadBool(parameters, "allowMultipleAnswers");
                PollHideResultsUntilRevealed = ReadBool(parameters, "hideResultsUntilRevealed");
                PollShowPercentage = ReadBool(parameters, "showPercentage");
                PollAllowChangeVote = ReadBool(parameters, "allowChangeVote", defaultValue: true);
                if (PollOptions.Count < 2)
                {
                    while (PollOptions.Count < 2) PollOptions.Add(string.Empty);
                }
                break;
            case "quiz":
                QuizQuestion = ReadString(parameters, "question");
                QuizAnswers = ReadStringList(parameters, "answers");
                QuizCorrectAnswerIndices = ReadIntList(parameters, "correctAnswerIndices");
                QuizTimeLimitSeconds = ReadNullableInt(parameters, "timeLimitSeconds");
                QuizPoints = ReadNullableInt(parameters, "points") ?? 100;
                QuizEnableSpeedBonus = ReadBool(parameters, "enableSpeedBonus");
                QuizExplanation = ReadString(parameters, "explanation");
                if (QuizAnswers.Count < 2)
                {
                    while (QuizAnswers.Count < 2) QuizAnswers.Add(string.Empty);
                }
                break;
            case "ranking":
                RankingTitle = !string.IsNullOrWhiteSpace(ReadString(parameters, "title")) ? ReadString(parameters, "title") : "Zwischenstand";
                RankingTopCount = ReadNullableInt(parameters, "topCount");
                RankingAnimatePodium = ReadBool(parameters, "animatePodium");
                RankingChartType = !string.IsNullOrWhiteSpace(ReadString(parameters, "chartType")) ? ReadString(parameters, "chartType") : "bar_chart";
                break;
            case "estimation":
                EstimationQuestion = ReadString(parameters, "question");
                EstimationCorrectValue = ReadDouble(parameters, "correctValue");
                EstimationUnit = ReadString(parameters, "unit");
                EstimationPoints = ReadNullableInt(parameters, "points") ?? 100;
                break;
            case "buzzer":
                BuzzerQuestion = ReadString(parameters, "question");
                BuzzerPoints = ReadNullableInt(parameters, "points") ?? 100;
                BuzzerTimeLimitSeconds = ReadNullableInt(parameters, "timeLimitSeconds");
                break;
        }
    }

    public string DisplayName =>
        !string.IsNullOrWhiteSpace(InternalName)
            ? InternalName
            : Type switch
            {
                "announcement" => !string.IsNullOrWhiteSpace(AnnouncementTitle) ? AnnouncementTitle : "Ankündigung",
                "poll" => !string.IsNullOrWhiteSpace(PollQuestion) ? PollQuestion : "Abstimmung",
                "quiz" => !string.IsNullOrWhiteSpace(QuizQuestion) ? QuizQuestion : "Quiz",
                "ranking" => !string.IsNullOrWhiteSpace(RankingTitle) ? RankingTitle : "Zwischenstand",
                "estimation" => !string.IsNullOrWhiteSpace(EstimationQuestion) ? EstimationQuestion : "Schätzfrage",
                "buzzer" => !string.IsNullOrWhiteSpace(BuzzerQuestion) ? BuzzerQuestion : "Buzzer",
                _ => "Slide"
            };

    public static string SlideTypeLabel(string type) => type switch
    {
        "announcement" => "Ankündigung",
        "poll" => "Abstimmung",
        "quiz" => "Quiz",
        "ranking" => "Zwischenstand",
        "estimation" => "Schätzfrage",
        "buzzer" => "Buzzer",
        _ => type
    };

    public (JsonElement Parameters, string? Error) BuildParameters()
    {
        ErrorMessage = null;
        object payload;

        switch (Type)
        {
            case "announcement":
                if (string.IsNullOrWhiteSpace(AnnouncementTitle))
                {
                    ErrorMessage = "Bitte einen Titel für die Ankündigung eingeben.";
                    return (default, ErrorMessage);
                }
                payload = new
                {
                    internalName = InternalName.Trim(),
                    adminNotes = AdminNotes.Trim(),
                    isDisabled = IsDisabled,
                    title = AnnouncementTitle.Trim(),
                    message = AnnouncementMessage.Trim()
                };
                break;

            case "poll":
                var validPollOptions = PollOptions.Where(o => !string.IsNullOrWhiteSpace(o)).Select(o => o.Trim()).ToList();
                if (string.IsNullOrWhiteSpace(PollQuestion))
                {
                    ErrorMessage = "Bitte eine Frage für die Abstimmung eingeben.";
                    return (default, ErrorMessage);
                }
                if (validPollOptions.Count < 2)
                {
                    ErrorMessage = "Bitte mindestens 2 Antwortoptionen eingeben.";
                    return (default, ErrorMessage);
                }
                payload = new
                {
                    internalName = InternalName.Trim(),
                    adminNotes = AdminNotes.Trim(),
                    isDisabled = IsDisabled,
                    question = PollQuestion.Trim(),
                    options = validPollOptions,
                    allowMultipleAnswers = PollAllowMultiple,
                    hideResultsUntilRevealed = PollHideResultsUntilRevealed,
                    showPercentage = PollShowPercentage,
                    allowChangeVote = PollAllowChangeVote
                };
                break;

            case "quiz":
                var validQuizAnswers = QuizAnswers.Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => a.Trim()).ToList();
                if (string.IsNullOrWhiteSpace(QuizQuestion))
                {
                    ErrorMessage = "Bitte eine Quizfrage eingeben.";
                    return (default, ErrorMessage);
                }
                if (validQuizAnswers.Count < 2)
                {
                    ErrorMessage = "Bitte mindestens 2 Antwortmöglichkeiten eingeben.";
                    return (default, ErrorMessage);
                }
                if (QuizCorrectAnswerIndices.Count == 0)
                {
                    ErrorMessage = "Bitte mindestens eine Antwort als richtig markieren.";
                    return (default, ErrorMessage);
                }
                payload = new
                {
                    internalName = InternalName.Trim(),
                    adminNotes = AdminNotes.Trim(),
                    isDisabled = IsDisabled,
                    question = QuizQuestion.Trim(),
                    answers = validQuizAnswers,
                    correctAnswerIndices = QuizCorrectAnswerIndices,
                    timeLimitSeconds = QuizTimeLimitSeconds,
                    points = QuizPoints,
                    enableSpeedBonus = QuizEnableSpeedBonus,
                    explanation = QuizExplanation.Trim()
                };
                break;

            case "ranking":
                payload = new
                {
                    internalName = InternalName.Trim(),
                    adminNotes = AdminNotes.Trim(),
                    isDisabled = IsDisabled,
                    title = string.IsNullOrWhiteSpace(RankingTitle) ? "Zwischenstand" : RankingTitle.Trim(),
                    topCount = RankingTopCount,
                    animatePodium = RankingAnimatePodium,
                    chartType = string.IsNullOrWhiteSpace(RankingChartType) ? "bar_chart" : RankingChartType
                };
                break;

            case "estimation":
                if (string.IsNullOrWhiteSpace(EstimationQuestion))
                {
                    ErrorMessage = "Bitte eine Schätzfrage eingeben.";
                    return (default, ErrorMessage);
                }
                var safeCorrectValue = double.IsNaN(EstimationCorrectValue) || double.IsInfinity(EstimationCorrectValue) ? 0.0 : EstimationCorrectValue;
                payload = new
                {
                    internalName = InternalName.Trim(),
                    adminNotes = AdminNotes.Trim(),
                    isDisabled = IsDisabled,
                    question = EstimationQuestion.Trim(),
                    correctValue = safeCorrectValue,
                    unit = EstimationUnit.Trim(),
                    points = EstimationPoints
                };
                break;

            case "buzzer":
                if (string.IsNullOrWhiteSpace(BuzzerQuestion))
                {
                    ErrorMessage = "Bitte eine Buzzer-Frage eingeben.";
                    return (default, ErrorMessage);
                }
                payload = new
                {
                    internalName = InternalName.Trim(),
                    adminNotes = AdminNotes.Trim(),
                    isDisabled = IsDisabled,
                    question = BuzzerQuestion.Trim(),
                    points = BuzzerPoints,
                    timeLimitSeconds = BuzzerTimeLimitSeconds
                };
                break;

            default:
                payload = new
                {
                    internalName = InternalName.Trim(),
                    adminNotes = AdminNotes.Trim(),
                    isDisabled = IsDisabled
                };
                break;
        }

        return (JsonSerializer.SerializeToElement(payload, payload.GetType(), JsonOptions), null);
    }

    public void RemovePollOption(int index)
    {
        if (PollOptions.Count > 2)
        {
            PollOptions.RemoveAt(index);
        }
    }

    public void RemoveQuizAnswer(int index)
    {
        if (QuizAnswers.Count > 2)
        {
            QuizAnswers.RemoveAt(index);
            QuizCorrectAnswerIndices.Remove(index);
            for (int i = 0; i < QuizCorrectAnswerIndices.Count; i++)
            {
                if (QuizCorrectAnswerIndices[i] > index)
                {
                    QuizCorrectAnswerIndices[i]--;
                }
            }
        }
    }

    public void ToggleQuizCorrectAnswer(int index, bool isCorrect)
    {
        if (isCorrect)
        {
            if (!QuizCorrectAnswerIndices.Contains(index))
            {
                QuizCorrectAnswerIndices.Add(index);
            }
        }
        else
        {
            QuizCorrectAnswerIndices.Remove(index);
        }
    }

    private static string ReadString(JsonElement element, string prop) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(prop, out var val) && val.ValueKind == JsonValueKind.String
            ? val.GetString() ?? string.Empty
            : string.Empty;

    private static bool ReadBool(JsonElement element, string prop, bool defaultValue = false) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(prop, out var val) && (val.ValueKind == JsonValueKind.True || val.ValueKind == JsonValueKind.False)
            ? val.GetBoolean()
            : defaultValue;

    private static int? ReadNullableInt(JsonElement element, string prop) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(prop, out var val) && val.ValueKind == JsonValueKind.Number
            ? val.GetInt32()
            : null;

    private static double ReadDouble(JsonElement element, string prop) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(prop, out var val) && val.ValueKind == JsonValueKind.Number
            ? val.GetDouble()
            : 0.0;

    private static List<string> ReadStringList(JsonElement element, string prop)
    {
        var result = new List<string>();
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(prop, out var array) && array.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in array.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    result.Add(item.GetString() ?? string.Empty);
                }
            }
        }
        return result;
    }

    private static List<int> ReadIntList(JsonElement element, string prop)
    {
        var result = new List<int>();
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(prop, out var array) && array.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in array.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Number)
                {
                    result.Add(item.GetInt32());
                }
            }
        }
        return result;
    }
}
