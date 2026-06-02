using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using Organizer.Application.Services;

namespace Organizer.Application.Components;

public partial class ToastHost : UserControl
{
    public ToastHost()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<IToastNotificationStore>();
    }

    private void OnDismissClick(object? sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.CommandParameter is not ToastNotification notification)
            return;

        if (DataContext is IToastNotificationStore store)
            store.Dismiss(notification.Id);
    }
}
