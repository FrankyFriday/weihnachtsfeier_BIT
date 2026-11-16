using Microsoft.AspNetCore.SignalR;
using WeihnachtsfeierBit.ApiService.Auth;
using WeihnachtsfeierBit.ApiService.Hubs;

namespace WeihnachtsfeierBit.ApiService.Services;

public sealed class ProjectHubConnectionManager(IHubContext<GameStateHub> hubContext)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<(string ConnectionId, Guid ProjectId), HubConnectionAccess> _connectionsByConnection = [];
    private readonly Dictionary<Guid, HashSet<(string ConnectionId, Guid ProjectId)>> _connectionsBySession = [];
    private readonly Dictionary<Guid, DateTime> _revokedSessions = [];

    public async Task<bool> JoinProjectAsync(
        ProjectAccess access,
        Guid projectId,
        string connectionId,
        bool canReadChat,
        Guid? teamId,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            RemoveExpiredRevocations();
            if (_revokedSessions.ContainsKey(access.SessionId))
            {
                return false;
            }

            var connection = (connectionId, projectId);
            await hubContext.Groups.AddToGroupAsync(
                connectionId,
                access.IsAdmin ? GameStateHub.GetAdminGroupName(projectId) : GameStateHub.GetGroupName(projectId),
                cancellationToken);
            if (!access.IsAdmin && teamId is Guid joinedTeamId)
            {
                await hubContext.Groups.AddToGroupAsync(
                    connectionId,
                    GameStateHub.GetTeamGroupName(projectId, joinedTeamId),
                    cancellationToken);
            }
            if (access.IsAdmin || canReadChat)
            {
                await hubContext.Groups.AddToGroupAsync(
                    connectionId,
                    GameStateHub.GetChatGroupName(projectId),
                    cancellationToken);
            }

            _connectionsByConnection[connection] = new HubConnectionAccess(
                access.SessionId,
                access.ParticipantId,
                access.IsAdmin,
                access.IsAdmin || canReadChat,
                teamId);

            if (!_connectionsBySession.TryGetValue(access.SessionId, out var connections))
            {
                connections = [];
                _connectionsBySession.Add(access.SessionId, connections);
            }

            connections.Add(connection);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task LeaveProjectAsync(
        string connectionId,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await hubContext.Groups.RemoveFromGroupAsync(
                connectionId,
                GetAccessGroupName(connectionId, projectId),
                cancellationToken);
            await hubContext.Groups.RemoveFromGroupAsync(
                connectionId,
                GameStateHub.GetChatGroupName(projectId),
                cancellationToken);
            if (_connectionsByConnection.TryGetValue((connectionId, projectId), out var existing)
                && !existing.IsAdmin
                && existing.TeamId is Guid teamId)
            {
                await hubContext.Groups.RemoveFromGroupAsync(
                    connectionId,
                    GameStateHub.GetTeamGroupName(projectId, teamId),
                    cancellationToken);
            }
            RemoveConnection((connectionId, projectId));
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RemoveConnectionAsync(string connectionId)
    {
        await _gate.WaitAsync();
        try
        {
            var connections = _connectionsByConnection.Keys
                .Where(connection => connection.ConnectionId == connectionId)
                .ToArray();

            foreach (var connection in connections)
            {
                await hubContext.Groups.RemoveFromGroupAsync(
                    connectionId,
                    GetAccessGroupName(connectionId, connection.ProjectId));
                await hubContext.Groups.RemoveFromGroupAsync(
                    connectionId,
                    GameStateHub.GetChatGroupName(connection.ProjectId));
                if (_connectionsByConnection.TryGetValue(connection, out var existing)
                    && !existing.IsAdmin
                    && existing.TeamId is Guid teamId)
                {
                    await hubContext.Groups.RemoveFromGroupAsync(
                        connectionId,
                        GameStateHub.GetTeamGroupName(connection.ProjectId, teamId));
                }
                RemoveConnection(connection);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RevokeSessionAsync(Guid sessionId, DateTime expiresAtUtc)
    {
        await _gate.WaitAsync();
        try
        {
            RemoveExpiredRevocations();
            _revokedSessions[sessionId] = expiresAtUtc;

            if (!_connectionsBySession.Remove(sessionId, out var connections))
            {
                return;
            }

            foreach (var connection in connections)
            {
                await hubContext.Groups.RemoveFromGroupAsync(
                    connection.ConnectionId,
                    GetAccessGroupName(connection.ConnectionId, connection.ProjectId));
                await hubContext.Groups.RemoveFromGroupAsync(
                    connection.ConnectionId,
                    GameStateHub.GetChatGroupName(connection.ProjectId));
                if (_connectionsByConnection.TryGetValue(connection, out var existing)
                    && !existing.IsAdmin
                    && existing.TeamId is Guid teamId)
                {
                    await hubContext.Groups.RemoveFromGroupAsync(
                        connection.ConnectionId,
                        GameStateHub.GetTeamGroupName(connection.ProjectId, teamId));
                }
                _connectionsByConnection.Remove(connection);
                await hubContext.Clients.Client(connection.ConnectionId)
                    .SendAsync("SessionRevoked", CancellationToken.None);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task UpdateChatReadersAsync(
        Guid projectId,
        IReadOnlySet<Guid> participantIds,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            foreach (var pair in _connectionsByConnection
                         .Where(item => item.Key.ProjectId == projectId && !item.Value.IsAdmin)
                         .ToArray())
            {
                if (pair.Value.ParticipantId is not Guid participantId)
                {
                    continue;
                }

                var canReadChat = participantIds.Contains(participantId);
                if (canReadChat == pair.Value.CanReadChat)
                {
                    continue;
                }

                if (canReadChat)
                {
                    await hubContext.Groups.AddToGroupAsync(
                        pair.Key.ConnectionId,
                        GameStateHub.GetChatGroupName(projectId),
                        cancellationToken);
                }
                else
                {
                    await hubContext.Groups.RemoveFromGroupAsync(
                        pair.Key.ConnectionId,
                        GameStateHub.GetChatGroupName(projectId),
                        cancellationToken);
                }

                _connectionsByConnection[pair.Key] = pair.Value with { CanReadChat = canReadChat };
            }

            var participantConnections = _connectionsByConnection
                .Where(item => item.Key.ProjectId == projectId && !item.Value.IsAdmin)
                .Select(item => item.Key.ConnectionId)
                .Distinct()
                .ToArray();
            if (participantConnections.Length > 0)
            {
                await hubContext.Clients.Clients(participantConnections)
                    .SendAsync("ParticipantPermissionsChanged", cancellationToken);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SyncParticipantTeamGroupsAsync(
        Guid projectId,
        IReadOnlyDictionary<Guid, Guid?> teamByParticipant,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            foreach (var pair in _connectionsByConnection
                         .Where(item => item.Key.ProjectId == projectId && !item.Value.IsAdmin)
                         .ToArray())
            {
                if (pair.Value.ParticipantId is not Guid participantId)
                {
                    continue;
                }

                var teamId = teamByParticipant.GetValueOrDefault(participantId);
                if (teamId == pair.Value.TeamId)
                {
                    continue;
                }

                if (pair.Value.TeamId is Guid oldTeamId)
                {
                    await hubContext.Groups.RemoveFromGroupAsync(
                        pair.Key.ConnectionId,
                        GameStateHub.GetTeamGroupName(projectId, oldTeamId),
                        cancellationToken);
                }
                if (teamId is Guid newTeamId)
                {
                    await hubContext.Groups.AddToGroupAsync(
                        pair.Key.ConnectionId,
                        GameStateHub.GetTeamGroupName(projectId, newTeamId),
                        cancellationToken);
                }

                _connectionsByConnection[pair.Key] = pair.Value with { TeamId = teamId };
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private string GetAccessGroupName(string connectionId, Guid projectId) =>
        _connectionsByConnection.TryGetValue((connectionId, projectId), out var access) && access.IsAdmin
            ? GameStateHub.GetAdminGroupName(projectId)
            : GameStateHub.GetGroupName(projectId);

    private void RemoveConnection((string ConnectionId, Guid ProjectId) connection)
    {
        if (!_connectionsByConnection.Remove(connection, out var access) ||
            !_connectionsBySession.TryGetValue(access.SessionId, out var connections))
        {
            return;
        }

        connections.Remove(connection);
        if (connections.Count == 0)
        {
            _connectionsBySession.Remove(access.SessionId);
        }
    }

    private void RemoveExpiredRevocations()
    {
        var now = DateTime.UtcNow;
        foreach (var sessionId in _revokedSessions
                     .Where(item => item.Value <= now)
                     .Select(item => item.Key)
                     .ToArray())
        {
            _revokedSessions.Remove(sessionId);
        }
    }

    private sealed record HubConnectionAccess(
        Guid SessionId,
        Guid? ParticipantId,
        bool IsAdmin,
        bool CanReadChat,
        Guid? TeamId);
}
