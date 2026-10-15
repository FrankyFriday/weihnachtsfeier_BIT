using Microsoft.EntityFrameworkCore;
using WeihnachtsfeierBit.ApiService.Contracts;
using WeihnachtsfeierBit.Data;

namespace WeihnachtsfeierBit.ApiService.Endpoints;

public static class ParticipantEndpoints
{
    public static void Map(RouteGroupBuilder admin)
    {
        admin.MapGet("/participants", GetParticipantsAsync).WithName("GetParticipants");
        admin.MapPost("/participants", CreateParticipantAsync).WithName("CreateParticipant");
        admin.MapPost("/participants/generate", GenerateParticipantsAsync).WithName("GenerateParticipants");
        admin.MapPost("/participants/randomize-teams", RandomizeTeamsAsync).WithName("RandomizeTeams");
        admin.MapPut("/participants/{participantId:guid}", UpdateParticipantAsync).WithName("UpdateParticipant");
        admin.MapDelete("/participants/{participantId:guid}", DeleteParticipantAsync).WithName("DeleteParticipant");
    }

    private static async Task<IResult> GetParticipantsAsync(
        Guid projectId,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var participants = await dbContext.Teilnehmer.AsNoTracking()
            .Where(item => item.ProjektId == projectId)
            .OrderBy(item => item.Nummer)
            .Select(item => new ParticipantResponse(item.Id, item.Nummer, item.Name, item.Team == null ? null : item.Team.Id))
            .ToListAsync(cancellationToken);
        return Results.Ok(participants);
    }

    private static async Task<IResult> CreateParticipantAsync(
        Guid projectId,
        CreateParticipantRequest request,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (request.Number <= 0 || request.Name?.Length > 120)
        {
            return Results.BadRequest(new ApiError("Die Teilnehmernummer muss positiv und der Name höchstens 120 Zeichen lang sein."));
        }

        var project = await dbContext.Projekte.FindAsync([projectId], cancellationToken);
        if (project is null)
        {
            return Results.NotFound();
        }

        Team? team = null;
        if (request.TeamId is Guid teamId)
        {
            team = await dbContext.Teams.FirstOrDefaultAsync(
                item => item.Id == teamId && item.ProjektId == projectId, cancellationToken);
            if (team is null)
            {
                return Results.BadRequest(new ApiError("Das Team gehört nicht zu diesem Projekt."));
            }
        }

        var participant = new Teilnehmer
        {
            ProjektId = projectId,
            Projekt = project,
            Nummer = request.Number,
            Name = request.Name?.Trim(),
            Team = team
        };
        dbContext.Teilnehmer.Add(participant);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Results.Conflict(new ApiError("Diese Teilnehmernummer wird in diesem Projekt bereits verwendet."));
        }

        return Results.Created($"/api/projects/{projectId}/participants/{participant.Id}",
            new ParticipantResponse(participant.Id, participant.Nummer, participant.Name, participant.Team?.Id));
    }

    private static async Task<IResult> UpdateParticipantAsync(
        Guid projectId,
        Guid participantId,
        UpdateParticipantRequest request,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (request.Name?.Length > 120 || request.Number is <= 0)
        {
            return Results.BadRequest(new ApiError("Name oder Teilnehmernummer ist ungültig."));
        }

        var participant = await dbContext.Teilnehmer.FirstOrDefaultAsync(
            item => item.Id == participantId && item.ProjektId == projectId, cancellationToken);
        if (participant is null)
        {
            return Results.NotFound();
        }

        Team? team = null;
        if (request.TeamId is Guid teamId)
        {
            team = await dbContext.Teams.FirstOrDefaultAsync(
                item => item.Id == teamId && item.ProjektId == projectId, cancellationToken);
            if (team is null)
            {
                return Results.BadRequest(new ApiError("Das Team gehört nicht zu diesem Projekt."));
            }
        }

        if (request.Number is int number)
        {
            var duplicateNumber = await dbContext.Teilnehmer.AnyAsync(
                item => item.ProjektId == projectId && item.Id != participantId && item.Nummer == number,
                cancellationToken);
            if (duplicateNumber)
            {
                return Results.Conflict(new ApiError("Diese Teilnehmernummer wird in diesem Projekt bereits verwendet."));
            }

            participant.Nummer = number;
        }

        participant.Name = request.Name?.Trim();
        participant.Team = team;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Ok(new ParticipantResponse(participant.Id, participant.Nummer, participant.Name, participant.Team?.Id));
    }

    private static async Task<IResult> DeleteParticipantAsync(
        Guid projectId,
        Guid participantId,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var participant = await dbContext.Teilnehmer.FirstOrDefaultAsync(
            item => item.Id == participantId && item.ProjektId == projectId, cancellationToken);
        if (participant is null)
        {
            return Results.NotFound();
        }

        dbContext.Teilnehmer.Remove(participant);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> GenerateParticipantsAsync(
        Guid projectId,
        GenerateParticipantsRequest request,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (request.Count is <= 0 or > 500)
        {
            return Results.BadRequest(new ApiError("Die Anzahl der zu generierenden Teilnehmer muss zwischen 1 und 500 liegen."));
        }

        if (request.Digits is < 2 or > 4)
        {
            return Results.BadRequest(new ApiError("Die Stellenzahl muss 2, 3 oder 4 sein."));
        }

        var project = await dbContext.Projekte.FindAsync([projectId], cancellationToken);
        if (project is null)
        {
            return Results.NotFound();
        }

        Team? team = null;
        if (request.TeamId is Guid teamId)
        {
            team = await dbContext.Teams.FirstOrDefaultAsync(
                item => item.Id == teamId && item.ProjektId == projectId, cancellationToken);
            if (team is null)
            {
                return Results.BadRequest(new ApiError("Das gewählte Team gehört nicht zu diesem Projekt."));
            }
        }

        var (minNumber, maxNumber) = request.Digits switch
        {
            2 => (10, 99),
            3 => (100, 999),
            4 => (1000, 9999),
            _ => (10, 99)
        };

        var existingNumbers = (await dbContext.Teilnehmer
            .Where(item => item.ProjektId == projectId && item.Nummer >= minNumber && item.Nummer <= maxNumber)
            .Select(item => item.Nummer)
            .ToListAsync(cancellationToken)).ToHashSet();

        var availableNumbers = new List<int>();
        for (var number = minNumber; number <= maxNumber; number++)
        {
            if (!existingNumbers.Contains(number))
            {
                availableNumbers.Add(number);
            }
        }

        if (availableNumbers.Count < request.Count)
        {
            return Results.BadRequest(new ApiError($"Es sind nicht genügend freie {request.Digits}-stellige Nummern im Projekt verfügbar."));
        }

        var createdParticipants = new List<Teilnehmer>();
        for (var i = 0; i < request.Count; i++)
        {
            var participant = new Teilnehmer
            {
                ProjektId = projectId,
                Projekt = project,
                Nummer = availableNumbers[i],
                Name = null,
                Team = team
            };
            dbContext.Teilnehmer.Add(participant);
            createdParticipants.Add(participant);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var responses = createdParticipants
            .Select(p => new ParticipantResponse(p.Id, p.Nummer, p.Name, p.Team?.Id))
            .ToArray();

        return Results.Ok(new GenerateParticipantsResponse(responses.Length, responses));
    }

    private static async Task<IResult> RandomizeTeamsAsync(
        Guid projectId,
        RandomizeTeamsRequest request,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var project = await dbContext.Projekte.FindAsync([projectId], cancellationToken);
        if (project is null)
        {
            return Results.NotFound();
        }

        var teams = await dbContext.Teams
            .Where(item => item.ProjektId == projectId)
            .OrderBy(item => item.DisplayName)
            .ToListAsync(cancellationToken);

        if (teams.Count == 0)
        {
            return Results.BadRequest(new ApiError("In diesem Projekt existieren noch keine Teams. Lege zuerst Teams an."));
        }

        var query = dbContext.Teilnehmer.Where(item => item.ProjektId == projectId);
        if (request.OnlyUnassigned)
        {
            query = query.Where(item => item.TeamId == null);
        }

        var participants = await query.ToListAsync(cancellationToken);
        if (participants.Count == 0)
        {
            return Results.Ok(new RandomizeTeamsResponse(0));
        }

        var rng = Random.Shared;
        for (var i = participants.Count - 1; i > 0; i--)
        {
            var k = rng.Next(i + 1);
            (participants[i], participants[k]) = (participants[k], participants[i]);
        }

        for (var i = 0; i < participants.Count; i++)
        {
            participants[i].Team = teams[i % teams.Count];
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Ok(new RandomizeTeamsResponse(participants.Count));
    }
}