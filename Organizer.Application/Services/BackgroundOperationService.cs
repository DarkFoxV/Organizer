using System;
using System.Threading;
using Avalonia.Threading;
using Organizer.Application.ViewModels;

namespace Organizer.Application.Services;

public sealed class BackgroundOperationService(
    BackgroundOperationHostViewModel host) : IBackgroundOperationService
{
    private static readonly TimeSpan MinimumVisibleDuration = TimeSpan.FromMilliseconds(750);

    public IBackgroundOperation Start(
        string title,
        string? message = null,
        bool canCancel = false,
        CancellationTokenSource? cancellationTokenSource = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        var cts = cancellationTokenSource ?? new CancellationTokenSource();
        var operation = new BackgroundOperationViewModel(
            title.Trim(),
            NormalizeMessage(message),
            canCancel,
            cts);

        RunOnUiThread(() => host.Add(operation));
        return new BackgroundOperationHandle(
            this,
            operation,
            cts,
            DateTimeOffset.UtcNow);
    }

    private void ReportProgress(
        BackgroundOperationViewModel operation,
        double progress,
        string? message)
    {
        RunOnUiThread(() => operation.ReportProgress(progress, NormalizeMessage(message)));
    }

    private void ReportIndeterminate(
        BackgroundOperationViewModel operation,
        string? message)
    {
        RunOnUiThread(() => operation.ReportIndeterminate(NormalizeMessage(message)));
    }

    private async void Finish(
        BackgroundOperationViewModel operation,
        CancellationTokenSource cancellationTokenSource,
        DateTimeOffset startedAt)
    {
        var remaining = MinimumVisibleDuration - (DateTimeOffset.UtcNow - startedAt);
        if (remaining > TimeSpan.Zero)
            await System.Threading.Tasks.Task.Delay(remaining);

        RunOnUiThread(() =>
        {
            host.Remove(operation.Id);
            cancellationTokenSource.Dispose();
        });
    }

    private static string? NormalizeMessage(string? message) =>
        string.IsNullOrWhiteSpace(message) ? null : message.Trim();

    private static void RunOnUiThread(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
            return;
        }

        Dispatcher.UIThread.Post(action);
    }

    private sealed class BackgroundOperationHandle(
        BackgroundOperationService service,
        BackgroundOperationViewModel operation,
        CancellationTokenSource cancellationTokenSource,
        DateTimeOffset startedAt) : IBackgroundOperation
    {
        private int _finished;

        public Guid Id => operation.Id;
        public CancellationToken CancellationToken => cancellationTokenSource.Token;

        public void ReportProgress(double progress, string? message = null)
        {
            if (Volatile.Read(ref _finished) == 0)
                service.ReportProgress(operation, progress, message);
        }

        public void ReportIndeterminate(string? message = null)
        {
            if (Volatile.Read(ref _finished) == 0)
                service.ReportIndeterminate(operation, message);
        }

        public void Complete() => Finish();

        public void Fail(Exception? exception = null) => Finish();

        public void Cancel() => Finish();

        private void Finish()
        {
            if (Interlocked.Exchange(ref _finished, 1) != 0)
                return;

            service.Finish(operation, cancellationTokenSource, startedAt);
        }
    }
}
