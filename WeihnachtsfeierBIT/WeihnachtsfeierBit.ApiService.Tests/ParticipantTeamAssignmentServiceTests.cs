using WeihnachtsfeierBit.ApiService.Services;

namespace WeihnachtsfeierBit.ApiService.Tests;

public sealed class ParticipantTeamAssignmentServiceTests
{
    [Fact]
    public void CreateBalancedAssignments_LimitsTeamSizeDifferenceToOne()
    {
        var teamIds = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToArray();
        var participantIds = Enumerable.Range(0, 11).Select(_ => Guid.NewGuid()).ToArray();

        var assignments = ParticipantTeamAssignmentService.CreateBalancedAssignments(
            participantIds,
            teamIds,
            new Dictionary<Guid, int>(),
            new Random(17));

        var teamSizes = teamIds
            .Select(teamId => assignments.Values.Count(assignedTeamId => assignedTeamId == teamId))
            .ToArray();

        Assert.Equal(participantIds.Length, assignments.Count);
        Assert.Equal(1, teamSizes.Max() - teamSizes.Min());
    }

    [Fact]
    public void CreateBalancedAssignments_FillsLeastPopulatedTeamsWhenOnlyUnassignedParticipantsAreMoved()
    {
        var teamIds = Enumerable.Range(0, 3).Select(_ => Guid.NewGuid()).ToArray();
        var participantIds = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToArray();
        var currentTeamCounts = new Dictionary<Guid, int>
        {
            [teamIds[0]] = 4,
            [teamIds[1]] = 1,
            [teamIds[2]] = 1
        };

        var assignments = ParticipantTeamAssignmentService.CreateBalancedAssignments(
            participantIds,
            teamIds,
            currentTeamCounts,
            new Random(23));

        var finalTeamSizes = teamIds
            .Select(teamId => currentTeamCounts.GetValueOrDefault(teamId)
                + assignments.Values.Count(assignedTeamId => assignedTeamId == teamId))
            .ToArray();

        Assert.Equal(1, finalTeamSizes.Max() - finalTeamSizes.Min());
    }
}
