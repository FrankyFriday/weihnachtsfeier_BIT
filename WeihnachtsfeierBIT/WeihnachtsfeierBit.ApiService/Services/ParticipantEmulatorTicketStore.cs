using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace WeihnachtsfeierBit.ApiService.Services;

public sealed record IssuedParticipantEmulatorTicket(string Ticket, DateTime ExpiresAtUtc);

public sealed class ParticipantEmulatorTicketStore
{
    private static readonly TimeSpan TicketLifetime = TimeSpan.FromMinutes(2);
    private readonly ConcurrentDictionary<string, TicketEntry> _tickets = new(StringComparer.Ordinal);

    public IssuedParticipantEmulatorTicket Issue(
        Guid projectId,
        Guid gameInstanceId,
        Guid participantId,
        DateTime nowUtc)
    {
        RemoveExpiredTickets(nowUtc);

        var ticket = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var expiresAtUtc = nowUtc.Add(TicketLifetime);
        _tickets[Hash(ticket)] = new TicketEntry(projectId, gameInstanceId, participantId, expiresAtUtc);
        return new IssuedParticipantEmulatorTicket(ticket, expiresAtUtc);
    }

    public bool TryConsume(
        string ticket,
        Guid projectId,
        Guid gameInstanceId,
        Guid participantId,
        DateTime nowUtc)
    {
        if (ticket.Length != 64 || !_tickets.TryGetValue(Hash(ticket), out var entry))
        {
            return false;
        }

        if (entry.ExpiresAtUtc <= nowUtc)
        {
            _tickets.TryRemove(Hash(ticket), out _);
            return false;
        }

        if (entry.ProjectId != projectId
            || entry.GameInstanceId != gameInstanceId
            || entry.ParticipantId != participantId)
        {
            return false;
        }

        return _tickets.TryRemove(Hash(ticket), out _);
    }

    private void RemoveExpiredTickets(DateTime nowUtc)
    {
        foreach (var (ticketHash, entry) in _tickets)
        {
            if (entry.ExpiresAtUtc <= nowUtc)
            {
                _tickets.TryRemove(ticketHash, out _);
            }
        }
    }

    private static string Hash(string ticket) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ticket)));

    private sealed record TicketEntry(
        Guid ProjectId,
        Guid GameInstanceId,
        Guid ParticipantId,
        DateTime ExpiresAtUtc);
}
