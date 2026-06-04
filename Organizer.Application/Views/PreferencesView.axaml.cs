using Avalonia.Controls;
using Avalonia.Interactivity;
using Organizer.Application.ViewModels;

namespace Organizer.Organizer.Application.Views;

public partial class PreferencesView : UserControl
{
    public PreferencesView()
    {
        InitializeComponent();
        DetachedFromVisualTree += (_, _) =>
        {
            if (DataContext is PreferencesViewModel vm)
                vm.Dispose();
        };
    }

    private PreferencesViewModel VM => (PreferencesViewModel)DataContext!;

    private void OnGeneralSettingsClick(object? sender, RoutedEventArgs e)
    {
        VM.SelectGeneral();
    }

    private void OnDataBackupClick(object? sender, RoutedEventArgs e)
    {
        VM.SelectDataBackup();
    }

    private void OnAboutClick(object? sender, RoutedEventArgs e)
    {
        VM.SelectAbout();
    }

}
