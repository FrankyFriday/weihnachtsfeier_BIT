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
}