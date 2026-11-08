using System.Text.Json;

namespace WeihnachtsfeierBit.AdminClient.Models;

public sealed class RankingEntryModel
{
    public string Label { get; set; } = string.Empty;
    public int Score { get; set; }
}

public sealed class JeopardyClueModel
{
    public int Value { get; set; } = 100;
    public List<Guid> TargetSlideIds { get; set; } = [];
}

public sealed class JeopardyCategoryModel
{
    public string Name { get; set; } = string.Empty;
    public List<JeopardyClueModel> Clues { get; set; } = [];
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

    public string DisplayName => GetDisplayName();

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

    // Hitster
    public string HitsterTitle { get; set; } = "Hitster";
    public string HitsterInstructions { get; set; } = "Ordnet die Songs in der Zeitleiste nach ihrem Erscheinungsjahr ein.";
    public int HitsterCardsToWin { get; set; } = 10;

    // Text (Freitext)
    public string TextQuestion { get; set; } = string.Empty;
    public string TextPlaceholder { get; set; } = "Dein Text...";
    public int TextMaxLength { get; set; } = 200;

    // WordCloud (Wortwolke)
    public string WordCloudQuestion { get; set; } = string.Empty;
    public int WordCloudMaxWordsPerUser { get; set; } = 3;

    // Slider (Schieberegler)
    public string SliderQuestion { get; set; } = string.Empty;
    public double SliderMin { get; set; } = 0;
    public double SliderMax { get; set; } = 100;
    public double SliderStep { get; set; } = 1;
    public string SliderUnit { get; set; } = string.Empty;

    // Rating (Bewertung)
    public string RatingQuestion { get; set; } = string.Empty;
    public int RatingMaxStars { get; set; } = 5;

    // Ordering (Reihenfolge)
    public string OrderingQuestion { get; set; } = string.Empty;
    public List<string> OrderingItems { get; set; } = [];

    // Countdown (Timer)
    public string CountdownTitle { get; set; } = string.Empty;
    public int CountdownSeconds { get; set; } = 60;
    public string CountdownMessage { get; set; } = string.Empty;

    // PointAward (Punktevergabe)
    public string PointAwardTitle { get; set; } = string.Empty;
    public string PointAwardReason { get; set; } = string.Empty;
    public int PointAwardDefaultPoints { get; set; } = 10;

    public string TeamMode { get; set; } = "open_competition";

    public string JeopardyTitle { get; set; } = "Jeopardy Board";
    public List<JeopardyCategoryModel> JeopardyCategories { get; set; } = [];

    public EditableSlide(Guid id, string type, int position, JsonElement parameters)
    {
        Id = id;
        Type = type;
        Position = position;
        InternalName = ReadString(parameters, "internalName");
        AdminNotes = ReadString(parameters, "adminNotes");
        IsDisabled = ReadBool(parameters, "isDisabled");
        TeamMode = ReadString(parameters, "teamMode") is { Length: > 0 } teamMode ? teamMode : "open_competition";

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
                PollAllowChangeVote = ReadBoolOrDefault(parameters, "allowChangeVote", true);
                break;
            case "quiz":
                QuizQuestion = ReadString(parameters, "question");
                QuizAnswers = ReadStringList(parameters, "answers");
                QuizCorrectAnswerIndices = ReadIntList(parameters, "correctAnswerIndices");
                QuizTimeLimitSeconds = ReadNullableInt(parameters, "timeLimitSeconds");
                QuizPoints = ReadNullableInt(parameters, "points") ?? 100;
                QuizEnableSpeedBonus = ReadBool(parameters, "enableSpeedBonus");
                QuizExplanation = ReadString(parameters, "explanation");
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
            case "hitster":
                HitsterTitle = ReadString(parameters, "title") is { Length: > 0 } title ? title : "Hitster";
                HitsterInstructions = ReadString(parameters, "instructions") is { Length: > 0 } instructions
                    ? instructions
                    : "Ordnet die Songs in der Zeitleiste nach ihrem Erscheinungsjahr ein.";
                HitsterCardsToWin = ReadNullableInt(parameters, "cardsToWin") ?? 10;
                break;
            case "text":
                TextQuestion = ReadString(parameters, "question");
                TextPlaceholder = ReadString(parameters, "placeholder");
                TextMaxLength = ReadNullableInt(parameters, "maxLength") ?? 200;
                break;
            case "word_cloud":
                WordCloudQuestion = ReadString(parameters, "question");
                WordCloudMaxWordsPerUser = ReadNullableInt(parameters, "maxWordsPerUser") ?? 3;
                break;
            case "slider":
                SliderQuestion = ReadString(parameters, "question");
                SliderMin = ReadDouble(parameters, "min");
                SliderMax = ReadDoubleOrDefault(parameters, "max", 100);
                SliderStep = ReadDoubleOrDefault(parameters, "step", 1);
                SliderUnit = ReadString(parameters, "unit");
                break;
            case "rating":
                RatingQuestion = ReadString(parameters, "question");
                RatingMaxStars = ReadNullableInt(parameters, "maxStars") ?? 5;
                break;
            case "ordering":
                OrderingQuestion = ReadString(parameters, "question");
                OrderingItems = ReadStringList(parameters, "items");
                break;
            case "countdown":
                CountdownTitle = ReadString(parameters, "title");
                CountdownSeconds = ReadNullableInt(parameters, "seconds") ?? 60;
                CountdownMessage = ReadString(parameters, "message");
                break;
            case "point_award":
                PointAwardTitle = ReadString(parameters, "title");
                PointAwardReason = ReadString(parameters, "defaultReason");
                PointAwardDefaultPoints = ReadNullableInt(parameters, "defaultDeltaPoints") ?? 10;
                break;
            case "jeopardy":
                JeopardyTitle = ReadString(parameters, "title") is { Length: > 0 } jeopardyTitle ? jeopardyTitle : "Bünting Jeopardy";
                JeopardyCategories = ReadJeopardyCategories(parameters);
                break;
        }

        if (string.IsNullOrWhiteSpace(InternalName))
        {
            InternalName = GetFallbackName(type);
        }
    }

    public static string SlideTypeLabel(string type) => type.ToLowerInvariant() switch
    {
        "announcement" => "Ankündigung",
        "poll" => "Abstimmung",
        "quiz" => "Quiz",
        "ranking" => "Rangliste",
        "estimation" => "Schätzfrage",
        "buzzer" => "Buzzer",
        "hitster" => "Hitster",
        "text" => "Freitext",
        "word_cloud" => "Wortwolke",
        "slider" => "Schieberegler",
        "rating" => "Bewertung",
        "ordering" => "Reihenfolge",
        "countdown" => "Timer",
        "point_award" => "Punktevergabe",
        "jeopardy" => "Jeopardy",
        _ => "Slide"
    };

    public void RemovePollOption(int index)
    {
        if (index >= 0 && index < PollOptions.Count)
        {
            PollOptions.RemoveAt(index);
        }
    }

    public void ToggleQuizCorrectAnswer(int index)
    {
        if (QuizCorrectAnswerIndices.Contains(index))
        {
            QuizCorrectAnswerIndices.Remove(index);
        }
        else
        {
            QuizCorrectAnswerIndices.Add(index);
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

    public void RemoveQuizAnswer(int index)
    {
        if (index >= 0 && index < QuizAnswers.Count)
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

    public string GetDisplayName()
    {
        if (!string.IsNullOrWhiteSpace(InternalName))
        {
            return InternalName;
        }

        return Type switch
        {
            "announcement" => !string.IsNullOrWhiteSpace(AnnouncementTitle) ? AnnouncementTitle : "Ankündigung",
            "poll" => !string.IsNullOrWhiteSpace(PollQuestion) ? PollQuestion : "Abstimmung",
            "quiz" => !string.IsNullOrWhiteSpace(QuizQuestion) ? QuizQuestion : "Quiz",
            "ranking" => !string.IsNullOrWhiteSpace(RankingTitle) ? RankingTitle : "Zwischenstand",
            "estimation" => !string.IsNullOrWhiteSpace(EstimationQuestion) ? EstimationQuestion : "Schätzfrage",
            "buzzer" => !string.IsNullOrWhiteSpace(BuzzerQuestion) ? BuzzerQuestion : "Buzzer",
            "hitster" => !string.IsNullOrWhiteSpace(HitsterTitle) ? HitsterTitle : "Hitster",
            "text" => !string.IsNullOrWhiteSpace(TextQuestion) ? TextQuestion : "Freitext-Frage",
            "word_cloud" => !string.IsNullOrWhiteSpace(WordCloudQuestion) ? WordCloudQuestion : "Wortwolke",
            "slider" => !string.IsNullOrWhiteSpace(SliderQuestion) ? SliderQuestion : "Schieberegler",
            "rating" => !string.IsNullOrWhiteSpace(RatingQuestion) ? RatingQuestion : "Bewertung",
            "ordering" => !string.IsNullOrWhiteSpace(OrderingQuestion) ? OrderingQuestion : "Reihenfolge",
            "countdown" => !string.IsNullOrWhiteSpace(CountdownTitle) ? CountdownTitle : "Timer",
            "point_award" => !string.IsNullOrWhiteSpace(PointAwardTitle) ? PointAwardTitle : "Punktevergabe",
            "jeopardy" => !string.IsNullOrWhiteSpace(JeopardyTitle) ? JeopardyTitle : "Jeopardy",
            _ => "Slide"
        };
    }

    private static string GetFallbackName(string type) => type switch
    {
        "announcement" => "Neue Ankündigung",
        "poll" => "Neue Abstimmung",
        "quiz" => "Neues Quiz",
        "ranking" => "Zwischenstand",
        "estimation" => "Neue Schätzfrage",
        "buzzer" => "Neuer Buzzer",
        "hitster" => "Hitster",
        "text" => "Neue Freitext-Frage",
        "word_cloud" => "Neue Wortwolke",
        "slider" => "Neuer Schieberegler",
        "rating" => "Neue Bewertung",
        "ordering" => "Neue Sortier-Aufgabe",
        "countdown" => "Neuer Timer",
        "point_award" => "Neue Punktevergabe",
        "jeopardy" => "Neues Jeopardy",
        _ => "Neue Slide"
    };

    public (JsonElement Parameters, string? Error) BuildParameters()
    {
        ErrorMessage = null;
        Dictionary<string, object?> dict = new()
        {
            ["internalName"] = InternalName.Trim(),
            ["adminNotes"] = AdminNotes.Trim(),
            ["isDisabled"] = IsDisabled,
            ["teamMode"] = TeamMode
        };

        switch (Type)
        {
            case "announcement":
                if (string.IsNullOrWhiteSpace(AnnouncementTitle))
                {
                    ErrorMessage = "Bitte einen Titel eingeben.";
                    return (default, ErrorMessage);
                }
                dict["title"] = AnnouncementTitle.Trim();
                dict["message"] = AnnouncementMessage.Trim();
                break;

            case "poll":
                if (string.IsNullOrWhiteSpace(PollQuestion))
                {
                    ErrorMessage = "Bitte eine Frage eingeben.";
                    return (default, ErrorMessage);
                }
                var cleanOptions = PollOptions.Select(o => o.Trim()).Where(o => !string.IsNullOrWhiteSpace(o)).ToList();
                if (cleanOptions.Count < 2)
                {
                    ErrorMessage = "Bitte mindestens 2 Antworten eingeben.";
                    return (default, ErrorMessage);
                }
                dict["question"] = PollQuestion.Trim();
                dict["options"] = cleanOptions;
                dict["allowMultipleAnswers"] = PollAllowMultiple;
                dict["hideResultsUntilRevealed"] = PollHideResultsUntilRevealed;
                dict["showPercentage"] = PollShowPercentage;
                dict["allowChangeVote"] = PollAllowChangeVote;
                break;

            case "quiz":
                if (string.IsNullOrWhiteSpace(QuizQuestion))
                {
                    ErrorMessage = "Bitte eine Frage eingeben.";
                    return (default, ErrorMessage);
                }
                var cleanAnswers = QuizAnswers.Select(a => a.Trim()).Where(a => !string.IsNullOrWhiteSpace(a)).ToList();
                if (cleanAnswers.Count < 2)
                {
                    ErrorMessage = "Bitte mindestens 2 Antworten eingeben.";
                    return (default, ErrorMessage);
                }
                if (QuizCorrectAnswerIndices.Count == 0)
                {
                    ErrorMessage = "Bitte mindestens eine korrekte Antwort markieren.";
                    return (default, ErrorMessage);
                }
                dict["question"] = QuizQuestion.Trim();
                dict["answers"] = cleanAnswers;
                dict["correctAnswerIndices"] = QuizCorrectAnswerIndices;
                dict["timeLimitSeconds"] = QuizTimeLimitSeconds;
                dict["points"] = QuizPoints;
                dict["enableSpeedBonus"] = QuizEnableSpeedBonus;
                dict["explanation"] = QuizExplanation.Trim();
                break;

            case "ranking":
                dict["title"] = string.IsNullOrWhiteSpace(RankingTitle) ? "Zwischenstand" : RankingTitle.Trim();
                dict["topCount"] = RankingTopCount;
                dict["animatePodium"] = RankingAnimatePodium;
                dict["chartType"] = string.IsNullOrWhiteSpace(RankingChartType) ? "bar_chart" : RankingChartType;
                break;

            case "estimation":
                if (string.IsNullOrWhiteSpace(EstimationQuestion))
                {
                    ErrorMessage = "Bitte eine Schätzfrage eingeben.";
                    return (default, ErrorMessage);
                }
                dict["question"] = EstimationQuestion.Trim();
                dict["correctValue"] = EstimationCorrectValue;
                dict["unit"] = EstimationUnit.Trim();
                dict["points"] = EstimationPoints;
                break;

            case "buzzer":
                dict["question"] = BuzzerQuestion.Trim();
                dict["points"] = BuzzerPoints;
                dict["timeLimitSeconds"] = BuzzerTimeLimitSeconds;
                break;

            case "hitster":
                if (string.IsNullOrWhiteSpace(HitsterTitle) || HitsterTitle.Length > 120)
                {
                    ErrorMessage = "Bitte einen Hitster-Titel mit maximal 120 Zeichen eingeben.";
                    return (default, ErrorMessage);
                }
                if (HitsterInstructions.Length > 1000 || HitsterCardsToWin is < 3 or > 20)
                {
                    ErrorMessage = "Die Anleitung darf maximal 1000 Zeichen lang sein und die Siegbedingung muss zwischen 3 und 20 Karten liegen.";
                    return (default, ErrorMessage);
                }
                dict["title"] = HitsterTitle.Trim();
                dict["instructions"] = HitsterInstructions.Trim();
                dict["cardsToWin"] = HitsterCardsToWin;
                break;

            case "text":
                if (string.IsNullOrWhiteSpace(TextQuestion))
                {
                    ErrorMessage = "Bitte eine Frage / Anweisung eingeben.";
                    return (default, ErrorMessage);
                }
                dict["question"] = TextQuestion.Trim();
                dict["placeholder"] = TextPlaceholder.Trim();
                dict["maxLength"] = TextMaxLength;
                break;

            case "word_cloud":
                if (string.IsNullOrWhiteSpace(WordCloudQuestion))
                {
                    ErrorMessage = "Bitte eine Frage / Thema eingeben.";
                    return (default, ErrorMessage);
                }
                dict["question"] = WordCloudQuestion.Trim();
                dict["maxWordsPerUser"] = WordCloudMaxWordsPerUser;
                break;

            case "slider":
                if (string.IsNullOrWhiteSpace(SliderQuestion))
                {
                    ErrorMessage = "Bitte eine Frage / Skalen-Thema eingeben.";
                    return (default, ErrorMessage);
                }
                dict["question"] = SliderQuestion.Trim();
                dict["min"] = SliderMin;
                dict["max"] = SliderMax;
                dict["step"] = SliderStep;
                dict["unit"] = SliderUnit.Trim();
                break;

            case "rating":
                if (string.IsNullOrWhiteSpace(RatingQuestion))
                {
                    ErrorMessage = "Bitte eine Frage / Bewertungs-Thema eingeben.";
                    return (default, ErrorMessage);
                }
                dict["question"] = RatingQuestion.Trim();
                dict["maxStars"] = RatingMaxStars;
                break;

            case "ordering":
                if (string.IsNullOrWhiteSpace(OrderingQuestion))
                {
                    ErrorMessage = "Bitte eine Aufgabe / Frage eingeben.";
                    return (default, ErrorMessage);
                }
                var cleanItems = OrderingItems.Select(i => i.Trim()).Where(i => !string.IsNullOrWhiteSpace(i)).ToList();
                if (cleanItems.Count < 2)
                {
                    ErrorMessage = "Bitte mindestens 2 Elemente zum Sortieren eingeben.";
                    return (default, ErrorMessage);
                }
                dict["question"] = OrderingQuestion.Trim();
                dict["items"] = cleanItems;
                break;

            case "countdown":
                dict["title"] = string.IsNullOrWhiteSpace(CountdownTitle) ? "Timer" : CountdownTitle.Trim();
                dict["seconds"] = CountdownSeconds;
                dict["message"] = CountdownMessage.Trim();
                break;

            case "point_award":
                dict["title"] = string.IsNullOrWhiteSpace(PointAwardTitle) ? "Punktevergabe" : PointAwardTitle.Trim();
                dict["defaultReason"] = PointAwardReason.Trim();
                dict["defaultDeltaPoints"] = PointAwardDefaultPoints;
                break;

            case "jeopardy":
                var cleanCategories = JeopardyCategories
                    .Select(category => new JeopardyCategoryModel
                    {
                        Name = category.Name.Trim(),
                        Clues = category.Clues
                            .Select(clue => new JeopardyClueModel
                            {
                                Value = clue.Value,
                                TargetSlideIds = clue.TargetSlideIds ?? []
                            })
                            .ToList()
                    })
                    .Where(category => category.Name.Length > 0 || category.Clues.Count > 0)
                    .ToList();
                if (cleanCategories.Count == 0 || cleanCategories.Any(category => category.Name.Length == 0))
                {
                    ErrorMessage = "Bitte mindestens eine Kategorie mit Namen anlegen.";
                    return (default, ErrorMessage);
                }
                dict["title"] = string.IsNullOrWhiteSpace(JeopardyTitle) ? "Jeopardy Board" : JeopardyTitle.Trim();
                dict["categories"] = cleanCategories.Select(category => new
                {
                    name = category.Name,
                    clues = category.Clues.Select(clue => new
                    {
                        value = clue.Value,
                        targetSlideIds = clue.TargetSlideIds
                    })
                });
                break;

            default:
                ErrorMessage = $"Unbekannter Slide-Typ: {Type}";
                return (default, ErrorMessage);
        }

        return (JsonSerializer.SerializeToElement(dict, JsonOptions), null);
    }

    private static string ReadString(JsonElement element, string prop)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(prop, out var val) && val.ValueKind == JsonValueKind.String)
        {
            return val.GetString() ?? string.Empty;
        }
        return string.Empty;
    }

    private static bool ReadBool(JsonElement element, string prop)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(prop, out var val))
        {
            return val.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => false
            };
        }
        return false;
    }

    private static bool ReadBoolOrDefault(JsonElement element, string prop, bool defaultValue)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(prop, out var val))
        {
            return val.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => defaultValue
            };
        }
        return defaultValue;
    }

    private static double ReadDouble(JsonElement element, string prop)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(prop, out var val) && val.ValueKind == JsonValueKind.Number)
        {
            return val.GetDouble();
        }
        return 0;
    }

    private static double ReadDoubleOrDefault(JsonElement element, string prop, double defaultValue)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(prop, out var val) && val.ValueKind == JsonValueKind.Number)
        {
            return val.GetDouble();
        }
        return defaultValue;
    }

    private static int? ReadNullableInt(JsonElement element, string prop)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(prop, out var val) && val.ValueKind == JsonValueKind.Number)
        {
            return val.GetInt32();
        }
        return null;
    }

    private static List<string> ReadStringList(JsonElement element, string prop)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(prop, out var val) && val.ValueKind == JsonValueKind.Array)
        {
            return val.EnumerateArray()
                .Where(x => x.ValueKind == JsonValueKind.String)
                .Select(x => x.GetString() ?? string.Empty)
                .ToList();
        }
        return [];
    }

    private static List<int> ReadIntList(JsonElement element, string prop)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(prop, out var val) && val.ValueKind == JsonValueKind.Array)
        {
            return val.EnumerateArray()
                .Where(x => x.ValueKind == JsonValueKind.Number)
                .Select(x => x.GetInt32())
                .ToList();
        }
        return [];
    }

    private static List<JeopardyCategoryModel> ReadJeopardyCategories(JsonElement element)
    {
        var result = new List<JeopardyCategoryModel>();
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty("categories", out var categories)
            || categories.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var category in categories.EnumerateArray())
        {
            var model = new JeopardyCategoryModel { Name = ReadString(category, "name") };
            if (category.ValueKind == JsonValueKind.Object
                && category.TryGetProperty("clues", out var clues)
                && clues.ValueKind == JsonValueKind.Array)
            {
                foreach (var clue in clues.EnumerateArray())
                {
                    var targetIds = ReadGuidList(clue, "targetSlideIds");
                    if (targetIds.Count == 0 && clue.TryGetProperty("targetSlideId", out var singleIdProp) && singleIdProp.ValueKind == JsonValueKind.String && Guid.TryParse(singleIdProp.GetString(), out var singleGuid))
                    {
                        targetIds.Add(singleGuid);
                    }
                    model.Clues.Add(new JeopardyClueModel
                    {
                        Value = ReadNullableInt(clue, "value") ?? 100,
                        TargetSlideIds = targetIds
                    });
                }
            }

            result.Add(model);
        }

        return result;
    }

    private static List<Guid> ReadGuidList(JsonElement element, string prop)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(prop, out var val) && val.ValueKind == JsonValueKind.Array)
        {
            var list = new List<Guid>();
            foreach (var item in val.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String && Guid.TryParse(item.GetString(), out var g))
                {
                    list.Add(g);
                }
            }
            return list;
        }
        return [];
    }
}
