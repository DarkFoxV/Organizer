using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Organizer.Application.Services;

namespace Organizer.Application.ViewModels.Components;

public partial class DeleteConfirmationViewModel : ObservableObject, IDisposable
{
    private readonly AppPreferencesService _preferencesService;
    private bool _isDisposed;

    [ObservableProperty] private bool _isVisible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    [NotifyPropertyChangedFor(nameof(Message))]
    private CardItemViewModel? _pendingCard;

    public string Title => PendingCard?.IsGroup == true
        ? _preferencesService.T("Loc.Search.DeleteGroupTitle")
        : _preferencesService.T("Loc.Search.DeleteImageTitle");

    public string Message
    {
        get
        {
            if (PendingCard is null)
                return string.Empty;

            return PendingCard.IsGroup
                ? _preferencesService.T(
                    "Loc.Search.DeleteGroupMessage",
                    PendingCard.Filename,
                    PendingCard.ImageCount)
                : _preferencesService.T("Loc.Search.DeleteImageMessage", PendingCard.Filename);
        }
    }

    public event Action<CardItemViewModel>? Confirmed;

    public DeleteConfirmationViewModel(AppPreferencesService preferencesService)
    {
        _preferencesService = preferencesService;
        _preferencesService.PreferencesChanged += OnPreferencesChanged;
    }

    public void Request(CardItemViewModel card)
    {
        if (_isDisposed)
            return;

        PendingCard = card;
        IsVisible = true;
    }

    [RelayCommand]
    private void Confirm()
    {
        if (PendingCard is not { } card)
            return;

        Clear();
        Confirmed?.Invoke(card);
    }

    [RelayCommand]
    public void Cancel() => Clear();

    private void Clear()
    {
        PendingCard = null;
        IsVisible = false;
    }

    private void OnPreferencesChanged(
        object? sender,
        AppPreferencesChangedEventArgs e)
    {
        if (!e.LanguageChanged)
            return;

        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Message));
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        Clear();
        _preferencesService.PreferencesChanged -= OnPreferencesChanged;
    }
}
