namespace WeihnachtsfeierBit.ParticipantClient.Services;

public enum ParticipantToastSeverity
{
    Success,
    Warning,
    Error
}

public sealed record ParticipantToast(long Id, string Message, ParticipantToastSeverity Severity);

public sealed class ParticipantNotificationService
{
    private long _nextId;

    public event Action<ParticipantToast>? NotificationRaised;

    public void Show(string message, ParticipantToastSeverity severity)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        NotificationRaised?.Invoke(new ParticipantToast(
            Interlocked.Increment(ref _nextId),
            message,
            severity));
    }
}
