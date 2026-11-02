using Microsoft.AspNetCore.SignalR.Client;

namespace WeihnachtsfeierBit.Framework;

public static class HubConnectionRecovery
{
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(30)
    ];

    public static async Task RunAsync(
        HubConnection connection,
        Func<CancellationToken, Task> onConnected,
        Action<Exception> onFailure,
        Func<bool, Task> onConnectionStateChanged,
        CancellationToken cancellationToken)
    {
        var retryCount = 0;
        bool? isConnected = null;

        while (!cancellationToken.IsCancellationRequested)
        {
            if (connection.State == HubConnectionState.Disconnected)
            {
                if (isConnected != false)
                {
                    isConnected = false;
                    await onConnectionStateChanged(false);
                }

                try
                {
                    await connection.StartAsync(cancellationToken);
                    await onConnected(cancellationToken);
                    retryCount = 0;
                    if (isConnected != true)
                    {
                        isConnected = true;
                        await onConnectionStateChanged(true);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    onFailure(exception);
                    if (connection.State != HubConnectionState.Disconnected)
                    {
                        try
                        {
                            await connection.StopAsync(cancellationToken);
                        }
                        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                        {
                            break;
                        }
                        catch (Exception stopException)
                        {
                            onFailure(stopException);
                        }
                    }

                    var retryDelay = RetryDelays[Math.Min(retryCount, RetryDelays.Length - 1)];
                    retryCount++;
                    try
                    {
                        await Task.Delay(retryDelay, cancellationToken);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    continue;
                }
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
