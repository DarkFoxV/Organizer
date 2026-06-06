using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Organizer.Application.Services;

namespace Organizer.Application.ViewModels.Components;

public partial class ImageOrderListViewModel : ObservableObject, System.IDisposable
{
    private readonly AppPreferencesService _preferencesService;
    private bool _isDisposed;

    [ObservableProperty] private bool _isEmpty = true;
    public ObservableCollection<ImageOrderItemViewModel> Items { get; } = [];

    public ImageOrderListViewModel(AppPreferencesService preferencesService)
    {
        _preferencesService = preferencesService;
        _preferencesService.PreferencesChanged += OnPreferencesChanged;
        Items.CollectionChanged += OnItemsChanged;
    }

    public string CountLabel => _preferencesService.T("Loc.ImageOrder.Count", Items.Count);

    public void Add(ImageOrderItemViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (_isDisposed)
        {
            item.Dispose();
            return;
        }

        if (Items.Contains(item))
            return;

        item.RemoveRequested -= Remove;
        item.RemoveRequested += Remove;
        Items.Add(item);
    }

    public void AddRange(IEnumerable<ImageOrderItemViewModel> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        foreach (var item in items)
            Add(item);
    }

    public void Remove(ImageOrderItemViewModel item)
    {
        item.RemoveRequested -= Remove;
        Items.Remove(item);
        item.Dispose();
    }

    public void Move(int fromIndex, int toIndex)
    {
        if (fromIndex == toIndex) return;

        if (fromIndex < 0 || toIndex < 0) return;

        if (fromIndex >= Items.Count || toIndex >= Items.Count) return;

        Items.Move(fromIndex, toIndex);
    }

    private void OnPreferencesChanged(
        object? sender,
        AppPreferencesChangedEventArgs e)
    {
        if (e.LanguageChanged)
            OnPropertyChanged(nameof(CountLabel));
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        IsEmpty = Items.Count == 0;
        OnPropertyChanged(nameof(CountLabel));
    }

    public void ClearItems()
    {
        var items = Items.ToList();
        Items.Clear();

        foreach (var item in items)
        {
            item.RemoveRequested -= Remove;
            item.Dispose();
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _preferencesService.PreferencesChanged -= OnPreferencesChanged;
        ClearItems();
        Items.CollectionChanged -= OnItemsChanged;
    }
}
