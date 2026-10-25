using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WeihnachtsfeierBit.ApiService.Auth;
using WeihnachtsfeierBit.ApiService.Contracts;
using WeihnachtsfeierBit.ApiService.Services;
using WeihnachtsfeierBit.Data;

namespace WeihnachtsfeierBit.ApiService.Endpoints;

public static class ChatEndpoints
{
    public static void Map(RouteGroupBuilder reader, RouteGroupBuilder admin)
    {
        reader.MapGet("/chat/groups", GetChatGroupsAsync).WithName("GetChatGroups");
        reader.MapGet("/chat/groups/{groupId:guid}/messages", GetChatMessagesAsync).WithName("GetChatMessages");
        reader.MapPost("/chat/groups/{groupId:guid}/messages", SendChatMessageAsync).WithName("SendChatMessage");
        reader.MapPost("/chat/direct/{targetParticipantId:guid}", GetOrCreateDirectChatAsync).WithName("GetOrCreateDirectChat");
        reader.MapPost("/chat/groups", CreateCustomGroupAsync).WithName("CreateCustomGroup");
    }

    private static async Task<IResult> GetChatGroupsAsync(
        Guid projectId,
        HttpContext httpContext,
        ProjectAccessService accessService,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var token = ProjectAccessService.GetBearerToken(httpContext.Request);
        var access = await accessService.AuthenticateAsync(projectId, token, cancellationToken);
        if (access is null) return Results.Unauthorized();

        // 1. Ensure Global group exists
        var globalGroup = await dbContext.ChatGroups
            .FirstOrDefaultAsync(cg => cg.ProjektId == projectId && cg.Type == "global", cancellationToken);

        if (globalGroup is null)
        {
            globalGroup = new ChatGroup
            {
                ProjektId = projectId,
                Name = "Allgemein",
                Type = "global"
            };
            dbContext.ChatGroups.Add(globalGroup);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        // 2. If participant, ensure personal Admin Support group exists
        if (access.ParticipantId is Guid pId)
        {
            var participant = await dbContext.Teilnehmer
                .Include(t => t.Team)
                .FirstOrDefaultAsync(t => t.Id == pId, cancellationToken);

            if (participant is not null)
            {
                var supportGroup = await dbContext.ChatGroups
                    .Include(cg => cg.Members)
                    .FirstOrDefaultAsync(cg => cg.ProjektId == projectId && cg.Type == "admin_support" && cg.Members.Any(m => m.ParticipantId == pId), cancellationToken);

                if (supportGroup is null)
                {
                    supportGroup = new ChatGroup
                    {
                        ProjektId = projectId,
                        Name = "Moderation & Hilfe",
                        Type = "admin_support"
                    };
                    supportGroup.Members.Add(new ChatGroupMember { ParticipantId = pId });
                    dbContext.ChatGroups.Add(supportGroup);
                    await dbContext.SaveChangesAsync(cancellationToken);
                }

                if (participant.TeamId is Guid teamId && participant.Team is not null)
                {
                    var teamGroup = await dbContext.ChatGroups
                        .FirstOrDefaultAsync(cg => cg.ProjektId == projectId && cg.Type == "team" && cg.AssociatedTeamId == teamId, cancellationToken);

                    if (teamGroup is null)
                    {
                        teamGroup = new ChatGroup
                        {
                            ProjektId = projectId,
                            Name = $"Team {participant.Team.DisplayName}",
                            Type = "team",
                            AssociatedTeamId = teamId
                        };
                        dbContext.ChatGroups.Add(teamGroup);
                        await dbContext.SaveChangesAsync(cancellationToken);
                    }

                    bool isMember = await dbContext.ChatGroupMembers
                        .AnyAsync(m => m.ChatGroupId == teamGroup.Id && m.ParticipantId == pId, cancellationToken);

                    if (!isMember)
                    {
                        dbContext.ChatGroupMembers.Add(new ChatGroupMember
                        {
                            ChatGroupId = teamGroup.Id,
                            ParticipantId = pId
                        });
                        await dbContext.SaveChangesAsync(cancellationToken);
                    }
                }
            }
        }

        List<ChatGroup> groups;
        if (access.ParticipantId is Guid partId)
        {
            var participant = await dbContext.Teilnehmer.AsNoTracking().FirstOrDefaultAsync(t => t.Id == partId, cancellationToken);
            Guid? myTeamId = participant?.TeamId;

            groups = await dbContext.ChatGroups
                .AsNoTracking()
                .Where(cg => cg.ProjektId == projectId && (
                    cg.Type == "global" ||
                    (cg.Type == "team" && cg.AssociatedTeamId == myTeamId) ||
                    (cg.Type == "admin_support" && cg.Members.Any(m => m.ParticipantId == partId)) ||
                    (cg.Type == "direct" && cg.Members.Any(m => m.ParticipantId == partId)) ||
                    (cg.Type == "custom" && cg.Members.Any(m => m.ParticipantId == partId))
                ))
                .OrderBy(cg => cg.Type == "global" ? 0 : cg.Type == "team" ? 1 : cg.Type == "admin_support" ? 2 : 3)
                .ThenBy(cg => cg.Name)
                .ToListAsync(cancellationToken);
        }
        else
        {
            groups = await dbContext.ChatGroups
                .AsNoTracking()
                .Where(cg => cg.ProjektId == projectId)
                .OrderBy(cg => cg.Type == "global" ? 0 : cg.Type == "team" ? 1 : cg.Type == "admin_support" ? 2 : 3)
                .ThenBy(cg => cg.Name)
                .ToListAsync(cancellationToken);
        }

        var summaries = groups.Select(g => new ChatGroupSummary(
            g.Id,
            g.Name,
            g.Type,
            g.AssociatedTeamId,
            g.Members.Count)).ToArray();

        return Results.Ok(summaries);
    }

    private static async Task<IResult> GetChatMessagesAsync(
        Guid projectId,
        Guid groupId,
        HttpContext httpContext,
        ProjectAccessService accessService,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var token = ProjectAccessService.GetBearerToken(httpContext.Request);
        var access = await accessService.AuthenticateAsync(projectId, token, cancellationToken);
        if (access is null) return Results.Unauthorized();

        var group = await dbContext.ChatGroups
            .AsNoTracking()
            .Include(cg => cg.Members)
            .FirstOrDefaultAsync(cg => cg.Id == groupId && cg.ProjektId == projectId, cancellationToken);

        if (group is null) return Results.NotFound();

        if (access.ParticipantId is Guid partId)
        {
            var participant = await dbContext.Teilnehmer.AsNoTracking().FirstOrDefaultAsync(t => t.Id == partId, cancellationToken);
            bool hasAccess = group.Type == "global" ||
                (group.Type == "team" && group.AssociatedTeamId == participant?.TeamId) ||
                (group.Type == "admin_support" && group.Members.Any(m => m.ParticipantId == partId)) ||
                (group.Type == "direct" && group.Members.Any(m => m.ParticipantId == partId)) ||
                (group.Type == "custom" && group.Members.Any(m => m.ParticipantId == partId));

            if (!hasAccess) return Results.Forbid();
        }

        var messages = await dbContext.ChatMessages
            .AsNoTracking()
            .Where(m => m.ChatGroupId == groupId && m.ChatGroup.ProjektId == projectId)
            .OrderBy(m => m.SentAtUtc)
            .Take(100)
            .Select(m => new ChatMessageResponse(
                m.Id,
                m.ChatGroupId,
                m.SenderParticipantId,
                m.SenderAdminId,
                m.SenderName,
                m.TeamColor,
                m.MessageText,
                m.SenderAdminId.HasValue,
                m.SentAtUtc))
            .ToListAsync(cancellationToken);

        return Results.Ok(messages);
    }

    private static async Task<IResult> SendChatMessageAsync(
        Guid projectId,
        Guid groupId,
        SendChatMessageRequest request,
        HttpContext httpContext,
        ProjectAccessService accessService,
        WeihnachtsfeierDbContext dbContext,
        IHubContext<Hubs.GameStateHub> hubContext,
        CancellationToken cancellationToken)
    {
        var token = ProjectAccessService.GetBearerToken(httpContext.Request);
        var access = await accessService.AuthenticateAsync(projectId, token, cancellationToken);
        if (access is null) return Results.Unauthorized();

        var text = request.MessageText?.Trim();
        if (string.IsNullOrWhiteSpace(text) || text.Length > 1000)
        {
            return Results.BadRequest(new ApiError("Nachricht muss zwischen 1 und 1000 Zeichen lang sein."));
        }

        var group = await dbContext.ChatGroups
            .Include(cg => cg.Members)
            .FirstOrDefaultAsync(cg => cg.Id == groupId && cg.ProjektId == projectId, cancellationToken);

        if (group is null)
        {
            return Results.NotFound();
        }

        if (access.ParticipantId is Guid partId)
        {
            var participant = await dbContext.Teilnehmer.AsNoTracking().FirstOrDefaultAsync(t => t.Id == partId, cancellationToken);
            bool hasAccess = group.Type == "global" ||
                (group.Type == "team" && group.AssociatedTeamId == participant?.TeamId) ||
                (group.Type == "admin_support" && group.Members.Any(m => m.ParticipantId == partId)) ||
                (group.Type == "direct" && group.Members.Any(m => m.ParticipantId == partId)) ||
                (group.Type == "custom" && group.Members.Any(m => m.ParticipantId == partId));

            if (!hasAccess) return Results.Forbid();
        }

        string senderName = "Moderation";
        string? teamColor = null;
        Guid? participantId = null;
        Guid? adminId = null;

        if (access.ParticipantId is Guid pId)
        {
            participantId = pId;
            var participant = await dbContext.Teilnehmer
                .Include(t => t.Team)
                .FirstOrDefaultAsync(t => t.Id == pId, cancellationToken);

            senderName = !string.IsNullOrWhiteSpace(participant?.Name) ? participant.Name : $"Teilnehmer #{participant?.Nummer}";
            teamColor = participant?.Team?.Color;
        }
        else if (access.AdminId is Guid aId)
        {
            adminId = aId;
            var admin = await dbContext.ProjektAdmins.FindAsync([aId], cancellationToken);
            senderName = admin?.Benutzername ?? "Moderator";
        }

        var message = new ChatMessage
        {
            ChatGroupId = groupId,
            SenderParticipantId = participantId,
            SenderAdminId = adminId,
            SenderName = senderName,
            TeamColor = teamColor,
            MessageText = text,
            SentAtUtc = DateTime.UtcNow
        };

        dbContext.ChatMessages.Add(message);
        await dbContext.SaveChangesAsync(cancellationToken);

        var response = new ChatMessageResponse(
            message.Id,
            message.ChatGroupId,
            message.SenderParticipantId,
            message.SenderAdminId,
            message.SenderName,
            message.TeamColor,
            message.MessageText,
            message.SenderAdminId.HasValue,
            message.SentAtUtc);

        var groupName = Hubs.GameStateHub.GetGroupName(projectId);
        await hubContext.Clients.Group(groupName).SendAsync("ReceiveChatMessage", response, cancellationToken);

        return Results.Ok(response);
    }

    private static async Task<IResult> GetOrCreateDirectChatAsync(
        Guid projectId,
        Guid targetParticipantId,
        HttpContext httpContext,
        ProjectAccessService accessService,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var token = ProjectAccessService.GetBearerToken(httpContext.Request);
        var access = await accessService.AuthenticateAsync(projectId, token, cancellationToken);
        if (access is null || access.ParticipantId is null) return Results.Unauthorized();

        Guid p1 = access.ParticipantId.Value;
        Guid p2 = targetParticipantId;
        if (p1 == p2) return Results.BadRequest(new ApiError("Selbstgespräche nicht möglich."));

        var target = await dbContext.Teilnehmer.FirstOrDefaultAsync(t => t.Id == p2 && t.ProjektId == projectId, cancellationToken);
        if (target is null) return Results.NotFound();

        var existingGroup = await dbContext.ChatGroups
            .Include(cg => cg.Members)
            .Where(cg => cg.ProjektId == projectId && cg.Type == "direct" && cg.Members.Any(m => m.ParticipantId == p1) && cg.Members.Any(m => m.ParticipantId == p2))
            .FirstOrDefaultAsync(cancellationToken);

        if (existingGroup is not null)
        {
            return Results.Ok(new ChatGroupSummary(existingGroup.Id, existingGroup.Name, existingGroup.Type, existingGroup.AssociatedTeamId, existingGroup.Members.Count));
        }

        var sender = await dbContext.Teilnehmer.FindAsync([p1], cancellationToken);
        string groupName = $"{sender?.Name ?? $"#{sender?.Nummer}"} & {target.Name ?? $"#{target.Nummer}"}";

        var newGroup = new ChatGroup
        {
            ProjektId = projectId,
            Name = groupName,
            Type = "direct"
        };
        newGroup.Members.Add(new ChatGroupMember { ParticipantId = p1 });
        newGroup.Members.Add(new ChatGroupMember { ParticipantId = p2 });

        dbContext.ChatGroups.Add(newGroup);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(new ChatGroupSummary(newGroup.Id, newGroup.Name, newGroup.Type, newGroup.AssociatedTeamId, newGroup.Members.Count));
    }

    private static async Task<IResult> CreateCustomGroupAsync(
        Guid projectId,
        CreateCustomGroupRequest request,
        HttpContext httpContext,
        ProjectAccessService accessService,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var token = ProjectAccessService.GetBearerToken(httpContext.Request);
        var access = await accessService.AuthenticateAsync(projectId, token, cancellationToken);
        if (access is null || access.ParticipantId is null) return Results.Unauthorized();

        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 60)
        {
            return Results.BadRequest(new ApiError("Gruppenname muss zwischen 1 und 60 Zeichen lang sein."));
        }

        var group = new ChatGroup
        {
            ProjektId = projectId,
            Name = name,
            Type = "custom"
        };

        group.Members.Add(new ChatGroupMember { ParticipantId = access.ParticipantId.Value });
        if (request.ParticipantIds is not null)
        {
            foreach (var pid in request.ParticipantIds.Distinct())
            {
                if (pid != access.ParticipantId.Value)
                {
                    group.Members.Add(new ChatGroupMember { ParticipantId = pid });
                }
            }
        }

        dbContext.ChatGroups.Add(group);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(new ChatGroupSummary(group.Id, group.Name, group.Type, group.AssociatedTeamId, group.Members.Count));
    }
}
