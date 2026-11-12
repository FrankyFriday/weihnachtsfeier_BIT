namespace WeihnachtsfeierBit.ApiService.Services;

public static class ParticipantTeamAssignmentService
{
    public static IReadOnlyDictionary<Guid, Guid> CreateBalancedAssignments(
        IReadOnlyCollection<Guid> participantIds,
        IReadOnlyList<Guid> teamIds,
        IReadOnlyDictionary<Guid, int> currentTeamCounts,
        Random random)
    {
        ArgumentNullException.ThrowIfNull(participantIds);
        ArgumentNullException.ThrowIfNull(teamIds);
        ArgumentNullException.ThrowIfNull(currentTeamCounts);
        ArgumentNullException.ThrowIfNull(random);

        if (teamIds.Count == 0)
        {
            throw new ArgumentException("Mindestens ein Team ist erforderlich.", nameof(teamIds));
        }

        var teamCounts = teamIds.ToDictionary(
            teamId => teamId,
            teamId => currentTeamCounts.GetValueOrDefault(teamId));
        var shuffledParticipantIds = participantIds.ToArray();

        for (var index = shuffledParticipantIds.Length - 1; index > 0; index--)
        {
            var swapIndex = random.Next(index + 1);
            (shuffledParticipantIds[index], shuffledParticipantIds[swapIndex]) =
                (shuffledParticipantIds[swapIndex], shuffledParticipantIds[index]);
        }

        var assignments = new Dictionary<Guid, Guid>(shuffledParticipantIds.Length);
        foreach (var participantId in shuffledParticipantIds)
        {
            var smallestTeamCount = teamIds.Min(teamId => teamCounts[teamId]);
            var leastLoadedTeamIds = teamIds
                .Where(teamId => teamCounts[teamId] == smallestTeamCount)
                .ToArray();
            var selectedTeamId = leastLoadedTeamIds[random.Next(leastLoadedTeamIds.Length)];

            assignments.Add(participantId, selectedTeamId);
            teamCounts[selectedTeamId]++;
        }

        return assignments;
    }
}
