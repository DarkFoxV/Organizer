using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Organizer.Application.ViewModels.Components;
using Organize.Organizer.Core.Enums;
using Organize.Organizer.Core.Interfaces;
using Organizer.Application.Services;
using Organizer.Core.Helpers;

namespace Organizer.Application.ViewModels;

public partial class SearchViewModel : ObservableObject
{
    private readonly ICardService _cardService;
    private readonly IImageService _imageService;
    private readonly ITagService _tagService;
    private readonly AppPreferencesService _preferencesService;
    private int _loadVersion;

    [ObservableProperty] private bool _isEmpty;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _tagsLoaded;
    [ObservableProperty] private bool _isDeleteConfirmationVisible;
    [ObservableProperty] private CardItemViewModel? _pendingDeleteCard;

    // ── Componentes ───────────────────────────────────────────────────────────
    public SearchBarViewModel SearchBar { get; } = new();
    public PaginationViewModel Pagination { get; } = new();
    public ImagePreviewViewModel Preview { get; } = new();
    public GroupCopyPickerViewModel CopyPicker { get; }
    public TagSelectorViewModel TagSelector { get; }

    // ── Estado ────────────────────────────────────────────────────────────────
    public ObservableCollection<CardItemViewModel> Cards { get; } = [];

    public string DeleteConfirmationTitle => PendingDeleteCard?.IsGroup == true
        ? _preferencesService.T("Loc.Search.DeleteGroupTitle")
        : _preferencesService.T("Loc.Search.DeleteImageTitle");

    public string DeleteConfirmationMessage
    {
        get
        {
            if (PendingDeleteCard is null)
                return string.Empty;

            return PendingDeleteCard.IsGroup
                ? _preferencesService.T(
                    "Loc.Search.DeleteGroupMessage",
                    PendingDeleteCard.Filename,
                    PendingDeleteCard.ImageCount)
                : _preferencesService.T("Loc.Search.DeleteImageMessage", PendingDeleteCard.Filename);
        }
    }

    // ── Evento de navegação ───────────────────────────────────────────────────
    public event Action? RegisterRequested;
    public event Action<CardItemViewModel>? EditRequested;

    // ── Init ──────────────────────────────────────────────────────────────────
    public SearchViewModel(
        ICardService cardService,
        IImageService imageService,
        ITagService tagService,
        AppPreferencesService preferencesService)
    {
        _cardService = cardService;
        _imageService = imageService;
        _tagService = tagService;
        _preferencesService = preferencesService;

        CopyPicker = new GroupCopyPickerViewModel(_preferencesService);
        TagSelector = new TagSelectorViewModel(_tagService, _preferencesService, showAddButton: false);

        SearchBar.SearchRequested += OnSearch;
        SearchBar.RegisterRequested += OnRegister;
        Pagination.PageChanged += OnPageChanged;
        TagSelector.SelectionChanged += OnTagSelectionChanged;
        _preferencesService.PreferencesChanged += OnPreferencesChanged;

        _ = LoadCardsAsync();
        _ = LoadTagsAsync();
    }

    private async Task LoadTagsAsync()
    {
        await TagSelector.LoadAsync();
        TagsLoaded = true;
    }


    // Chamado pelo MainWindowViewModel após salvar um registro novo
    public async Task ReloadAsync()
    {
        await LoadTagsAsync();
        await LoadCardsAsync(SearchBar.Query, Pagination.CurrentPage, SearchBar.SelectedSort);
    }

    // ── Handlers ─────────────────────────────────────────────────────────────
    private void OnSearch(string query, SortOrder sort)
    {
        Pagination.CurrentPage = 0;
        _ = LoadCardsAsync(query, 0, sort);
    }

    private void OnTagSelectionChanged()
    {
        Pagination.CurrentPage = 0;
        _ = LoadCardsAsync(SearchBar.Query, 0, SearchBar.SelectedSort);
    }

    private void OnPageChanged(int page) =>
        _ = LoadCardsAsync(SearchBar.Query, page, SearchBar.SelectedSort);

    private void OnRegister() => RegisterRequested?.Invoke();

    private void OnPreferencesChanged()
    {
        OnPropertyChanged(nameof(DeleteConfirmationTitle));
        OnPropertyChanged(nameof(DeleteConfirmationMessage));
        Pagination.CurrentPage = 0;
        _ = LoadCardsAsync(SearchBar.Query, 0, SearchBar.SelectedSort);
    }

    // ── Card actions ──────────────────────────────────────────────────────────
    private void SubscribeCard(CardItemViewModel card)
    {
        card.ViewRequested += OnViewCard;
        card.EditRequested += OnEditCard;
        card.DeleteRequested += OnDeleteCard;
        card.CopyRequested += OnCopyCard;
    }

    private void UnsubscribeCard(CardItemViewModel card)
    {
        card.ViewRequested -= OnViewCard;
        card.EditRequested -= OnEditCard;
        card.DeleteRequested -= OnDeleteCard;
        card.CopyRequested -= OnCopyCard;
    }

    private async void OnViewCard(CardItemViewModel card)
    {
        try
        {
            var imageIds = await _imageService.GetIdsByCardAsync(card.CardId);

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

    private async void OnDeleteCard(CardItemViewModel card)
    {
        if (_preferencesService.Current.ConfirmDeletion)
        {
            PendingDeleteCard = card;
            IsDeleteConfirmationVisible = true;
            OnPropertyChanged(nameof(DeleteConfirmationTitle));
            OnPropertyChanged(nameof(DeleteConfirmationMessage));
            return;
        }

        await DeleteCardAsync(card);
    }

    [RelayCommand]
    private async Task ConfirmDelete()
    {
        if (PendingDeleteCard is null)
            return;

        var card = PendingDeleteCard;
        PendingDeleteCard = null;
        IsDeleteConfirmationVisible = false;
        OnPropertyChanged(nameof(DeleteConfirmationTitle));
        OnPropertyChanged(nameof(DeleteConfirmationMessage));

        await DeleteCardAsync(card);
    }

    [RelayCommand]
    private void CancelDelete()
    {
        PendingDeleteCard = null;
        IsDeleteConfirmationVisible = false;
        OnPropertyChanged(nameof(DeleteConfirmationTitle));
        OnPropertyChanged(nameof(DeleteConfirmationMessage));
    }

    private async Task DeleteCardAsync(CardItemViewModel card)
    {
        await _cardService.DeleteAsync(card.CardId);

        Cards.Remove(card);
        card.ReleaseResources();
        IsEmpty = Cards.Count == 0;
    }

    private async void OnCopyCard(CardItemViewModel card)
    {
        try
        {
            if (!card.IsGroup)
                return;

            var images = await _imageService.GetGroupImageSummariesAsync(card.CardId);
            if (images.Count == 0)
                return;

            await CopyPicker.OpenAsync(images, _imageService.GetDataAsync);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OnCopyCard] {ex}");
        }
    }

    // ── Carga de dados ────────────────────────────────────────────────────────
    private async Task LoadCardsAsync(
        string query = "",
        int page = 0,
        SortOrder sort = SortOrder.MaisRecente)
    {
        var loadVersion = ++_loadVersion;
        IsLoading = true;

        try
        {
            var selectedTagIds = TagSelector.SelectedTags
                .Select(tag => tag.Id)
                .ToArray();

            var (cards, total) =
                await _imageService.SearchCardsAsync(
                    query,
                    selectedTagIds,
                    sort,
                    page,
                    _preferencesService.Current.SearchItemsPerPage);

            if (loadVersion != _loadVersion)
                return;

            Pagination.TotalPages =
                (int)Math.Ceiling(total / (double)_preferencesService.Current.SearchItemsPerPage);

            Pagination.CurrentPage = page;

            var cardViewModels = await Task.Run(() =>
            {
                return cards.Select(card =>
                {
                    var thumbnail = ImageHelper.ToBitmap(card.CoverThumbnail, maxWidth: 204, maxHeight: 164);

                    return new CardItemViewModel
                    {
                        Id = card.CoverImageId ?? 0,
                        CardId = card.CardId,
                        Thumbnail = thumbnail,
                        Filename = card.CoverFilename,
                        MimeType = card.CoverMimeType ?? "application/octet-stream",
                        Description = card.CoverDescription,
                        CreatedAt = card.CreatedAt.ToString("dd/MM/yyyy"),
                        LoadImageDataAsync = card.CoverImageId is null
                            ? null
                            : () => _imageService.GetDataAsync(card.CoverImageId.Value),
                        IsGroup = card.CardType == CardType.Group,
                        ImageCount = card.ImageCount,
                        IsLoaded = thumbnail is not null
                    };
                }).ToList();
            });

            if (loadVersion != _loadVersion)
            {
                foreach (var vm in cardViewModels)
                    vm.ReleaseResources();

                return;
            }

            foreach (var existingCard in Cards)
            {
                UnsubscribeCard(existingCard);
                existingCard.ReleaseResources();
            }

            Cards.Clear();

            foreach (var vm in cardViewModels)
            {
                SubscribeCard(vm);
                Cards.Add(vm);
            }

            IsEmpty = Cards.Count == 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LoadCardsAsync] {ex}");

            IsEmpty = true;
        }
        finally
        {
            IsLoading = false;
        }
    }
}
