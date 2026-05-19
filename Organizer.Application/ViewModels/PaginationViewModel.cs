using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Organizer.Application.ViewModels.Components;

public partial class PaginationViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoPrevious))]
    [NotifyPropertyChangedFor(nameof(CanGoNext))]
    [NotifyPropertyChangedFor(nameof(Pages))]
    public partial int CurrentPage { get; set; }
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoPrevious))]
    [NotifyPropertyChangedFor(nameof(CanGoNext))]
    [NotifyPropertyChangedFor(nameof(Pages))]
    [NotifyPropertyChangedFor(nameof(ShowPagination))]
    public partial int TotalPages { get; set; }

    public bool ShowPagination => TotalPages > 1;
    public bool CanGoPrevious => CurrentPage > 0;
    public bool CanGoNext => CurrentPage < TotalPages - 1;

    // Janela de páginas visíveis (máx 5)
    public ObservableCollection<PageItem> Pages => BuildPages();

    public event Action<int>? PageChanged;

    partial void OnCurrentPageChanged(int value)
    {
        PreviousCommand.NotifyCanExecuteChanged();
        NextCommand.NotifyCanExecuteChanged();
    }

    partial void OnTotalPagesChanged(int value)
    {
        PreviousCommand.NotifyCanExecuteChanged();
        NextCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanGoPrevious))]
    private void Previous()
    {
        GoTo(CurrentPage - 1);
    }

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private void Next()
    {
        GoTo(CurrentPage + 1);
    }

    [RelayCommand]
    private void GoToPage(int page)
    {
        GoTo(page);
    }

    private void GoTo(int page)
    {
        if (page < 0 || page >= TotalPages) return;
        CurrentPage = page;
        PageChanged?.Invoke(CurrentPage);
    }

    private ObservableCollection<PageItem> BuildPages()
    {
        var items = new ObservableCollection<PageItem>();
        if (TotalPages <= 1) return items;

        var start = Math.Max(0, CurrentPage - 2);
        var end = Math.Min(start + 5, TotalPages);

        // Primeira página + ellipsis
        if (start > 0)
        {
            items.Add(new PageItem(0, "1", false, false));
            if (start > 1)
                items.Add(new PageItem(-1, "...", false, true));
        }

        for (var i = start; i < end; i++)
            items.Add(new PageItem(i, (i + 1).ToString(), i == CurrentPage, false));

        // Ellipsis + última página
        if (end < TotalPages)
        {
            if (end < TotalPages - 1)
                items.Add(new PageItem(-1, "...", false, true));
            items.Add(new PageItem(TotalPages - 1, TotalPages.ToString(), false, false));
        }

        return items;
    }
}

public record PageItem(int Index, string Label, bool IsCurrent, bool IsEllipsis);
