using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.EntityFrameworkCore;
using Organizer.Application.Services;

namespace Organizer.Application.ViewModels;

public sealed partial class HomeViewModel : ObservableObject, IDisposable
{
    private readonly HomeWorkspaceCacheService _homeWorkspaceCacheService;
    private readonly AppDbContextFactory _dbContextFactory;
    private readonly WorkspaceViewModel _workspaceViewModel;
    private readonly AppPreferencesService _preferencesService;
    private readonly List<HomeWorkspaceItemViewModel> _allWorkspaces = [];
    private bool _isDisposed;

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private int _imageTotal;
    [ObservableProperty] private int _workspaceTotal;
    [ObservableProperty] private int _tagTotal;
    [ObservableProperty] private string? _errorMessage;

    public HomeViewModel(
        HomeWorkspaceCacheService homeWorkspaceCacheService,
        AppDbContextFactory dbContextFactory,
        WorkspaceViewModel workspaceViewModel,
        AppPreferencesService preferencesService)
    {
        _homeWorkspaceCacheService = homeWorkspaceCacheService;
        _dbContextFactory = dbContextFactory;
        _workspaceViewModel = workspaceViewModel;
        _preferencesService = preferencesService;
        _homeWorkspaceCacheService.Changed += OnHomeWorkspaceCacheChanged;
        _preferencesService.PreferencesChanged += OnPreferencesChanged;
        _ = RefreshAsync();
    }

    public event Action? WorkspaceOpened;
    public event Action? NewWorkspaceRequested;
    public event Action? ImportImagesRequested;

    public ObservableCollection<HomeWorkspaceItemViewModel> RecentWorkspaces { get; } = [];

    public bool HasRecentWorkspaces => RecentWorkspaces.Count > 0;
    public string ImageTotalText => AppPreferencesService.Translate("Loc.Home.StatImages", ImageTotal);
    public string WorkspaceTotalText => AppPreferencesService.Translate("Loc.Home.StatWorkspaces", WorkspaceTotal);
    public string TagTotalText => AppPreferencesService.Translate("Loc.Home.StatTags", TagTotal);

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    public async Task RefreshAsync()
    {
        if (_isDisposed)
            return;

        try
        {
            ErrorMessage = null;
            _homeWorkspaceCacheService.ValidateStartup();
            RebuildWorkspaceItems();
            await RefreshStatsAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = AppPreferencesService.Translate("Loc.Home.ErrorLoad", ex.Message);
        }
    }

    public async Task OpenWorkspaceAsync(HomeWorkspaceItemViewModel workspace, IStorageProvider storageProvider)
    {
        if (workspace.IsMissing)
        {
            ErrorMessage = AppPreferencesService.Translate("Loc.Home.ErrorFileNotFound");
            return;
        }

        var file = await storageProvider.TryGetFileFromPathAsync(workspace.Path);
        if (file is null)
        {
            _homeWorkspaceCacheService.ValidateStartup();
            RebuildWorkspaceItems();
            ErrorMessage = AppPreferencesService.Translate("Loc.Home.ErrorFileNotFound");
            return;
        }

        var loaded = false;

        try
        {
            await using var stream = await file.OpenReadAsync();
            loaded = await _workspaceViewModel.LoadAsync(stream);
        }
        catch (Exception ex)
        {
            ErrorMessage = AppPreferencesService.Translate("Loc.Home.ErrorOpen", ex.Message);
        }

        if (!loaded)
        {
            file.Dispose();
            ErrorMessage = _workspaceViewModel.ErrorMessage;
            return;
        }

        _workspaceViewModel.SetWorkspaceFile(file);
        await _homeWorkspaceCacheService.RememberAsync(workspace.Path);
        WorkspaceOpened?.Invoke();
    }

    public void NewWorkspace()
    {
        _workspaceViewModel.CloseWorkspace();
        NewWorkspaceRequested?.Invoke();
    }

    public void ImportImages()
    {
        ImportImagesRequested?.Invoke();
    }

    public void RemoveFromHome(HomeWorkspaceItemViewModel workspace)
    {
        _homeWorkspaceCacheService.Remove(workspace.Path);
    }

    public void DeleteWorkspace(HomeWorkspaceItemViewModel workspace)
    {
        _homeWorkspaceCacheService.DeleteWorkspace(workspace.Path);
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _homeWorkspaceCacheService.Changed -= OnHomeWorkspaceCacheChanged;
        _preferencesService.PreferencesChanged -= OnPreferencesChanged;
        ClearWorkspaceItems();
    }

    private void OnHomeWorkspaceCacheChanged()
    {
        if (_isDisposed)
            return;

        RebuildWorkspaceItems();
    }

    private void OnPreferencesChanged()
    {
        if (_isDisposed)
            return;

        RebuildWorkspaceItems();
        OnPropertyChanged(nameof(ImageTotalText));
        OnPropertyChanged(nameof(WorkspaceTotalText));
        OnPropertyChanged(nameof(TagTotalText));
    }

    private void RebuildWorkspaceItems()
    {
        ClearWorkspaceItems();

        foreach (var entry in _homeWorkspaceCacheService.RecentWorkspaces)
        {
            _allWorkspaces.Add(new HomeWorkspaceItemViewModel(
                entry,
                _homeWorkspaceCacheService.GetThumbnailFullPath(entry)));
        }

        WorkspaceTotal = _allWorkspaces.Count;
        OnPropertyChanged(nameof(WorkspaceTotalText));
        ApplyFilter();
    }

    private void ClearWorkspaceItems()
    {
        RecentWorkspaces.Clear();

        foreach (var workspace in _allWorkspaces)
            workspace.Dispose();

        _allWorkspaces.Clear();
    }

    private void ApplyFilter()
    {
        RecentWorkspaces.Clear();

        var query = SearchText.Trim();
        foreach (var workspace in _allWorkspaces.Where(workspace =>
                     string.IsNullOrWhiteSpace(query)
                     || workspace.Name.Contains(query, StringComparison.OrdinalIgnoreCase)))
        {
            RecentWorkspaces.Add(workspace);
        }

        OnPropertyChanged(nameof(HasRecentWorkspaces));
    }

    private async Task RefreshStatsAsync()
    {
        await using var lease = await _dbContextFactory.CreateLeaseAsync();
        ImageTotal = await lease.Context.Images.CountAsync();
        TagTotal = await lease.Context.Tags.CountAsync();
        WorkspaceTotal = _allWorkspaces.Count;
        OnPropertyChanged(nameof(ImageTotalText));
        OnPropertyChanged(nameof(WorkspaceTotalText));
        OnPropertyChanged(nameof(TagTotalText));
    }
}
