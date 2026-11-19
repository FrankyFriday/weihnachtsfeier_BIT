using WeihnachtsfeierBit.ApiService.Services;

namespace WeihnachtsfeierBit.ApiService.Tests;

public sealed class ParticipantEmulatorTicketStoreTests
{
    [Fact]
    public void TryConsume_RequiresMatchingSessionAndCanOnlySucceedOnce()
    {
        var store = new ParticipantEmulatorTicketStore();
        var now = new DateTime(2026, 11, 19, 12, 0, 0, DateTimeKind.Utc);
        var projectId = Guid.NewGuid();
        var instanceId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var ticket = store.Issue(projectId, instanceId, participantId, now);

        Assert.False(store.TryConsume(ticket.Ticket, projectId, instanceId, Guid.NewGuid(), now));
        Assert.True(store.TryConsume(ticket.Ticket, projectId, instanceId, participantId, now));
        Assert.False(store.TryConsume(ticket.Ticket, projectId, instanceId, participantId, now));
    }

    [Fact]
    public void TryConsume_RejectsExpiredTicket()
    {
        var store = new ParticipantEmulatorTicketStore();
        var now = new DateTime(2026, 11, 19, 12, 0, 0, DateTimeKind.Utc);
        var ticket = store.Issue(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), now);

        Assert.False(store.TryConsume(
            ticket.Ticket,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            ticket.ExpiresAtUtc));
    }
}
