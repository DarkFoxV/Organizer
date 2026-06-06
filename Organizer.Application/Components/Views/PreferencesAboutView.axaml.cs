using System;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Organizer.Organizer.Application.Views;

public partial class PreferencesAboutView : UserControl
{
    private const string RepositoryUrl = "https://github.com/DarkFoxV/Organizer";
    private const string IssuesUrl = "https://github.com/DarkFoxV/Organizer/issues";
    private const string LicenseUrl = "https://github.com/DarkFoxV/Organizer/blob/main/LICENSE";

    public PreferencesAboutView()
    {
        InitializeComponent();
    }

    private async void OnRepositoryClick(object? sender, RoutedEventArgs e)
    {
        await OpenUriAsync(RepositoryUrl);
    }

    private async void OnReportBugClick(object? sender, RoutedEventArgs e)
    {
        await OpenUriAsync(IssuesUrl);
    }

    private async void OnLicenseClick(object? sender, RoutedEventArgs e)
    {
        await OpenUriAsync(LicenseUrl);
    }

    private async System.Threading.Tasks.Task OpenUriAsync(string uri)
    {
        if (TopLevel.GetTopLevel(this)?.Launcher is not { } launcher)
            return;

        await launcher.LaunchUriAsync(new Uri(uri));
    }
}
