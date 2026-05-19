using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Organizer.Organizer.App.Enums;

namespace Organizer.Application.ViewModels;

public partial class NavbarViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isNavigationEnabled = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(IsHomeSelected),
        nameof(IsSearchSelected),
        nameof(IsWorkspaceSelected),
        nameof(IsManageTagsSelected),
        nameof(IsPreferencesSelected))]
    private NavButton _selected = NavButton.Search;

    public bool IsHomeSelected => Selected == NavButton.Home;
    public bool IsSearchSelected => Selected == NavButton.Search;
    public bool IsWorkspaceSelected => Selected == NavButton.Workspace;
    public bool IsManageTagsSelected => Selected == NavButton.ManageTags;
    public bool IsPreferencesSelected => Selected == NavButton.Preferences;

    public event Action<NavButton>? NavigationRequested;

    [RelayCommand]
    private void Navigate(NavButton button)
    {
        if (!IsNavigationEnabled) return;

        Selected = button;
        NavigationRequested?.Invoke(button);
    }
}
