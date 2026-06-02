using System;
using System.Collections.ObjectModel;

namespace Organizer.Application.Services;

public interface IToastService
{
    void Info(string title, string? message = null);

    void Success(string title, string? message = null);

    void Warning(string title, string? message = null);

    void Error(string title, string? message = null);
}

public interface IToastNotificationStore
{
    ReadOnlyObservableCollection<ToastNotification> Toasts { get; }

    void Dismiss(Guid id);
}
