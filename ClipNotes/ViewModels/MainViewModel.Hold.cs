using System.Windows;
using ClipNotes.Helpers;
using ClipNotes.Models;
using ClipNotes.Services;
using Loc = ClipNotes.Helpers.LocalizationService;

namespace ClipNotes.ViewModels;

public partial class MainViewModel
{
    private async Task StartHoldAsync(MarkerType type)
    {
        var status = await _obs.GetRecordStatusAsync();
        if (status == null) return;
        _holdStart = status.Value.duration;
        _holdMarkerType = type;
        _holdStartWallClock = DateTime.Now;
        IsHolding = true;
        HoldingType = type;
        HoldingTimerText = "0.0";
        _holdTimer.Start();
        RecordingStatus = $"{Loc.T("loc_StatusHolding")}: {type} @ {_holdStart:hh\\:mm\\:ss}";
    }

    private async Task EndHoldAsync()
    {
        _holdTimer.Stop();
        IsHolding = false;
        if (_holdStart == null) return;
        try
        {
            var status = await _obs.GetRecordStatusAsync();
            if (status == null) return;

            var holdEnd = status.Value.duration;
            var holdDuration = holdEnd - _holdStart.Value;
            if (holdDuration < TimeSpan.Zero) holdDuration = TimeSpan.Zero;

            var marker = new Marker
            {
                Index = Markers.Count + 1,
                Type = _holdMarkerType,
                Timestamp = _holdStart.Value,
                Timecode = _holdStart.Value.ToString(@"hh\:mm\:ss"),
                HoldDuration = holdDuration
            };

            Markers.Add(marker);
            SaveMarkersToSession();
            RecordingStatus = $"[{_holdMarkerType}] {marker.TimestampFormatted} ({holdDuration:mm\\:ss})";
        }
        finally
        {
            _holdStart = null;
        }
    }

    public async Task StartUiHoldAsync(MarkerType type)
    {
        if (!HoldModeEnabled || !IsRecording || _currentSession == null || _holdStart != null) return;
        await StartHoldAsync(type);
    }

    public async Task EndUiHoldAsync()
    {
        if (!IsHolding || _currentSession == null) return;
        await EndHoldAsync();
    }

    private void OnHotkeyPressed(HotkeyAction action)
    {
        // BeginInvoke schedules an async void on the UI thread (fire-and-forget via dispatcher queue)
        Application.Current.Dispatcher.BeginInvoke(async () =>
        {
            var isMarkerAction = action is HotkeyAction.MarkerBug or HotkeyAction.MarkerTask or HotkeyAction.MarkerNote or HotkeyAction.MarkerSummary;

            if (isMarkerAction && HoldModeEnabled && IsRecording && _currentSession != null)
            {
                _holdMarkerType = action switch
                {
                    HotkeyAction.MarkerBug => MarkerType.Bug,
                    HotkeyAction.MarkerTask => MarkerType.Task,
                    HotkeyAction.MarkerSummary => MarkerType.Summary,
                    _ => MarkerType.Note
                };
                await StartHoldAsync(_holdMarkerType);
                return;
            }

            switch (action)
            {
                case HotkeyAction.MarkerBug:
                    await AddMarkerAsync(MarkerType.Bug);
                    break;
                case HotkeyAction.MarkerTask:
                    await AddMarkerAsync(MarkerType.Task);
                    break;
                case HotkeyAction.MarkerNote:
                    await AddMarkerAsync(MarkerType.Note);
                    break;
                case HotkeyAction.MarkerSummary:
                    await AddMarkerAsync(MarkerType.Summary);
                    break;
                case HotkeyAction.StartRecording:
                    await StartRecordingAsync();
                    break;
                case HotkeyAction.StopRecording:
                    await StopRecordingAsync();
                    break;
                case HotkeyAction.Generate:
                    await GenerateAsync();
                    break;
                case HotkeyAction.OpenOutputFolder:
                    OpenSessionFolder();
                    break;
            }
        });
    }

    private void OnHotkeyReleased(HotkeyAction action)
    {
        if (!HoldModeEnabled || !IsRecording || _currentSession == null || _holdStart == null) return;
        Application.Current.Dispatcher.BeginInvoke(async () => await EndHoldAsync());
    }
}
