using System.Collections.Concurrent;

namespace WeihnachtsfeierBit.Framework.Services;

public enum DiagnosticLogLevel
{
    Info,
    Warning,
    Error
}

public sealed record DiagnosticLogEntry(
    DateTime TimestampUtc,
    DiagnosticLogLevel Level,
    string Category,
    string Message,
    int? HttpStatusCode = null,
    string? Details = null);

public sealed class ClientDiagnosticLogger
{
    private const int MaxEntries = 200;
    private readonly ConcurrentQueue<DiagnosticLogEntry> _logs = new();

    public event Action? OnLogAdded;

    public void Log(DiagnosticLogLevel level, string category, string message, int? httpStatusCode = null, string? details = null)
    {
        var entry = new DiagnosticLogEntry(DateTime.UtcNow, level, category, message, httpStatusCode, details);
        _logs.Enqueue(entry);
        while (_logs.Count > MaxEntries)
        {
            _logs.TryDequeue(out _);
        }
        OnLogAdded?.Invoke();
    }

    public void LogInfo(string category, string message, string? details = null)
        => Log(DiagnosticLogLevel.Info, category, message, null, details);

    public void LogWarning(string category, string message, int? statusCode = null, string? details = null)
        => Log(DiagnosticLogLevel.Warning, category, message, statusCode, details);

    public void LogError(string category, string message, int? statusCode = null, string? details = null)
        => Log(DiagnosticLogLevel.Error, category, message, statusCode, details);

    public IReadOnlyList<DiagnosticLogEntry> GetLogs() => _logs.ToArray();

    public void Clear()
    {
        while (_logs.TryDequeue(out _)) { }
        OnLogAdded?.Invoke();
    }

    public string FormatLogsAsText()
    {
        var logs = GetLogs();
        if (logs.Count == 0) return "Keine Log-Einträge vorhanden.";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"=== DIAGNOSE PROTOKOLL ({DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC) ===");
        foreach (var log in logs)
        {
            var levelStr = log.Level.ToString().ToUpperInvariant();
            var statusStr = log.HttpStatusCode.HasValue ? $" [HTTP {log.HttpStatusCode}]" : "";
            sb.AppendLine($"[{log.TimestampUtc.ToLocalTime():HH:mm:ss.fff}] [{levelStr}] [{log.Category}]{statusStr} {log.Message}");
            if (!string.IsNullOrWhiteSpace(log.Details))
            {
                sb.AppendLine($"   Details: {log.Details}");
            }
        }
        return sb.ToString();
    }
}
