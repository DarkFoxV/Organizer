using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Organizer.Organizer.App.Enums;

namespace Organizer.Application.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly IServiceProvider _services;
    private RegisterViewModel? _activeRegisterViewModel;
    private readonly WorkspaceViewModel _workspaceViewModel;

    [ObservableProperty] private ObservableObject _currentView;

    [ObservableProperty] private bool _isGlobalLoading;

    [ObservableProperty] private string _globalLoadingText = "Carregando, aguarde...";

    public NavbarViewModel Navbar { get; }

    // Guarda a SearchView pra reutilizar ao voltar
    private readonly SearchViewModel _searchViewModel;

    public MainWindowViewModel(SearchViewModel searchViewModel,
        NavbarViewModel navbar,
        IServiceProvider services)
    {
        _services = services;
        _searchViewModel = searchViewModel;
        _workspaceViewModel = _services.GetRequiredService<WorkspaceViewModel>();
        Navbar = navbar;

        _searchViewModel.RegisterRequested += GoToRegister;
        _searchViewModel.EditRequested += GoToEdit;

        _currentView = _searchViewModel;

        Navbar.NavigationRequested += OnNavigate;
    }

    private void OnNavigate(NavButton button)
    {
        if (IsGlobalLoading) return;

        DetachRegister();

        CurrentView = button switch
        {
            NavButton.Home => _services.GetRequiredService<HomeViewModel>(),
            NavButton.Search => _searchViewModel,
            NavButton.Workspace => _workspaceViewModel,
            NavButton.ManageTags => _services.GetRequiredService<ManageTagsViewModel>(),
            NavButton.Preferences => _services.GetRequiredService<PreferencesViewModel>(),
            _ => _searchViewModel
        };

        if (button == NavButton.Search)
            _ = _searchViewModel.ReloadAsync();
    }

    private void GoToRegister()
    {
        if (IsGlobalLoading) return;

        var registerVm = _services.GetRequiredService<RegisterViewModel>();
        registerVm.CloseRequested += GoBackToSearch;
        registerVm.SubmitSuccess += GoBackToSearch;
        registerVm.BusyStateChanged += OnRegisterBusyStateChanged;
        _activeRegisterViewModel = registerVm;
        CurrentView = registerVm;
    }

    private async void GoToEdit(Components.CardItemViewModel card)
    {
        if (IsGlobalLoading) return;

        DetachRegister();

        var editVm = _services.GetRequiredService<EditViewModel>();
        editVm.CloseRequested += GoBackToSearch;
        editVm.SubmitSuccess += GoBackToSearch;
        await editVm.LoadAsync(card);
        CurrentView = editVm;
    }

    private void GoBackToSearch()
    {
        DetachRegister();
        Navbar.Selected = NavButton.Search;
        CurrentView = _searchViewModel;
        _ = _searchViewModel.ReloadAsync();
    }

    private void OnRegisterBusyStateChanged(bool isBusy, string message)
    {
        IsGlobalLoading = isBusy;
        GlobalLoadingText = string.IsNullOrWhiteSpace(message)
            ? "Carregando, aguarde..."
            : message;
        Navbar.IsNavigationEnabled = !isBusy;
    }

    private void DetachRegister()
    {
        if (_activeRegisterViewModel is null) return;

        _activeRegisterViewModel.CloseRequested -= GoBackToSearch;
        _activeRegisterViewModel.SubmitSuccess -= GoBackToSearch;
        _activeRegisterViewModel.BusyStateChanged -= OnRegisterBusyStateChanged;
        _activeRegisterViewModel = null;

        IsGlobalLoading = false;
        GlobalLoadingText = "Carregando, aguarde...";
        Navbar.IsNavigationEnabled = true;
    }
}
