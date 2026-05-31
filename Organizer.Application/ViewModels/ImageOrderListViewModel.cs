using System.Collections.Generic;
using System.Collections.ObjectModel;
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

    public ImageOrderListViewModel(AppPreferencesService preferencesService)
    {
        _preferencesService = preferencesService;
        _preferencesService.PreferencesChanged += OnPreferencesChanged;
        Items.CollectionChanged += OnItemsChanged;
    }

    public ObservableCollection<ImageOrderItemViewModel> Items { get; } = [];

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
        if (_isDisposed)
        {
            vm.Dispose();
            return;
        }

        vm.RemoveRequested += Remove;

        try
        {
            await vm.LoadThumbnailAsync();
        }
        catch
        {
            vm.Dispose();
            throw;
        }

        if (_isDisposed)
        {
            vm.Dispose();
            return;
        }

        Items.Add(vm);
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

    private void OnItemsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
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
