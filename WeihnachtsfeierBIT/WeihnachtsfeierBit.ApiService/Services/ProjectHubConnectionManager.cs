using Microsoft.AspNetCore.SignalR;
using WeihnachtsfeierBit.ApiService.Hubs;

namespace WeihnachtsfeierBit.ApiService.Services;

public sealed class ProjectHubConnectionManager(IHubContext<GameStateHub> hubContext)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<(string ConnectionId, Guid ProjectId), Guid> _sessionsByConnection = [];
    private readonly Dictionary<Guid, HashSet<(string ConnectionId, Guid ProjectId)>> _connectionsBySession = [];
    private readonly Dictionary<Guid, DateTime> _revokedSessions = [];

    public async Task<bool> JoinProjectAsync(
        Guid sessionId,
        Guid projectId,
        string connectionId,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            RemoveExpiredRevocations();
            if (_revokedSessions.ContainsKey(sessionId))
            {
                return false;
            }

            var connection = (connectionId, projectId);
            await hubContext.Groups.AddToGroupAsync(
                connectionId,
                GameStateHub.GetGroupName(projectId),
                cancellationToken);
            _sessionsByConnection[connection] = sessionId;

            if (!_connectionsBySession.TryGetValue(sessionId, out var connections))
            {
                connections = [];
                _connectionsBySession.Add(sessionId, connections);
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
                GameStateHub.GetGroupName(projectId),
                cancellationToken);
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
            var connections = _sessionsByConnection.Keys
                .Where(connection => connection.ConnectionId == connectionId)
                .ToArray();

            foreach (var connection in connections)
            {
                await hubContext.Groups.RemoveFromGroupAsync(
                    connectionId,
                    GameStateHub.GetGroupName(connection.ProjectId));
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
                    GameStateHub.GetGroupName(connection.ProjectId));
                _sessionsByConnection.Remove(connection);
                await hubContext.Clients.Client(connection.ConnectionId)
                    .SendAsync("SessionRevoked", CancellationToken.None);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private void RemoveConnection((string ConnectionId, Guid ProjectId) connection)
    {
        if (!_sessionsByConnection.Remove(connection, out var sessionId) ||
            !_connectionsBySession.TryGetValue(sessionId, out var connections))
        {
            return;
        }

        connections.Remove(connection);
        if (connections.Count == 0)
        {
            _connectionsBySession.Remove(sessionId);
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
}
