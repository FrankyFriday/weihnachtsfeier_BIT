using WeihnachtsfeierBit.ApiService.Services;
using WeihnachtsfeierBit.Data;

namespace WeihnachtsfeierBit.ApiService.Tests;

public sealed class SessionEventOrderServiceTests
{
    [Fact]
    public void NextPosition_ReturnsZeroForEmptySessionAndAppendsOtherwise()
    {
        Assert.Equal(0, SessionEventOrderService.NextPosition([]));
        Assert.Equal(4, SessionEventOrderService.NextPosition([CreateEvent(1), CreateEvent(3)]));
    }

    [Fact]
    public void TryApplyOrder_AssignsRequestedOrderAndAppendsMissingEvents()
    {
        var first = CreateEvent(0);
        var second = CreateEvent(1);
        var third = CreateEvent(2);
        var addedConcurrently = CreateEvent(3);

        var error = SessionEventOrderService.TryApplyOrder(
            [first, second, third, addedConcurrently],
            [third.Id, first.Id, second.Id]);

        Assert.Null(error);
        Assert.Equal(0, third.Position);
        Assert.Equal(1, first.Position);
        Assert.Equal(2, second.Position);
        Assert.Equal(3, addedConcurrently.Position);
    }

    [Fact]
    public void TryApplyOrder_RejectsDuplicateAndForeignEventsWithoutChangingPositions()
    {
        var first = CreateEvent(0);
        var second = CreateEvent(1);

        Assert.NotNull(SessionEventOrderService.TryApplyOrder([first, second], [second.Id, second.Id]));
        Assert.NotNull(SessionEventOrderService.TryApplyOrder([first, second], [Guid.NewGuid(), first.Id]));
        Assert.Equal(0, first.Position);
        Assert.Equal(1, second.Position);
    }

    private static GameInstanceEvent CreateEvent(int position) => new() { Position = position };
}
