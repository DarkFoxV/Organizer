using System;
using System.Threading;

namespace Organizer.Application.Services;

public interface IBackgroundOperationService
{
    IBackgroundOperation Start(
        string title,
        string? message = null,
        bool canCancel = false,
        CancellationTokenSource? cancellationTokenSource = null);
}

public interface IBackgroundOperation
{
    Guid Id { get; }
    CancellationToken CancellationToken { get; }

    void ReportProgress(double progress, string? message = null);
    void ReportIndeterminate(string? message = null);
    void Complete();
    void Fail(Exception? exception = null);
    void Cancel();
}
