using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Organizer.Application.ViewModels.Components;
using Organize.Organizer.Core.Enums;
using Organize.Organizer.Core.Interfaces;
using Organizer.Core.Helpers;

namespace Organizer.Application.ViewModels;

public partial class SearchViewModel : ObservableObject
{
    private const int PageSize = 20;
    private readonly ICardService _cardService;
    private readonly IImageService _imageService;
    private readonly ITagService _tagService;
    private int _loadVersion;

    [ObservableProperty] private bool _isEmpty;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _tagsLoaded;

    // ── Componentes ───────────────────────────────────────────────────────────
    public SearchBarViewModel SearchBar { get; } = new();
    public PaginationViewModel Pagination { get; } = new();
    public ImagePreviewViewModel Preview { get; } = new();
    public GroupCopyPickerViewModel CopyPicker { get; } = new();
    public TagSelectorViewModel TagSelector { get; }

    // ── Estado ────────────────────────────────────────────────────────────────
    public ObservableCollection<CardItemViewModel> Cards { get; } = [];

    // ── Evento de navegação ───────────────────────────────────────────────────
    public event Action? RegisterRequested;
    public event Action<CardItemViewModel>? EditRequested;

    // ── Init ──────────────────────────────────────────────────────────────────
    public SearchViewModel(
        ICardService cardService,
        IImageService imageService,
        ITagService tagService)
    {
        _cardService = cardService;
        _imageService = imageService;
        _tagService = tagService;

        TagSelector = new TagSelectorViewModel(_tagService, showAddButton: false);

        SearchBar.SearchRequested += OnSearch;
        SearchBar.RegisterRequested += OnRegister;
        Pagination.PageChanged += OnPageChanged;
        TagSelector.SelectionChanged += OnTagSelectionChanged;

        _ = LoadCardsAsync();
        _ = LoadTagsAsync();
    }

    private async Task LoadTagsAsync()
    {
        await TagSelector.LoadAsync(_tagService);
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

    // ── Card actions ──────────────────────────────────────────────────────────
    private void SubscribeCard(CardItemViewModel card)
    {
        card.ViewRequested += OnViewCard;
        card.EditRequested += OnEditCard;
        card.DeleteRequested += OnDeleteCard;
        card.CopyRequested += OnCopyCard;
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

            var imageIds = await _imageService.GetIdsByCardAsync(card.CardId);
            if (imageIds.Count == 0)
                return;

            await CopyPicker.OpenAsync(imageIds, _imageService.GetDataAsync);
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
                    PageSize);

            if (loadVersion != _loadVersion)
                return;

            Pagination.TotalPages =
                (int)Math.Ceiling(total / (double)PageSize);

            Pagination.CurrentPage = page;

            var cardViewModels = await Task.Run(() =>
            {
                return cards.Select(card =>
                {
                    var thumbnail = ImageHelper.ToBitmap(card.CoverThumbnail);

                    return new CardItemViewModel
                    {
                        Id = card.CoverImageId ?? 0,
                        CardId = card.CardId,
                        Thumbnail = thumbnail,
                        Filename = card.CoverFilename,
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
                existingCard.ReleaseResources();

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