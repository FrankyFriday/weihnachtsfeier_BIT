using System.Text.Json;
using System.Text.Json.Nodes;
using WeihnachtsfeierBit.Data;
using WeihnachtsfeierBit.ApiService.Services;
using WeihnachtsfeierBit.Framework;

namespace WeihnachtsfeierBit.ApiService.Tests;

public sealed class QuestionWorkflowServiceTests
{
    [Fact]
    public void TickActiveSlide_AdvancesQuestionAndAnswerTimers()
    {
        var now = DateTimeOffset.UtcNow;
        var slide = JsonNode.Parse(
            """{"type":"quiz","parameters":{"questionTimeLimitSeconds":10,"timeLimitSeconds":20}}""")!.AsObject();

        Assert.True(QuestionWorkflowService.TickActiveSlide(slide, now));
        Assert.Equal(QuestionWorkflowService.QuestionPhase, QuestionWorkflowService.GetPhase(slide));
        Assert.Equal(now.AddSeconds(10), QuestionWorkflowService.GetTimerEnd(slide));

        Assert.False(QuestionWorkflowService.TickActiveSlide(slide, now.AddSeconds(9)));
        Assert.True(QuestionWorkflowService.TickActiveSlide(slide, now.AddSeconds(10)));
        Assert.Equal(QuestionWorkflowService.AnswerPhase, QuestionWorkflowService.GetPhase(slide));
        Assert.Equal(now.AddSeconds(30), QuestionWorkflowService.GetTimerEnd(slide));

        Assert.True(QuestionWorkflowService.TickActiveSlide(slide, now.AddSeconds(30)));
        Assert.Equal(QuestionWorkflowService.LockedPhase, QuestionWorkflowService.GetPhase(slide));
        Assert.Null(QuestionWorkflowService.GetTimerEnd(slide));
    }

    [Fact]
    public void GetActiveTeamId_FindsSelectionEvenWhenAnotherSlideIsCurrent()
    {
        var activeTeamId = Guid.NewGuid();
        var gameEvent = new GameInstanceEvent
        {
            CurrentSlideIndex = 0,
            SlidesJson = JsonSerializer.Serialize(new object[]
            {
                new { type = "announcement", parameters = new { message = "next" } },
                new { type = "jeopardy", parameters = new { state = new { selectedTeamId = activeTeamId } } }
            })
        };

        Assert.Equal(activeTeamId, QuestionWorkflowService.GetActiveTeamId(gameEvent));
    }

    [Fact]
    public void StartPhase_PersistsTimerDurationForProgressDisplay()
    {
        var now = DateTimeOffset.UtcNow;
        var slide = CreateWorkflowSlide("quiz", """{"phase":"answers"}""");

        QuestionWorkflowService.StartPhase(slide, QuestionWorkflowService.AnswerPhase, 30, now);

        var workflow = QuestionWorkflowService.GetWorkflowState(slide);
        Assert.Equal(30, workflow["timerDurationSeconds"]!.GetValue<int>());

        workflow["timerPaused"] = true;
        workflow["timerEndsAtUtc"] = null;
        workflow["remainingSeconds"] = 12;

        Assert.Equal(30, workflow["timerDurationSeconds"]!.GetValue<int>());
    }

    [Fact]
    public void DisableTimer_LeavesTheCurrentPhaseWithoutRestartingConfiguredTimer()
    {
        var now = DateTimeOffset.UtcNow;
        var slide = JsonNode.Parse(
            """{"type":"quiz","parameters":{"questionTimeLimitSeconds":1,"timeLimitSeconds":20}}""")!.AsObject();

        Assert.True(QuestionWorkflowService.TickActiveSlide(slide, now));
        Assert.True(QuestionWorkflowService.TickActiveSlide(slide, now.AddSeconds(1)));
        Assert.Equal(QuestionWorkflowService.AnswerPhase, QuestionWorkflowService.GetPhase(slide));
        Assert.True(QuestionWorkflowService.DisableTimer(slide, now.AddSeconds(2)));
        Assert.Null(QuestionWorkflowService.GetTimerEnd(slide));
        Assert.False(QuestionWorkflowService.TickActiveSlide(slide, now.AddSeconds(3)));
        Assert.Equal(QuestionWorkflowService.AnswerPhase, QuestionWorkflowService.GetPhase(slide));
    }

    [Theory]
    [InlineData("quiz", "correctAnswerIndices")]
    [InlineData("estimation", "correctValue")]
    public void FilterProjectState_HidesSolutionUntilResults(string slideType, string solutionProperty)
    {
        var slide = CreateSlide(slideType, """{"phase":"answers","officialAnswerIndex":0,"officialEstimationValue":42}""");
        var state = CreateProjectState(slide, activeTeamId: null, responses: []);

        var publicState = QuestionWorkflowService.FilterProjectState(state, recipientTeamId: null);
        var publicParameters = publicState.ActiveEvent!.Slides[0].Parameters;
        Assert.False(publicParameters.TryGetProperty(solutionProperty, out _));
        Assert.False(publicParameters.TryGetProperty("explanation", out _));
        var publicWorkflow = publicParameters.GetProperty("state").GetProperty("questionWorkflow");
        Assert.False(publicWorkflow.TryGetProperty("officialAnswerIndex", out _));
        Assert.False(publicWorkflow.TryGetProperty("officialEstimationValue", out _));
        Assert.False(publicWorkflow.TryGetProperty("scoreAwards", out _));

        var resultsSlide = CreateSlide(slideType, """{"phase":"results","officialAnswerIndex":0,"officialEstimationValue":42}""");
        var resultsState = CreateProjectState(resultsSlide, activeTeamId: null, responses: []);
        var resultsParameters = QuestionWorkflowService.FilterProjectState(resultsState, recipientTeamId: null)
            .ActiveEvent!.Slides[0].Parameters;
        Assert.True(resultsParameters.TryGetProperty(solutionProperty, out _));
        Assert.True(resultsParameters.TryGetProperty("explanation", out _));
        var resultsWorkflow = resultsParameters.GetProperty("state").GetProperty("questionWorkflow");
        Assert.True(resultsWorkflow.TryGetProperty("officialAnswerIndex", out _));
        Assert.True(resultsWorkflow.TryGetProperty("officialEstimationValue", out _));
    }

    [Fact]
    public void FilterProjectState_OnlySharesTeamExclusiveResponsesWithActiveTeam()
    {
        var activeTeamId = Guid.NewGuid();
        var otherTeamId = Guid.NewGuid();
        var slide = CreateSlide("quiz", """{"phase":"answers"}""", teamMode: "team_exclusive");
        var responses = new[]
        {
            CreateResponse(slide.SlideId, activeTeamId),
            CreateResponse(slide.SlideId, otherTeamId)
        };
        var state = CreateProjectState(slide, activeTeamId, responses);

        var activeTeamState = QuestionWorkflowService.FilterProjectState(state, activeTeamId);
        Assert.Collection(activeTeamState.ActiveEvent!.Responses,
            response => Assert.Equal(activeTeamId, response.TeamId));

        var otherTeamState = QuestionWorkflowService.FilterProjectState(state, otherTeamId);
        Assert.Empty(otherTeamState.ActiveEvent!.Responses);
    }

    [Fact]
    public void FilterProjectState_RemovesModeratorNotesFromEveryPublicSlide()
    {
        var slide = new EventSlideState(
            Guid.NewGuid(),
            "announcement",
            0,
            JsonSerializer.SerializeToElement(new
            {
                adminNotes = "private moderator notes",
                message = "public announcement"
            }));
        var state = CreateProjectState(slide, activeTeamId: null, responses: []);

        var publicParameters = QuestionWorkflowService.FilterProjectState(state, recipientTeamId: null)
            .ActiveEvent!.Slides[0].Parameters;

        Assert.False(publicParameters.TryGetProperty("adminNotes", out _));
        Assert.Equal("public announcement", publicParameters.GetProperty("message").GetString());
    }

    [Fact]
    public void CanEditTeamResponse_QuizIsLimitedToOwnVote()
    {
        var participantId = Guid.NewGuid();

        Assert.True(QuestionWorkflowService.CanEditTeamResponse("quiz", participantId, participantId));
        Assert.False(QuestionWorkflowService.CanEditTeamResponse("quiz", participantId, Guid.NewGuid()));
        Assert.True(QuestionWorkflowService.CanEditTeamResponse("estimation", participantId, Guid.NewGuid()));
    }

    [Fact]
    public void HasOfficialTeamResult_RequiresExplicitTeamAnswerButAcceptsZeroEstimate()
    {
        var workflow = new JsonObject();
        Assert.False(QuestionWorkflowService.HasOfficialTeamResult("quiz", workflow));
        Assert.False(QuestionWorkflowService.HasOfficialTeamResult("estimation", workflow));

        workflow["officialAnswerIndex"] = 0;
        workflow["officialEstimationValue"] = 0d;
        Assert.True(QuestionWorkflowService.HasOfficialTeamResult("quiz", workflow));
        Assert.True(QuestionWorkflowService.HasOfficialTeamResult("estimation", workflow));
    }

    [Fact]
    public void LockAnswers_StopsAnswerTimerAndRejectsQuestionPhase()
    {
        var now = DateTimeOffset.UtcNow;
        var question = CreateWorkflowSlide("quiz", """{"phase":"question"}""");
        var answers = CreateWorkflowSlide("quiz", """{"phase":"answers","timerPaused":true,"remainingSeconds":12}""");
        var results = CreateWorkflowSlide("quiz", """{"phase":"results"}""");

        Assert.False(QuestionWorkflowService.LockAnswers(question, now));
        Assert.True(QuestionWorkflowService.LockAnswers(answers, now));
        Assert.True(QuestionWorkflowService.LockAnswers(results, now));
        Assert.Equal(QuestionWorkflowService.LockedPhase, QuestionWorkflowService.GetPhase(answers));
        Assert.Equal(QuestionWorkflowService.LockedPhase, QuestionWorkflowService.GetPhase(results));
        Assert.Null(QuestionWorkflowService.GetTimerEnd(answers));

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(answers));
        var workflow = document.RootElement.GetProperty("parameters")
            .GetProperty("state").GetProperty("questionWorkflow");
        Assert.False(workflow.GetProperty("timerPaused").GetBoolean());
        Assert.Equal(0, workflow.GetProperty("remainingSeconds").GetInt32());
    }

    [Fact]
    public void OfficialTeamResult_CanOnlyBeSetAfterAnswersAreLocked()
    {
        var answers = CreateWorkflowSlide("quiz", """{"phase":"answers"}""", teamMode: "team_exclusive");
        var locked = CreateWorkflowSlide("quiz", """{"phase":"locked"}""", teamMode: "team_exclusive");
        var openAnswers = CreateWorkflowSlide("quiz", """{"phase":"answers"}""");

        Assert.False(QuestionWorkflowService.CanSetOfficialTeamResult(answers));
        Assert.True(QuestionWorkflowService.CanSetOfficialTeamResult(locked));
        Assert.True(QuestionWorkflowService.CanSetOfficialTeamResult(openAnswers));
    }

    [Fact]
    public void Results_CanOnlyBeRevealedFromLockedPhaseAndRequireTeamResult()
    {
        var answers = CreateWorkflowSlide("quiz", """{"phase":"answers","officialAnswerIndex":0}""");
        var lockedWithoutAnswer = CreateWorkflowSlide("quiz", """{"phase":"locked"}""", teamMode: "team_exclusive");
        var lockedWithAnswer = CreateWorkflowSlide("quiz", """{"phase":"locked","officialAnswerIndex":0}""", teamMode: "team_exclusive");
        var lockedEstimateWithoutValue = CreateWorkflowSlide("estimation", """{"phase":"locked"}""", teamMode: "team_exclusive");
        var lockedEstimateWithValue = CreateWorkflowSlide("estimation", """{"phase":"locked","officialEstimationValue":0}""", teamMode: "team_exclusive");
        var lockedOpenQuiz = CreateWorkflowSlide("quiz", """{"phase":"locked"}""");
        var alreadyResults = CreateWorkflowSlide("quiz", """{"phase":"results"}""");

        Assert.False(QuestionWorkflowService.CanRevealResults(answers));
        Assert.False(QuestionWorkflowService.CanRevealResults(lockedWithoutAnswer));
        Assert.True(QuestionWorkflowService.CanRevealResults(lockedWithAnswer));
        Assert.False(QuestionWorkflowService.CanRevealResults(lockedEstimateWithoutValue));
        Assert.True(QuestionWorkflowService.CanRevealResults(lockedEstimateWithValue));
        Assert.True(QuestionWorkflowService.CanRevealResults(lockedOpenQuiz));
        Assert.False(QuestionWorkflowService.CanRevealResults(alreadyResults));
    }

    private static EventSlideState CreateSlide(string type, string workflowState, string teamMode = "open")
    {
        var parameters = new JsonObject
        {
            ["teamMode"] = teamMode,
            ["adminNotes"] = "private moderator notes",
            ["explanation"] = "Answer explanation",
            ["state"] = new JsonObject
            {
                ["questionWorkflow"] = JsonNode.Parse(workflowState)
            }
        };
        if (type == "quiz")
        {
            parameters["correctAnswerIndices"] = new JsonArray(JsonValue.Create(0));
        }
        else
        {
            parameters["correctValue"] = 42;
        }

        return new EventSlideState(
            Guid.NewGuid(),
            type,
            0,
            JsonSerializer.SerializeToElement(parameters));
    }

    private static JsonObject CreateWorkflowSlide(string type, string workflowState, string teamMode = "open") =>
        new()
        {
            ["type"] = type,
            ["parameters"] = new JsonObject
            {
                ["teamMode"] = teamMode,
                ["state"] = new JsonObject
                {
                    ["questionWorkflow"] = JsonNode.Parse(workflowState)
                }
            }
        };

    private static EventResponseState CreateResponse(Guid slideId, Guid teamId)
    {
        using var responseData = JsonDocument.Parse("""{"answerIndex":0}""");
        return new EventResponseState(
            Guid.NewGuid(),
            slideId,
            Guid.NewGuid(),
            1,
            "Participant",
            "Team",
            "#ffffff",
            responseData.RootElement.Clone(),
            DateTime.UtcNow,
            teamId);
    }

    private static ProjectGameState CreateProjectState(
        EventSlideState slide,
        Guid? activeTeamId,
        EventResponseState[] responses)
    {
        var gameEvent = new EventState(
            Guid.NewGuid(),
            null,
            "Question",
            null,
            "active",
            "active",
            "host_controlled",
            0,
            false,
            null,
            false,
            [slide],
            responses,
            DateTime.UtcNow,
            ActiveTeamId: activeTeamId);

        return new ProjectGameState(
            Guid.NewGuid(),
            "Project",
            [],
            gameEvent,
            [],
            [],
            null,
            DateTime.UtcNow,
            ActiveTeamId: activeTeamId);
    }
}
