using System.Text.Json;
using System.Text.Json.Nodes;
using WeihnachtsfeierBit.ApiService.Services;

namespace WeihnachtsfeierBit.ApiService.Tests;

public sealed class PantomimeGameServiceTests
{
    [Fact]
    public void CreateRoundState_AssignsDifferentRandomTermsToAllTeams()
    {
        var startedAt = new DateTime(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);
        var state = PantomimeGameService.CreateRoundState(
            [Assignment(1), Assignment(2), Assignment(3)],
            termsCount: 5,
            durationSeconds: 75,
            startedAtUtc: startedAt,
            random: new Random(17));
        var teams = (JsonArray)state["teamStates"]!;
        var initialTerms = teams
            .OfType<JsonObject>()
            .Select(PantomimeGameService.GetCurrentTermIndex)
            .ToArray();

        Assert.All(initialTerms, term => Assert.NotNull(term));
        Assert.Equal(initialTerms.Length, initialTerms.Distinct().Count());
        Assert.Equal(startedAt.AddSeconds(75), PantomimeGameService.ReadDateTime(state, "deadlineUtc"));
        Assert.Equal(2, PantomimeGameService.ReadInt(state, "termsRemaining"));
    }

    [Fact]
    public void AssignNextTerm_NeverReusesTermAndLeavesTeamsWaitingWhenPoolIsEmpty()
    {
        var state = PantomimeGameService.CreateRoundState(
            [Assignment(1), Assignment(2)],
            termsCount: 3,
            durationSeconds: 60,
            startedAtUtc: DateTime.UtcNow,
            random: new Random(5));
        var teams = ((JsonArray)state["teamStates"]!).OfType<JsonObject>().ToArray();
        var firstTeamTerms = new List<int> { PantomimeGameService.GetCurrentTermIndex(teams[0])!.Value };
        var secondInitialTerm = PantomimeGameService.GetCurrentTermIndex(teams[1])!.Value;

        Assert.True(PantomimeGameService.AssignNextTerm(state, teams[0]));
        firstTeamTerms.Add(PantomimeGameService.GetCurrentTermIndex(teams[0])!.Value);
        Assert.False(PantomimeGameService.AssignNextTerm(state, teams[1]));

        Assert.Equal(3, firstTeamTerms.Append(secondInitialTerm).Distinct().Count());
        Assert.Null(PantomimeGameService.GetCurrentTermIndex(teams[1]));
        Assert.False((bool)teams[1]["hasCurrentTerm"]!);
        Assert.Equal(0, PantomimeGameService.ReadInt(state, "termsRemaining"));
    }

    [Fact]
    public void PublicParameters_HideTermsAndPrivateAllocationIndexes()
    {
        var state = PantomimeGameService.CreateRoundState(
            [Assignment(1)],
            termsCount: 3,
            durationSeconds: 60,
            startedAtUtc: DateTime.UtcNow,
            random: new Random(1));
        using var stateDocument = JsonDocument.Parse(state.ToJsonString());
        var parameters = JsonSerializer.SerializeToElement(new
        {
            category = "Geheim",
            terms = new[] { "Geheimer Begriff" },
            roundDurationSeconds = 60,
            state = stateDocument.RootElement
        });

        var publicParameters = PantomimeGameService.PublicParameters(parameters);
        var publicState = publicParameters.GetProperty("state");
        var publicTeam = publicState.GetProperty("teamStates")[0];

        Assert.False(publicParameters.TryGetProperty("terms", out _));
        Assert.False(publicState.TryGetProperty("termOrder", out _));
        Assert.False(publicTeam.TryGetProperty("currentTermIndex", out _));
        Assert.False(publicTeam.TryGetProperty("hasCurrentTerm", out _));
        Assert.True(publicTeam.TryGetProperty("performerId", out _));
        Assert.True(publicTeam.TryGetProperty("correctCount", out _));
    }

    [Fact]
    public void ExpireIfNeeded_EndsEveryTeamAndRemovesTheirTerms()
    {
        var now = DateTime.UtcNow;
        var state = PantomimeGameService.CreateRoundState(
            [Assignment(1), Assignment(2)],
            termsCount: 4,
            durationSeconds: 30,
            startedAtUtc: now,
            random: new Random(3));
        var teams = ((JsonArray)state["teamStates"]!).OfType<JsonObject>().ToArray();

        Assert.True(PantomimeGameService.ExpireIfNeeded(state, now.AddSeconds(31)));

        Assert.False(PantomimeGameService.ReadBool(state, "isRunning"));
        Assert.True(PantomimeGameService.ReadBool(state, "isEnded"));
        Assert.Equal(0, PantomimeGameService.RemainingSeconds(state, now.AddSeconds(31)));
        Assert.All(teams, team => Assert.Null(PantomimeGameService.GetCurrentTermIndex(team)));
    }

    [Fact]
    public void GetDistinctTerms_RemovesDuplicateTermsIgnoringCaseAndWhitespace()
    {
        var terms = new JsonArray("Hund", " hund ", "Katze", "Katze");

        var result = PantomimeGameService.GetDistinctTerms(terms);

        Assert.Equal(["Hund", "Katze"], result);
    }

    private static PantomimeTeamAssignment Assignment(int number)
    {
        var teamId = Guid.NewGuid();
        return new PantomimeTeamAssignment(
            teamId,
            $"Team {number}",
            Guid.NewGuid(),
            $"Person {number}");
    }
}
