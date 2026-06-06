using Avalonia.Controls;
using Avalonia.Interactivity;
using Organizer.Application.ViewModels;

namespace Organizer.Organizer.Application.Views;

public partial class PreferencesGeneralView : UserControl
{
    public PreferencesGeneralView()
    {
        InitializeComponent();
    }

    private PreferencesViewModel VM => (PreferencesViewModel)DataContext!;

    private void OnSystemThemeClick(object? sender, RoutedEventArgs e)
    {
        VM.SelectSystemTheme();
    }

    private void OnDarkThemeClick(object? sender, RoutedEventArgs e)
    {
        VM.SelectDarkTheme();
    }

    private void OnLightThemeClick(object? sender, RoutedEventArgs e)
    {
        VM.SelectLightTheme();
    }

    private void OnGrayThemeClick(object? sender, RoutedEventArgs e)
    {
        VM.SelectGrayTheme();
    }
}
