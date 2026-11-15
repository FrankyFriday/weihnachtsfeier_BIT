using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WeihnachtsfeierBit.ApiService.Auth;
using WeihnachtsfeierBit.ApiService.Contracts;
using WeihnachtsfeierBit.ApiService.Hubs;
using WeihnachtsfeierBit.ApiService.Services;
using WeihnachtsfeierBit.Data;

namespace WeihnachtsfeierBit.ApiService.Endpoints;

public static class ChatEndpoints
{
    public static void Map(RouteGroupBuilder reader, RouteGroupBuilder admin)
    {
        reader.MapGet("/chat/groups", GetChatGroupsAsync).WithName("GetChatGroups");
        reader.MapGet("/chat/participants", GetChatParticipantsAndAdminsAsync).WithName("GetChatParticipantsAndAdmins");
        reader.MapGet("/chat/groups/{groupId:guid}/messages", GetChatMessagesAsync).WithName("GetChatMessages");
        reader.MapPost("/chat/groups/{groupId:guid}/messages", SendChatMessageAsync).WithName("SendChatMessage");
        reader.MapPost("/chat/groups/{groupId:guid}/read", MarkGroupAsReadAsync).WithName("MarkGroupAsRead");
        reader.MapPost("/chat/direct/{targetParticipantId:guid}", GetOrCreateDirectChatAsync).WithName("GetOrCreateDirectChat");
        reader.MapPost("/chat/groups", CreateCustomGroupAsync).WithName("CreateCustomGroup");
        admin.MapDelete("/chat/messages/{messageId:guid}", DeleteChatMessageAsync).WithName("DeleteChatMessage");
        admin.MapDelete("/chat/groups/{groupId:guid}", DeleteChatGroupAsync).WithName("DeleteChatGroup");
        admin.MapPut("/chat/groups/{groupId:guid}", RenameChatGroupAsync).WithName("RenameChatGroup");
    }

    private static async Task<IResult> GetChatGroupsAsync(
        Guid projectId,
        HttpContext httpContext,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var access = httpContext.Items[RequireProjectAccessFilter.AccessItemKey] as ProjectAccess;
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
                else if (!supportGroup.Members.Any(m => m.ParticipantId == pId))
                {
                    supportGroup.Members.Add(new ChatGroupMember { ParticipantId = pId });
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
        Guid? partId = access.ParticipantId;
        Guid? adminId = access.AdminId;

        if (partId is Guid pid)
        {
            var participant = await dbContext.Teilnehmer.AsNoTracking().FirstOrDefaultAsync(t => t.Id == pid, cancellationToken);
            Guid? myTeamId = participant?.TeamId;

            groups = await dbContext.ChatGroups
                .AsNoTracking()
                .Include(cg => cg.Messages)
                .Include(cg => cg.Members)
                .Where(cg => cg.ProjektId == projectId && (
                    cg.Type == "global" ||
                    (cg.Type == "team" && cg.AssociatedTeamId == myTeamId) ||
                    (cg.Type == "admin_support" && cg.Members.Any(m => m.ParticipantId == pid)) ||
                    (cg.Type == "direct" && cg.Members.Any(m => m.ParticipantId == pid)) ||
                    (cg.Type == "custom" && cg.Members.Any(m => m.ParticipantId == pid))
                ))
                .OrderBy(cg => cg.Type == "global" ? 0 : cg.Type == "team" ? 1 : cg.Type == "admin_support" ? 2 : 3)
                .ThenBy(cg => cg.Name)
                .ToListAsync(cancellationToken);
        }
        else
        {
            groups = await dbContext.ChatGroups
                .AsNoTracking()
                .Include(cg => cg.Messages)
                .Include(cg => cg.Members)
                .Where(cg => cg.ProjektId == projectId)
                .OrderBy(cg => cg.Type == "global" ? 0 : cg.Type == "team" ? 1 : cg.Type == "admin_support" ? 2 : 3)
                .ThenBy(cg => cg.Name)
                .ToListAsync(cancellationToken);
        }

        var summaries = new List<ChatGroupSummary>();
        foreach (var g in groups)
        {
            var lastMsg = g.Messages.OrderByDescending(m => m.SentAtUtc).FirstOrDefault();

            ChatUserRead? userRead = null;
            if (partId is Guid pGuid)
            {
                userRead = await dbContext.ChatUserReads
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r => r.ChatGroupId == g.Id && r.ParticipantId == pGuid, cancellationToken);
            }
            else if (adminId is Guid aGuid)
            {
                userRead = await dbContext.ChatUserReads
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r => r.ChatGroupId == g.Id && r.ProjektAdminId == aGuid, cancellationToken);
            }

            DateTime? lastRead = userRead?.LastReadAtUtc;
            int unreadCount = lastRead.HasValue ? g.Messages.Count(m => m.SentAtUtc > lastRead.Value) : g.Messages.Count;

            string groupName = g.Name;
            if (adminId.HasValue && g.Type == "admin_support")
            {
                var memberParticipantId = g.Members.FirstOrDefault()?.ParticipantId;
                if (memberParticipantId is Guid partIdVal && partIdVal != Guid.Empty)
                {
                    var participant = await dbContext.Teilnehmer
                        .AsNoTracking()
                        .FirstOrDefaultAsync(t => t.Id == partIdVal, cancellationToken);

                    if (participant is not null)
                    {
                        string pName = string.IsNullOrWhiteSpace(participant.Name) ? $"Teilnehmer #{participant.Nummer}" : $"{participant.Name} (#{participant.Nummer})";
                        groupName = $"Support: {pName}";
                    }
                }
            }

            summaries.Add(new ChatGroupSummary(
                g.Id,
                groupName,
                g.Type,
                g.AssociatedTeamId,
                g.Members.Count,
                lastMsg?.MessageText,
                lastMsg?.SentAtUtc,
                unreadCount,
                lastMsg?.SenderName));
        }

        return Results.Ok(summaries);
    }

    private static async Task<IResult> GetChatParticipantsAndAdminsAsync(
        Guid projectId,
        HttpContext httpContext,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var access = httpContext.Items[RequireProjectAccessFilter.AccessItemKey] as ProjectAccess;
        if (access is null) return Results.Unauthorized();

        var participants = await dbContext.Teilnehmer
            .AsNoTracking()
            .Where(t => t.ProjektId == projectId && (access.ParticipantId == null || t.Id != access.ParticipantId.Value))
            .OrderBy(t => t.Nummer)
            .Select(t => new ChatContactDto(t.Id, $"Teilnehmer #{t.Nummer}" + (string.IsNullOrEmpty(t.Name) ? "" : $" ({t.Name})"), "participant"))
            .ToListAsync(cancellationToken);

        var admins = await dbContext.ProjektAdminProjekte
            .AsNoTracking()
            .Where(map => map.ProjektId == projectId)
            .Select(map => new ChatContactDto(map.ProjektAdminId, $"{map.ProjektAdmin.Benutzername} (Moderator)", "admin"))
            .ToListAsync(cancellationToken);

        var list = new List<ChatContactDto>();
        list.AddRange(participants);
        list.AddRange(admins);

        return Results.Ok(list.ToArray());
    }

    private static async Task<IResult> GetChatMessagesAsync(
        Guid projectId,
        Guid groupId,
        HttpContext httpContext,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var access = httpContext.Items[RequireProjectAccessFilter.AccessItemKey] as ProjectAccess;
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

    private static async Task<IResult> MarkGroupAsReadAsync(
        Guid projectId,
        Guid groupId,
        HttpContext httpContext,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var access = httpContext.Items[RequireProjectAccessFilter.AccessItemKey] as ProjectAccess;
        if (access is null) return Results.Unauthorized();

        Guid? partId = access.ParticipantId;
        Guid? adminId = access.AdminId;

        var latestMsgTime = await dbContext.ChatMessages
            .Where(m => m.ChatGroupId == groupId && m.ChatGroup.ProjektId == projectId)
            .MaxAsync(m => (DateTime?)m.SentAtUtc, cancellationToken) ?? DateTime.UtcNow;

        if (partId is Guid pGuid)
        {
            var existingRead = await dbContext.ChatUserReads
                .FirstOrDefaultAsync(r => r.ChatGroupId == groupId && r.ParticipantId == pGuid, cancellationToken);

            if (existingRead is not null)
            {
                existingRead.LastReadAtUtc = latestMsgTime;
            }
            else
            {
                dbContext.ChatUserReads.Add(new ChatUserRead
                {
                    ChatGroupId = groupId,
                    ParticipantId = pGuid,
                    ProjektAdminId = null,
                    LastReadAtUtc = latestMsgTime
                });
            }
        }
        else if (adminId is Guid aGuid)
        {
            var existingRead = await dbContext.ChatUserReads
                .FirstOrDefaultAsync(r => r.ChatGroupId == groupId && r.ProjektAdminId == aGuid, cancellationToken);

            if (existingRead is not null)
            {
                existingRead.LastReadAtUtc = latestMsgTime;
            }
            else
            {
                dbContext.ChatUserReads.Add(new ChatUserRead
                {
                    ChatGroupId = groupId,
                    ParticipantId = null,
                    ProjektAdminId = aGuid,
                    LastReadAtUtc = latestMsgTime
                });
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Ok();
    }

    private static async Task<IResult> SendChatMessageAsync(
        Guid projectId,
        Guid groupId,
        SendChatMessageRequest request,
        HttpContext httpContext,
        WeihnachtsfeierDbContext dbContext,
        IHubContext<GameStateHub> hubContext,
        CancellationToken cancellationToken)
    {
        var access = httpContext.Items[RequireProjectAccessFilter.AccessItemKey] as ProjectAccess;
        if (access is null) return Results.Unauthorized();

        var text = request.MessageText?.Trim();
        if (string.IsNullOrWhiteSpace(text) || text.Length > 1000)
        {
            return Results.BadRequest(new ApiError("Nachricht muss zwischen 1 und 1000 Zeichen lang sein."));
        }

        var group = await dbContext.ChatGroups
            .Include(cg => cg.Members)
            .FirstOrDefaultAsync(cg => cg.Id == groupId && cg.ProjektId == projectId, cancellationToken);

        if (group is null) return Results.NotFound();

        string senderName = "Teilnehmer";
        string? teamColor = null;

        if (access.ParticipantId is Guid pId)
        {
            var participant = await dbContext.Teilnehmer
                .Include(t => t.Team)
                .FirstOrDefaultAsync(t => t.Id == pId, cancellationToken);

            if (participant is null) return Results.Unauthorized();

            if (group.Type != "global" && group.Type != "team" && !group.Members.Any(m => m.ParticipantId == pId))
            {
                return Results.Forbid();
            }

            var project = await dbContext.Projekte.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken);
            var perms = WeihnachtsfeierBit.Framework.ParticipantPermissionKeys.ResolveEffectivePermissions(project?.PermissionsJson, participant.Berechtigung?.PermissionsJson);
            if (!perms.TryGetValue(WeihnachtsfeierBit.Framework.ParticipantPermissionKeys.CanSendChatMessages, out var canSend) || !canSend)
            {
                return Results.BadRequest(new ApiError("Das Schreiben im Chat ist für deinen Benutzer gesperrt."));
            }

            senderName = string.IsNullOrWhiteSpace(participant.Name) ? $"Teilnehmer #{participant.Nummer}" : participant.Name;
            teamColor = participant.Team?.Color;
        }
        else if (access.AdminId is Guid adminId)
        {
            var admin = await dbContext.ProjektAdmins.FindAsync([adminId], cancellationToken);
            senderName = admin?.Benutzername ?? "Moderator";
        }

        var message = new ChatMessage
        {
            ChatGroupId = groupId,
            SenderParticipantId = access.ParticipantId,
            SenderAdminId = access.AdminId,
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

        var groupName = GameStateHub.GetGroupName(projectId);
        await hubContext.Clients.Group(groupName).SendAsync("ReceiveChatMessage", response, cancellationToken);

        return Results.Ok(response);
    }

    private static async Task<IResult> GetOrCreateDirectChatAsync(
        Guid projectId,
        Guid targetParticipantId,
        HttpContext httpContext,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var access = httpContext.Items[RequireProjectAccessFilter.AccessItemKey] as ProjectAccess;
        if (access is null || (access.ParticipantId is null && access.AdminId is null)) return Results.Unauthorized();

        if (access.AdminId is Guid adminId)
        {
            var target = await dbContext.Teilnehmer.FirstOrDefaultAsync(t => t.Id == targetParticipantId && t.ProjektId == projectId, cancellationToken);
            if (target is null) return Results.NotFound();

            var supportGroup = await dbContext.ChatGroups
                .Include(cg => cg.Members)
                .Where(cg => cg.ProjektId == projectId && cg.Type == "admin_support" && cg.Members.Any(m => m.ParticipantId == targetParticipantId))
                .FirstOrDefaultAsync(cancellationToken);

            if (supportGroup is not null)
            {
                return Results.Ok(new ChatGroupSummary(supportGroup.Id, supportGroup.Name, supportGroup.Type, supportGroup.AssociatedTeamId, supportGroup.Members.Count, null, null, 0));
            }

            var newSupport = new ChatGroup
            {
                ProjektId = projectId,
                Name = $"Support (Teilnehmer #{target.Nummer})",
                Type = "admin_support"
            };
            newSupport.Members.Add(new ChatGroupMember { ParticipantId = targetParticipantId });
            dbContext.ChatGroups.Add(newSupport);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.Ok(new ChatGroupSummary(newSupport.Id, newSupport.Name, newSupport.Type, newSupport.AssociatedTeamId, newSupport.Members.Count, null, null, 0));
        }

        Guid p1 = access.ParticipantId!.Value;
        Guid p2 = targetParticipantId;
        if (p1 == p2) return Results.BadRequest(new ApiError("Selbstgespräche nicht möglich."));

        var targetParticipant = await dbContext.Teilnehmer.FirstOrDefaultAsync(t => t.Id == p2 && t.ProjektId == projectId, cancellationToken);
        if (targetParticipant is null) return Results.NotFound();

        var existingGroup = await dbContext.ChatGroups
            .Include(cg => cg.Members)
            .Where(cg => cg.ProjektId == projectId && cg.Type == "direct" && cg.Members.Any(m => m.ParticipantId == p1) && cg.Members.Any(m => m.ParticipantId == p2))
            .FirstOrDefaultAsync(cancellationToken);

        if (existingGroup is not null)
        {
            return Results.Ok(new ChatGroupSummary(existingGroup.Id, existingGroup.Name, existingGroup.Type, existingGroup.AssociatedTeamId, existingGroup.Members.Count, null, null, 0));
        }

        var sender = await dbContext.Teilnehmer.FindAsync([p1], cancellationToken);
        string groupName = $"{sender?.Name ?? $"#{sender?.Nummer}"} & {targetParticipant.Name ?? $"#{targetParticipant.Nummer}"}";

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

        return Results.Ok(new ChatGroupSummary(newGroup.Id, newGroup.Name, newGroup.Type, newGroup.AssociatedTeamId, newGroup.Members.Count, null, null, 0));
    }

    private static async Task<IResult> CreateCustomGroupAsync(
        Guid projectId,
        CreateCustomGroupRequest request,
        HttpContext httpContext,
        WeihnachtsfeierDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var access = httpContext.Items[RequireProjectAccessFilter.AccessItemKey] as ProjectAccess;
        if (access is null || (access.ParticipantId is null && access.AdminId is null)) return Results.Unauthorized();

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

        if (access.ParticipantId is Guid pId)
        {
            group.Members.Add(new ChatGroupMember { ParticipantId = pId });
        }

        if (request.ParticipantIds is not null)
        {
            foreach (var pid in request.ParticipantIds.Distinct())
            {
                if (access.ParticipantId is null || pid != access.ParticipantId.Value)
                {
                    group.Members.Add(new ChatGroupMember { ParticipantId = pid });
                }
            }
        }

        dbContext.ChatGroups.Add(group);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(new ChatGroupSummary(group.Id, group.Name, group.Type, group.AssociatedTeamId, group.Members.Count, null, null, 0));
    }

    private static async Task<IResult> RenameChatGroupAsync(
        Guid projectId,
        Guid groupId,
        RenameChatGroupRequest request,
        HttpContext httpContext,
        WeihnachtsfeierDbContext dbContext,
        IHubContext<GameStateHub> hubContext,
        CancellationToken cancellationToken)
    {
        var access = httpContext.Items[RequireProjectAccessFilter.AccessItemKey] as ProjectAccess;
        if (access is null || access.AdminId is null) return Results.Unauthorized();

        var group = await dbContext.ChatGroups
            .FirstOrDefaultAsync(g => g.Id == groupId && g.ProjektId == projectId, cancellationToken);

        if (group is null) return Results.NotFound();

        if (group.Type == "global")
        {
            return Results.BadRequest(new ApiError("Die globale Hauptgruppe kann nicht umbenannt werden."));
        }

        var newName = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(newName) || newName.Length > 60)
        {
            return Results.BadRequest(new ApiError("Gruppenname muss zwischen 1 und 60 Zeichen lang sein."));
        }

        group.Name = newName;
        await dbContext.SaveChangesAsync(cancellationToken);

        await hubContext.Clients.Group(GameStateHub.GetGroupName(projectId)).SendAsync("ReceiveGroupRenamed", groupId, group.Name, cancellationToken);
        return Results.Ok(new ChatGroupSummary(group.Id, group.Name, group.Type, group.AssociatedTeamId, group.Members.Count, null, null, 0, null));
    }

    private static async Task<IResult> DeleteChatMessageAsync(
        Guid projectId,
        Guid messageId,
        WeihnachtsfeierDbContext dbContext,
        IHubContext<GameStateHub> hubContext,
        CancellationToken cancellationToken)
    {
        var message = await dbContext.ChatMessages
            .Include(m => m.ChatGroup)
            .FirstOrDefaultAsync(m => m.Id == messageId && m.ChatGroup.ProjektId == projectId, cancellationToken);

        if (message is null) return Results.NotFound();

        var groupId = message.ChatGroupId;
        dbContext.ChatMessages.Remove(message);
        await dbContext.SaveChangesAsync(cancellationToken);

        await hubContext.Clients.Group(GameStateHub.GetGroupName(projectId)).SendAsync("ReceiveMessageDeleted", messageId, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> DeleteChatGroupAsync(
        Guid projectId,
        Guid groupId,
        WeihnachtsfeierDbContext dbContext,
        IHubContext<GameStateHub> hubContext,
        CancellationToken cancellationToken)
    {
        var group = await dbContext.ChatGroups
            .FirstOrDefaultAsync(g => g.Id == groupId && g.ProjektId == projectId, cancellationToken);

        if (group is null) return Results.NotFound();

        if (group.Type == "global")
        {
            return Results.BadRequest(new ApiError("Die globale Hauptgruppe kann nicht gelöscht werden."));
        }

        dbContext.ChatGroups.Remove(group);
        await dbContext.SaveChangesAsync(cancellationToken);

        await hubContext.Clients.Group(GameStateHub.GetGroupName(projectId)).SendAsync("ReceiveGroupDeleted", groupId, cancellationToken);
        return Results.NoContent();
    }
}
