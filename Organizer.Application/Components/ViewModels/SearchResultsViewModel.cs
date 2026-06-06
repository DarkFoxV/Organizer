using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Organize.Organizer.Core;
using Organize.Organizer.Core.Enums;
using Organize.Organizer.Core.Interfaces;
using Organizer.Application.Services;
using Organizer.Application.ViewModels.Components;

namespace Organizer.Application.ViewModels;

public partial class SearchResultsViewModel : ObservableObject, IDisposable
{
    private readonly ICardService _cardService;
    private readonly IImageService _imageService;
    private readonly CardItemViewModelFactory _cardFactory;
    private readonly AppPreferencesService _preferencesService;
    private readonly IToastService _toastService;
    private int _loadVersion;
    private bool _isDisposed;

    [ObservableProperty] private bool _isEmpty;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private int _totalResults;

    public ObservableCollection<CardItemViewModel> Cards { get; } = [];

    public event Action<CardItemViewModel>? ViewRequested;
    public event Action<CardItemViewModel>? EditRequested;
    public event Action<CardItemViewModel>? DeleteRequested;
    public event Action<CardItemViewModel>? CopyRequested;

    public SearchResultsViewModel(
        ICardService cardService,
        IImageService imageService,
        CardItemViewModelFactory cardFactory,
        AppPreferencesService preferencesService,
        IToastService toastService)
    {
        _cardService = cardService;
        _imageService = imageService;
        _cardFactory = cardFactory;
        _preferencesService = preferencesService;
        _toastService = toastService;
    }

    public async Task<SearchResultsLoadResult?> LoadAsync(
        string query,
        int page,
        SortOrder sort,
        int itemsPerPage,
        IReadOnlyCollection<int> selectedTagIds)
    {
        if (_isDisposed)
            return null;

        var loadVersion = ++_loadVersion;
        IsLoading = true;

        try
        {
            var (cards, totalResults) = await LoadSearchResultsAsync(
                query,
                selectedTagIds,
                sort,
                page,
                itemsPerPage);

            if (_isDisposed || loadVersion != _loadVersion)
                return null;

            // Bitmap creation stays on the caller/UI context to avoid relying on
            // Avalonia graphics objects being safe to construct on worker threads.
            var cardViewModels = BuildCardViewModels(cards);

            if (_isDisposed || loadVersion != _loadVersion)
            {
                ReleaseCards(cardViewModels);
                return null;
            }

            ReplaceCards(cardViewModels);
            UpdateResultState(totalResults);

            var totalPages = (int)Math.Ceiling(totalResults / (double)itemsPerPage);
            return new SearchResultsLoadResult(totalResults, totalPages);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SearchResults.LoadAsync] {ex}");

            if (!_isDisposed && loadVersion == _loadVersion)
                SetLoadErrorState();

            return null;
        }
        finally
        {
            if (!_isDisposed && loadVersion == _loadVersion)
                IsLoading = false;
        }
    }

    private Task<(List<SearchCardResult> Cards, int TotalCount)> LoadSearchResultsAsync(
        string query,
        IReadOnlyCollection<int> selectedTagIds,
        SortOrder sort,
        int page,
        int itemsPerPage) =>
        _imageService.SearchCardsAsync(query, selectedTagIds, sort, page, itemsPerPage);

    public async Task DeleteAsync(CardItemViewModel card)
    {
        try
        {
            await _cardService.DeleteAsync(card.CardId);

            var isGroup = card.IsGroup;
            var hadLargeImageResources = isGroup || card.ImageData is { Length: >= 85_000 };

            Cards.Remove(card);
            UnsubscribeCard(card);
            card.ReleaseResources();
            TotalResults = Math.Max(0, TotalResults - 1);
            IsEmpty = Cards.Count == 0;

            if (hadLargeImageResources)
                MemoryCleanupService.QueueLargeImageMemoryCompaction();

            _toastService.Success(
                _preferencesService.T("Loc.Search.ToastCardDeletedTitle"),
                isGroup
                    ? _preferencesService.T("Loc.Search.ToastGroupDeletedMessage")
                    : _preferencesService.T("Loc.Search.ToastImageDeletedMessage"));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SearchResults.DeleteAsync] {ex}");
            _toastService.Error(
                _preferencesService.T("Loc.Search.ToastDeleteFailedTitle"),
                _preferencesService.T("Loc.Search.ToastDeleteFailedMessage"));
        }
    }

    public void Clear()
    {
        if (_isDisposed)
            return;

        _loadVersion++;
        var hadImageResources = Cards.Count > 0;

        ClearCards();
        TotalResults = 0;
        IsEmpty = true;
        IsLoading = false;

        if (hadImageResources)
            MemoryCleanupService.QueueLargeImageMemoryCompaction();
    }

    private List<CardItemViewModel> BuildCardViewModels(IEnumerable<SearchCardResult> cards)
    {
        var viewModels = new List<CardItemViewModel>();

        try
        {
            foreach (var card in cards)
                viewModels.Add(_cardFactory.Create(card));

            return viewModels;
        }
        catch
        {
            ReleaseCards(viewModels);
            throw;
        }
    }

    private void ReplaceCards(IEnumerable<CardItemViewModel> cards)
    {
        ClearCards();

        foreach (var card in cards)
        {
            SubscribeCard(card);
            Cards.Add(card);
        }
    }

    private void UpdateResultState(int totalResults)
    {
        TotalResults = totalResults;
        IsEmpty = Cards.Count == 0;
    }

    private void SetLoadErrorState()
    {
        TotalResults = 0;
        IsEmpty = true;
    }

    private void ClearCards()
    {
        var existingCards = Cards.ToList();
        Cards.Clear();

        foreach (var card in existingCards)
        {
            UnsubscribeCard(card);
            card.ReleaseResources();
        }
    }

    private static void ReleaseCards(IEnumerable<CardItemViewModel> cards)
    {
        foreach (var card in cards)
            card.ReleaseResources();
    }

    private void SubscribeCard(CardItemViewModel card)
    {
        card.ViewRequested += OnViewRequested;
        card.EditRequested += OnEditRequested;
        card.DeleteRequested += OnDeleteRequested;
        card.CopyRequested += OnCopyRequested;
    }

    private void UnsubscribeCard(CardItemViewModel card)
    {
        card.ViewRequested -= OnViewRequested;
        card.EditRequested -= OnEditRequested;
        card.DeleteRequested -= OnDeleteRequested;
        card.CopyRequested -= OnCopyRequested;
    }

    private void OnViewRequested(CardItemViewModel card) => ViewRequested?.Invoke(card);
    private void OnEditRequested(CardItemViewModel card) => EditRequested?.Invoke(card);
    private void OnDeleteRequested(CardItemViewModel card) => DeleteRequested?.Invoke(card);
    private void OnCopyRequested(CardItemViewModel card) => CopyRequested?.Invoke(card);

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _loadVersion++;
        ClearCards();
    }
}

public sealed record SearchResultsLoadResult(int TotalResults, int TotalPages);
