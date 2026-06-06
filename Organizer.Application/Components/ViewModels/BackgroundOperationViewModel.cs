using System;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Organizer.Application.ViewModels;

public partial class BackgroundOperationViewModel : ObservableObject, IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource;
    private bool _isDisposed;

    [ObservableProperty] private string? _message;
    [ObservableProperty] private double _progress;
    [ObservableProperty] private bool _isIndeterminate = true;
    [ObservableProperty] private bool _canCancel;
    [ObservableProperty] private bool _isCancellationRequested;

    public Guid Id { get; } = Guid.NewGuid();
    public string Title { get; }
    public bool HasMessage => !string.IsNullOrWhiteSpace(Message);
    public string ProgressText => $"{Math.Round(Progress):0}%";

    public BackgroundOperationViewModel(
        string title,
        string? message,
        bool canCancel,
        CancellationTokenSource cancellationTokenSource)
    {
        Title = title;
        Message = message;
        CanCancel = canCancel;
        _cancellationTokenSource = cancellationTokenSource;
    }

    public void ReportProgress(double progress, string? message)
    {
        Progress = Math.Clamp(progress, 0, 100);
        IsIndeterminate = false;

        if (message is not null)
            Message = message;
    }

    public void ReportIndeterminate(string? message)
    {
        IsIndeterminate = true;

        if (message is not null)
            Message = message;
    }

    partial void OnMessageChanged(string? value) => OnPropertyChanged(nameof(HasMessage));

    partial void OnProgressChanged(double value) => OnPropertyChanged(nameof(ProgressText));

    [RelayCommand(CanExecute = nameof(CanRequestCancellation))]
    private void Cancel()
    {
        if (!CanRequestCancellation)
            return;

        IsCancellationRequested = true;
        CanCancel = false;
        _ = _cancellationTokenSource.CancelAsync();
        CancelCommand.NotifyCanExecuteChanged();
    }

    private bool CanRequestCancellation => CanCancel && !IsCancellationRequested;

    partial void OnCanCancelChanged(bool value) => CancelCommand.NotifyCanExecuteChanged();

    partial void OnIsCancellationRequestedChanged(bool value) =>
        CancelCommand.NotifyCanExecuteChanged();

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        CanCancel = false;
    }
}
