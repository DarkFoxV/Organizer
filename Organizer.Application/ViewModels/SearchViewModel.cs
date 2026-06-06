using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Organizer.Application.ViewModels.Components;
using Organize.Organizer.Core.Enums;
using Organize.Organizer.Core.Interfaces;
using Organizer.Application.Services;

namespace Organizer.Application.ViewModels;

public partial class SearchViewModel : ObservableObject, IDisposable
{
    private readonly IImageService _imageService;
    private readonly ITagService _tagService;
    private readonly AppPreferencesService _preferencesService;
    private readonly IToastService _toastService;
    private int _loadVersion;
    private bool _isDisposed;

    [ObservableProperty] private bool _tagsLoaded;

    // ── Componentes ───────────────────────────────────────────────────────────
    public SearchBarViewModel SearchBar { get; } = new();
    public PaginationViewModel Pagination { get; } = new();
    public ImagePreviewViewModel Preview { get; } = new();
    public GroupCopyPickerViewModel CopyPicker { get; }
    public TagSelectorViewModel TagSelector { get; }
    public SearchResultsViewModel Results { get; }
    public DeleteConfirmationViewModel DeleteConfirmation { get; }

    // ── Estado ────────────────────────────────────────────────────────────────
    public string ResultSummary => Results.TotalResults == 1
        ? _preferencesService.T("Loc.Search.ResultCountOne")
        : _preferencesService.T("Loc.Search.ResultCountMany", Results.TotalResults);

    // ── Evento de navegação ───────────────────────────────────────────────────
    public event Action? RegisterRequested;
    public event Action<CardItemViewModel>? EditRequested;

    // ── Init ──────────────────────────────────────────────────────────────────
    public SearchViewModel(
        IImageService imageService,
        ITagService tagService,
        AppPreferencesService preferencesService,
        IToastService toastService,
        SearchResultsViewModel results,
        DeleteConfirmationViewModel deleteConfirmation)
    {
        _imageService = imageService;
        _tagService = tagService;
        _preferencesService = preferencesService;
        _toastService = toastService;
        Results = results;
        DeleteConfirmation = deleteConfirmation;

        CopyPicker = new GroupCopyPickerViewModel(_preferencesService);
        TagSelector = new TagSelectorViewModel(_tagService, _preferencesService, showAddButton: false);

        Results.ViewRequested += OnViewCard;
        Results.EditRequested += OnEditCard;
        Results.DeleteRequested += OnDeleteCard;
        Results.CopyRequested += OnCopyCard;
        Results.PropertyChanged += OnResultsPropertyChanged;
        DeleteConfirmation.Confirmed += OnDeleteConfirmed;
        SearchBar.SearchRequested += OnSearch;
        SearchBar.RegisterRequested += OnRegister;
        Pagination.PageChanged += OnPageChanged;
        TagSelector.SelectionChanged += OnTagSelectionChanged;
        _preferencesService.PreferencesChanged += OnPreferencesChanged;

        _ = LoadResultsAsync();
        _ = LoadTagsAsync();
    }

    private async Task LoadTagsAsync()
    {
        try
        {
            await TagSelector.LoadAsync();
            if (_isDisposed)
                return;

            TagsLoaded = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LoadTagsAsync] {ex}");
            TagsLoaded = false;
        }
    }
    
    public async Task ReloadAsync()
    {
        if (_isDisposed)
            return;

        await LoadTagsAsync();
        await LoadResultsAsync(SearchBar.Query, Pagination.CurrentPage, SearchBar.SelectedSort);
    }

    public void Deactivate()
    {
        _loadVersion++;
        Results.Clear();
        Preview.CloseWithoutMemoryCompaction();
        CopyPicker.CloseWithoutMemoryCompaction();
        DeleteConfirmation.Cancel();
    }

    // ── Handlers ─────────────────────────────────────────────────────────────
    private void OnSearch(string query, SortOrder sort)
    {
        if (_isDisposed)
            return;

        Pagination.CurrentPage = 0;
        _ = LoadResultsAsync(query, 0, sort);
    }

    private void OnTagSelectionChanged()
    {
        if (_isDisposed)
            return;

        Pagination.CurrentPage = 0;
        _ = LoadResultsAsync(SearchBar.Query, 0, SearchBar.SelectedSort);
    }

    private void OnPageChanged(int page)
    {
        if (_isDisposed)
            return;

        _ = LoadResultsAsync(SearchBar.Query, page, SearchBar.SelectedSort);
    }

    private void OnRegister() => RegisterRequested?.Invoke();

    private void OnPreferencesChanged(
        object? sender,
        AppPreferencesChangedEventArgs e)
    {
        if (_isDisposed)
            return;

        if (e.LanguageChanged)
        {
            OnPropertyChanged(nameof(ResultSummary));
            SearchBar.RefreshSortOptions();
        }

        if (e.SearchItemsPerPageChanged)
        {
            Pagination.CurrentPage = 0;
            _ = LoadResultsAsync(SearchBar.Query, 0, SearchBar.SelectedSort);
        }
    }

    // ── Card actions ──────────────────────────────────────────────────────────
    private void OnViewCard(CardItemViewModel card) => _ = ViewCardAsync(card);

    private async Task ViewCardAsync(CardItemViewModel card)
    {
        var loadVersion = _loadVersion;

        try
        {
            var imageIds = await _imageService.GetIdsByCardAsync(card.CardId);
            if (_isDisposed || loadVersion != _loadVersion)
                return;

            if (imageIds.Count == 0)
            {
                return;
            }

            await Preview.OpenAsync(imageIds, _imageService.GetDataAsync);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OnViewCard] {ex}");
        }
    }

    private void OnEditCard(CardItemViewModel card)
    {
        EditRequested?.Invoke(card);
    }

    private void OnDeleteCard(CardItemViewModel card)
    {
        if (_preferencesService.Current.ConfirmDeletion)
        {
            DeleteConfirmation.Request(card);
            return;
        }

        _ = DeleteCardAsync(card);
    }

    private void OnDeleteConfirmed(CardItemViewModel card) => _ = DeleteCardAsync(card);

    private Task DeleteCardAsync(CardItemViewModel card) => Results.DeleteAsync(card);

    private void OnCopyCard(CardItemViewModel card) => _ = CopyCardAsync(card);

    private async Task CopyCardAsync(CardItemViewModel card)
    {
        var loadVersion = _loadVersion;

        try
        {
            if (!card.IsGroup)
                return;

            var images = await _imageService.GetGroupImageSummariesAsync(card.CardId);
            if (_isDisposed || loadVersion != _loadVersion)
                return;

            if (images.Count == 0)
            {
                _toastService.Warning(
                    _preferencesService.T("Loc.Search.ToastCopyUnavailableTitle"),
                    _preferencesService.T("Loc.Search.ToastCopyUnavailableMessage"));
                return;
            }

            await CopyPicker.OpenAsync(images, _imageService.GetDataAsync);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OnCopyCard] {ex}");
            _toastService.Error(
                _preferencesService.T("Loc.Search.ToastCopyFailedTitle"),
                _preferencesService.T("Loc.Search.ToastCopyFailedMessage"));
        }
    }

    // ── Carga de dados ────────────────────────────────────────────────────────
    private async Task LoadResultsAsync(
        string query = "",
        int page = 0,
        SortOrder sort = SortOrder.MaisRecente)
    {
        if (_isDisposed)
            return;

        var loadVersion = ++_loadVersion;
        var itemsPerPage = _preferencesService.Current.SearchItemsPerPage;
        var result = await Results.LoadAsync(
            query,
            page,
            sort,
            itemsPerPage,
            TagSelector.SelectedTags.Select(tag => tag.Id).ToArray());

        if (result is null || _isDisposed || loadVersion != _loadVersion)
            return;

        UpdatePagination(page, result);
    }

    private void UpdatePagination(int page, SearchResultsLoadResult result)
    {
        Pagination.TotalPages = result.TotalPages;
        Pagination.CurrentPage = page;
    }

    private void OnResultsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SearchResultsViewModel.TotalResults))
            OnPropertyChanged(nameof(ResultSummary));
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _loadVersion++;

        Results.ViewRequested -= OnViewCard;
        Results.EditRequested -= OnEditCard;
        Results.DeleteRequested -= OnDeleteCard;
        Results.CopyRequested -= OnCopyCard;
        Results.PropertyChanged -= OnResultsPropertyChanged;
        DeleteConfirmation.Confirmed -= OnDeleteConfirmed;
        SearchBar.SearchRequested -= OnSearch;
        SearchBar.RegisterRequested -= OnRegister;
        Pagination.PageChanged -= OnPageChanged;
        TagSelector.SelectionChanged -= OnTagSelectionChanged;
        _preferencesService.PreferencesChanged -= OnPreferencesChanged;

        Results.Dispose();
        DeleteConfirmation.Dispose();
        Preview.Dispose();
        CopyPicker.Dispose();
        TagSelector.Dispose();
        SearchBar.Dispose();
    }
}
