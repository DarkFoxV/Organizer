using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using Organizer.Application.Enums;

namespace Organizer.Application.Services;

public sealed class ToastService : IToastService, IToastNotificationStore
{
    private const int MaxVisibleToasts = 4;
    private readonly ObservableCollection<ToastNotification> _toasts = [];

    public ToastService()
    {
        Toasts = new ReadOnlyObservableCollection<ToastNotification>(_toasts);
    }

    public ReadOnlyObservableCollection<ToastNotification> Toasts { get; }

    public void Info(string title, string? message = null)
    {
        Show(ToastType.Info, title, message, TimeSpan.FromSeconds(4));
    }

    public void Success(string title, string? message = null)
    {
        Show(ToastType.Success, title, message, TimeSpan.FromSeconds(4));
    }

    public void Warning(string title, string? message = null)
    {
        Show(ToastType.Warning, title, message, TimeSpan.FromSeconds(6));
    }

    public void Error(string title, string? message = null)
    {
        Show(ToastType.Error, title, message, TimeSpan.FromSeconds(8));
    }

    public void Dismiss(Guid id)
    {
        RunOnUiThread(() => RemoveCore(id));
    }

    private void Show(ToastType type, string title, string? message, TimeSpan duration)
    {
        var notification = new ToastNotification
        {
            Type = type,
            Title = title.Trim(),
            Message = string.IsNullOrWhiteSpace(message) ? null : message.Trim(),
            Duration = duration
        };

        RunOnUiThread(() =>
        {
            MakeRoomForNextToast();
            _toasts.Add(notification);
            StartAutoDismiss(notification);
        });
    }

    private async void StartAutoDismiss(ToastNotification notification)
    {
        if (notification.Duration <= TimeSpan.Zero)
            return;

        await Task.Delay(notification.Duration);
        Dismiss(notification.Id);
    }

    private void MakeRoomForNextToast()
    {
        while (_toasts.Count >= MaxVisibleToasts)
        {
            var toast = _toasts.FirstOrDefault(item => item.Type != ToastType.Error)
                ?? _toasts.FirstOrDefault();

            if (toast is null)
                return;

            _toasts.Remove(toast);
        }
    }

    private void RemoveCore(Guid id)
    {
        var toast = _toasts.FirstOrDefault(item => item.Id == id);

        if (toast is not null)
            _toasts.Remove(toast);
    }

    private static void RunOnUiThread(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
            return;
        }

        Dispatcher.UIThread.Post(action);
    }
}
