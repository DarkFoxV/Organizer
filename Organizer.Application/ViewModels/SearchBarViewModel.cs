using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Organizer.Application.ViewModels.Components;

public enum SortOrder
{
    MaisRecente,
    MaisAntigo,
    AlfabeticoAZ,
    AlfabeticoZA
}

public partial class SearchBarViewModel : ObservableObject
{
    private CancellationTokenSource? _searchCts;

    [ObservableProperty]
    private string _query = string.Empty;

    [ObservableProperty]
    private SortOrder _selectedSort = SortOrder.MaisRecente;

    public ObservableCollection<SortOrder> SortOptions { get; } =
        new(Enum.GetValues<SortOrder>());

    public event Action<string, SortOrder>? SearchRequested;
    public event Action? RegisterRequested;

    partial void OnQueryChanged(string value)
    {
        DebounceSearch();
    }

    partial void OnSelectedSortChanged(SortOrder value)
    {
        DebounceSearch();
    }

    private async void DebounceSearch()
    {
        _searchCts?.Cancel();

        var cts = new CancellationTokenSource();

        _searchCts = cts;

        try
        {
            await Task.Delay(400, cts.Token);

            if (cts.IsCancellationRequested)
                return;

            SearchRequested?.Invoke(Query, SelectedSort);
        }
        catch (TaskCanceledException)
        {
            // ignorado
        }
    }

    [RelayCommand]
    private void Register()
    {
        RegisterRequested?.Invoke();
    }
}