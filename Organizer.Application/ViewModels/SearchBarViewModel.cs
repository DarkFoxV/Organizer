using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Organizer.Application.Services;

namespace Organizer.Application.ViewModels.Components;

public enum SortOrder
{
    MaisRecente,
    MaisAntigo,
    AlfabeticoAZ,
    AlfabeticoZA
}

public partial class SearchBarViewModel : ObservableObject, IDisposable
{
    private const int SearchDebounceMilliseconds = 300;
    private CancellationTokenSource? _searchCts;

    [ObservableProperty] private string _query = string.Empty;

    [ObservableProperty] private SortOrder _selectedSort = SortOrder.MaisRecente;

    [ObservableProperty] private SortOptionViewModel? _selectedSortOption;

    public ObservableCollection<SortOptionViewModel> SortOptions { get; } = [];

    public event Action<string, SortOrder>? SearchRequested;
    public event Action? RegisterRequested;

    public SearchBarViewModel()
    {
        RefreshSortOptions();
    }

    partial void OnQueryChanged(string value)
    {
        ScheduleSearch();
    }

    partial void OnSelectedSortChanged(SortOrder value)
    {
        ScheduleSearch();
    }

    partial void OnSelectedSortOptionChanged(SortOptionViewModel? value)
    {
        if (value is null || SelectedSort == value.Value)
            return;

        SelectedSort = value.Value;
    }

    public void RefreshSortOptions()
    {
        var selected = SelectedSortOption?.Value ?? SelectedSort;

        SortOptions.Clear();
        SortOptions.Add(new(AppPreferencesService.Translate("Loc.Search.Sort.Newest"), SortOrder.MaisRecente));
        SortOptions.Add(new(AppPreferencesService.Translate("Loc.Search.Sort.Oldest"), SortOrder.MaisAntigo));
        SortOptions.Add(new(AppPreferencesService.Translate("Loc.Search.Sort.AlphabeticalAZ"), SortOrder.AlfabeticoAZ));
        SortOptions.Add(new(AppPreferencesService.Translate("Loc.Search.Sort.AlphabeticalZA"), SortOrder.AlfabeticoZA));

        SelectedSortOption = SortOptions.FirstOrDefault(option => option.Value == selected) ?? SortOptions.FirstOrDefault();
    }

    private async void ScheduleSearch()
    {
        var previousCts = _searchCts;
        var cts = new CancellationTokenSource();
        _searchCts = cts;
        previousCts?.Cancel();

        try
        {
            await Task.Delay(SearchDebounceMilliseconds, cts.Token);

            if (ReferenceEquals(_searchCts, cts))
                SearchRequested?.Invoke(Query, SelectedSort);
        }
        catch (TaskCanceledException)
        {
            // ignorado
        }
        finally
        {
            if (!ReferenceEquals(_searchCts, cts))
                cts.Dispose();
        }
    }

    public void Dispose()
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = null;
    }

    [RelayCommand]
    private void Register()
    {
        RegisterRequested?.Invoke();
    }
}

public sealed record SortOptionViewModel(string Label, SortOrder Value)
{
    public override string ToString() => Label;
}
