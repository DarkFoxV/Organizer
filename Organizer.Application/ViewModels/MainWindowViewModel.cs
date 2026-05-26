using System;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Organizer.Organizer.App.Enums;

namespace Organizer.Application.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly IServiceProvider _services;
    private RegisterViewModel? _activeRegisterViewModel;
    private IServiceScope? _activeRegisterScope;
    private EditViewModel? _activeEditViewModel;
    private readonly WorkspaceViewModel _workspaceViewModel;

    [ObservableProperty] private ObservableObject _currentView;

    [ObservableProperty] private bool _isGlobalLoading;

    [ObservableProperty] private string _globalLoadingText = "Carregando, aguarde...";

    public NavbarViewModel Navbar { get; }

    public bool HasUnsavedWorkspaceChanges => _workspaceViewModel.HasUnsavedChanges;

    public bool HasFileBackedWorkspace => _workspaceViewModel.HasWorkspaceFile;

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
        DetachEdit();

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

        DetachRegister();
        DetachEdit();

        _activeRegisterScope = _services.CreateScope();
        var registerVm = _activeRegisterScope.ServiceProvider.GetRequiredService<RegisterViewModel>();
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
        DetachEdit();

        var editVm = _services.GetRequiredService<EditViewModel>();
        editVm.CloseRequested += GoBackToSearch;
        editVm.SubmitSuccess += GoBackToSearch;
        await editVm.LoadAsync(card);
        _activeEditViewModel = editVm;
        CurrentView = editVm;
    }

    private void GoBackToSearch()
    {
        DetachRegister();
        DetachEdit();
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
        _activeRegisterViewModel.Dispose();
        _activeRegisterViewModel = null;
        _activeRegisterScope?.Dispose();
        _activeRegisterScope = null;

        IsGlobalLoading = false;
        GlobalLoadingText = "Carregando, aguarde...";
        Navbar.IsNavigationEnabled = true;
    }

    private void DetachEdit()
    {
        if (_activeEditViewModel is null) return;

        _activeEditViewModel.CloseRequested -= GoBackToSearch;
        _activeEditViewModel.SubmitSuccess -= GoBackToSearch;
        _activeEditViewModel.Dispose();
        _activeEditViewModel = null;
    }

    public Task<bool> SaveWorkspaceToCurrentFileAsync()
    {
        return _workspaceViewModel.SaveToCurrentFileAsync();
    }

    public Task<bool> SaveWorkspaceToFileAsync(IStorageFile file)
    {
        return _workspaceViewModel.SaveToFileAsync(file);
    }
}
