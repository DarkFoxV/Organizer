using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using Organizer.Application.Services;

namespace Organizer.Application.ViewModels.Components;

public partial class ImageOrderListViewModel : ObservableObject, System.IDisposable
{
    private readonly AppPreferencesService _preferencesService;
    private bool _isDisposed;

    [ObservableProperty] private bool _isEmpty = true;
    [ObservableProperty] private ObservableCollection<ImageOrderItemViewModel> _items = [];

    public ImageOrderListViewModel(AppPreferencesService preferencesService)
    {
        _preferencesService = preferencesService;
        _preferencesService.PreferencesChanged += OnPreferencesChanged;
        Items.CollectionChanged += OnItemsChanged;
    }

    public string CountLabel => _preferencesService.T("Loc.ImageOrder.Count", Items.Count);

    public async Task AddImageAsync(
        IStorageFile file)
    {
        var vm = new ImageOrderItemViewModel
        {
            Filename = file.Name,
            MimeType = DetectMime(file.Name),
            SourceFile = file
        };

        await AddImageAsync(vm);
    }

    public async Task AddImageAsync(string filename, string mimeType, byte[] data)
    {
        var vm = new ImageOrderItemViewModel
        {
            Filename = filename,
            MimeType = mimeType,
            SourceData = data
        };

        await AddImageAsync(vm);
    }

    private async Task AddImageAsync(ImageOrderItemViewModel vm)
    {
        if (!await PrepareImageAsync(vm))
            return;

        if (_isDisposed)
        {
            vm.Dispose();
            return;
        }

        Items.Add(vm);
    }

    public async Task AddImagesAsync(
        IEnumerable<IStorageFile> files,
        Action<IStorageFile>? ownershipTransferred = null)
    {
        var loadedItems = new List<ImageOrderItemViewModel>();

        try
        {
            foreach (var file in files)
            {
                if (_isDisposed)
                    break;

                var vm = new ImageOrderItemViewModel
                {
                    Filename = file.Name,
                    MimeType = DetectMime(file.Name),
                    SourceFile = file
                };

                ownershipTransferred?.Invoke(file);

                if (await PrepareImageAsync(vm))
                    loadedItems.Add(vm);
            }

            AddLoadedItems(loadedItems);
            loadedItems.Clear();
        }
        finally
        {
            foreach (var item in loadedItems)
                item.Dispose();
        }
    }

    public async Task AddImagesAsync(IEnumerable<(string Filename, string MimeType, byte[] Data)> images)
    {
        var loadedItems = new List<ImageOrderItemViewModel>();

        try
        {
            foreach (var image in images)
            {
                if (_isDisposed)
                    break;

                var vm = new ImageOrderItemViewModel
                {
                    Filename = image.Filename,
                    MimeType = image.MimeType,
                    SourceData = image.Data
                };

                if (await PrepareImageAsync(vm))
                    loadedItems.Add(vm);
            }

            AddLoadedItems(loadedItems);
            loadedItems.Clear();
        }
        finally
        {
            foreach (var item in loadedItems)
                item.Dispose();
        }
    }

    private async Task<bool> PrepareImageAsync(ImageOrderItemViewModel vm)
    {
        if (_isDisposed)
        {
            vm.Dispose();
            return false;
        }

        vm.RemoveRequested += Remove;

        try
        {
            await vm.LoadThumbnailAsync();
        }
        catch
        {
            vm.RemoveRequested -= Remove;
            vm.Dispose();
            throw;
        }

        if (!_isDisposed)
            return true;

        vm.RemoveRequested -= Remove;
        vm.Dispose();
        return false;
    }

    private void AddLoadedItems(List<ImageOrderItemViewModel> loadedItems)
    {
        if (_isDisposed || loadedItems.Count == 0)
            return;

        ReplaceItems(Items.Concat(loadedItems));
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

    private static string DetectMime(string filename) =>
        Path.GetExtension(filename).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            _ => "application/octet-stream"
        };

    private void OnPreferencesChanged()
    {
        OnPropertyChanged(nameof(CountLabel));
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        IsEmpty = Items.Count == 0;
        OnPropertyChanged(nameof(CountLabel));
    }

    partial void OnItemsChanging(ObservableCollection<ImageOrderItemViewModel> value)
    {
        value.CollectionChanged -= OnItemsChanged;
    }

    partial void OnItemsChanged(ObservableCollection<ImageOrderItemViewModel> value)
    {
        value.CollectionChanged += OnItemsChanged;
        IsEmpty = value.Count == 0;
        OnPropertyChanged(nameof(CountLabel));
    }

    private void ReplaceItems(IEnumerable<ImageOrderItemViewModel> items)
    {
        Items = new ObservableCollection<ImageOrderItemViewModel>(items);
    }

    public void ClearItems()
    {
        var items = Items.ToList();
        ReplaceItems([]);

        foreach (var item in items)
        {
            item.RemoveRequested -= Remove;
            item.Dispose();
        }

        IsEmpty = true;
        OnPropertyChanged(nameof(CountLabel));
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _preferencesService.PreferencesChanged -= OnPreferencesChanged;
        Items.CollectionChanged -= OnItemsChanged;
        ClearItems();
    }
}
