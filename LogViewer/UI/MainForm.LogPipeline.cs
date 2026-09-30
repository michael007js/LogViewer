using System.Collections.Concurrent;
using System.Diagnostics;
using LogViewer.Models;

namespace LogViewer.UI;

public partial class MainForm
{
    private const int UiLogCoalesceDelayMs = 16;
    private const int UiLogBatchLimit = 512;
    private const int UiLogTimeBudgetMs = 8;

    private readonly ConcurrentQueue<PendingUiLog> _pendingUiLogs = new();
    private int _uiLogFlushScheduled;
    private int _uiLogPipelineVersion;
    private volatile bool _isClosing;

    private enum PendingLogKind
    {
        Network,
        Normal
    }

    private readonly record struct PendingUiLog(PendingLogKind Kind, string DeviceId, LogEntry Entry);

    private void EnqueueUiLog(PendingLogKind kind, string deviceId, LogEntry entry)
    {
        if (_isClosing) return;
        _pendingUiLogs.Enqueue(new PendingUiLog(kind, deviceId, entry));
        ScheduleUiLogFlush();
    }

    private void ScheduleUiLogFlush()
    {
        if (_isClosing || Interlocked.CompareExchange(ref _uiLogFlushScheduled, 1, 0) != 0) return;
        var version = Volatile.Read(ref _uiLogPipelineVersion);
        _ = PostUiLogFlushAsync(version);
    }

    private async Task PostUiLogFlushAsync(int version)
    {
        try
        {
            await Task.Delay(UiLogCoalesceDelayMs).ConfigureAwait(false);
            if (_isClosing || version != Volatile.Read(ref _uiLogPipelineVersion))
            {
                Interlocked.Exchange(ref _uiLogFlushScheduled, 0);
                return;
            }

            if (IsDisposed || !IsHandleCreated)
            {
                Interlocked.Exchange(ref _uiLogFlushScheduled, 0);
                return;
            }

            BeginInvoke(new Action(() => FlushPendingUiLogs(version)));
        }
        catch (ObjectDisposedException)
        {
            Interlocked.Exchange(ref _uiLogFlushScheduled, 0);
        }
        catch (InvalidOperationException)
        {
            Interlocked.Exchange(ref _uiLogFlushScheduled, 0);
        }
    }

    private void FlushPendingUiLogs(int version)
    {
        if (_isClosing || IsDisposed || version != Volatile.Read(ref _uiLogPipelineVersion))
        {
            Interlocked.Exchange(ref _uiLogFlushScheduled, 0);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var deviceCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var networkChanged = false;
        var normalChanged = false;
        var processed = 0;

        while (processed < UiLogBatchLimit &&
               stopwatch.ElapsedMilliseconds < UiLogTimeBudgetMs &&
               _pendingUiLogs.TryDequeue(out var pending))
        {
            if (pending.Kind == PendingLogKind.Network)
            {
                if (_deviceLogs.TryGetValue(pending.DeviceId, out var deviceBuffer))
                {
                    deviceBuffer.Add(pending.Entry);
                    deviceCounts[pending.DeviceId] = deviceBuffer.Count;
                }

                _allLogs.Add(pending.Entry);
                networkChanged = true;
            }
            else
            {
                if (_deviceNormalLogs.TryGetValue(pending.DeviceId, out var deviceBuffer))
                    deviceBuffer.Add(pending.Entry);

                _allNormalLogs.Add(pending.Entry);
                normalChanged = true;
            }

            processed++;
        }

        if (deviceCounts.Count > 0) _devicePanel.UpdateLogCounts(deviceCounts);
        if (networkChanged) _networkLogForm.NotifyDataChanged();
        if (normalChanged) _normalLogForm.NotifyDataChanged();

        if (!_pendingUiLogs.IsEmpty)
        {
            try
            {
                BeginInvoke(new Action(() => FlushPendingUiLogs(version)));
                return;
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }

        Interlocked.Exchange(ref _uiLogFlushScheduled, 0);
        if (!_isClosing && !_pendingUiLogs.IsEmpty) ScheduleUiLogFlush();
    }

    private void CancelUiLogPipeline()
    {
        Interlocked.Increment(ref _uiLogPipelineVersion);
        Interlocked.Exchange(ref _uiLogFlushScheduled, 0);
        while (_pendingUiLogs.TryDequeue(out _))
        {
        }
    }
}
