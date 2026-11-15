using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using WeihnachtsfeierBit.Framework.Services;

namespace WeihnachtsfeierBit.Framework.Components.Modals;

public partial class DiagnosticModal : ComponentBase, IDisposable
{
    [Parameter] public bool IsOpen { get; set; }
    [Parameter] public EventCallback OnClose { get; set; }

    private DiagnosticLogLevel? _filterLevel;
    private bool _copied;

    private IReadOnlyList<DiagnosticLogEntry> _allLogs = [];
    private int _errorCount;
    private int _warningCount;

    protected override void OnInitialized()
    {
        DiagnosticLogger.OnLogAdded += HandleLogAdded;
        RefreshLogs();
    }

    protected override void OnParametersSet()
    {
        if (IsOpen)
        {
            RefreshLogs();
        }
    }

    private void HandleLogAdded()
    {
        InvokeAsync(() =>
        {
            RefreshLogs();
            StateHasChanged();
        });
    }

    private void RefreshLogs()
    {
        _allLogs = DiagnosticLogger.GetLogs();
        _errorCount = _allLogs.Count(l => l.Level == DiagnosticLogLevel.Error);
        _warningCount = _allLogs.Count(l => l.Level == DiagnosticLogLevel.Warning);
    }

    private IReadOnlyList<DiagnosticLogEntry> FilteredLogs
    {
        get
        {
            if (!_filterLevel.HasValue) return _allLogs;
            return _allLogs.Where(l => l.Level == _filterLevel.Value).ToList();
        }
    }

    private async Task CopyLogsAsync()
    {
        var text = DiagnosticLogger.FormatLogsAsText();
        try
        {
            await JS.InvokeVoidAsync("navigator.clipboard.writeText", text);
            _copied = true;
            StateHasChanged();
            await Task.Delay(2000);
            _copied = false;
            StateHasChanged();
        }
        catch
        {
        }
    }

    private void ClearLogs()
    {
        DiagnosticLogger.Clear();
        RefreshLogs();
    }

    public void Dispose()
    {
        DiagnosticLogger.OnLogAdded -= HandleLogAdded;
    }
}
