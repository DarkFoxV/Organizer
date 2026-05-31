using System;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Organize.Organizer.Core;
using Organize.Organizer.Core.Enums;
using Organize.Organizer.Core.Interfaces;
using Organizer.Application.Services;
using Organizer.Application.ViewModels.Components;

namespace Organizer.Application.ViewModels;

public partial class RegisterViewModel : ObservableObject, IDisposable
{
    private static readonly string[] SupportedImagePatterns =
        ["*.png", "*.jpg", "*.jpeg", "*.gif", "*.bmp", "*.webp"];

    private readonly ICardService _cardService;
    private readonly IImageService _imageService;
    private readonly ITagService _tagService;
    private readonly IClipboardService _clipboardService;
    private readonly AppPreferencesService _preferencesService;

    // ── Componentes ───────────────────────────────────────────────────────────
    public TagSelectorViewModel TagSelector { get; }
    public ImageOrderListViewModel ImageOrder { get; }

    // ── Estado ────────────────────────────────────────────────────────────────
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsReady), nameof(StatusText), nameof(StatusIsReady))]
    private string _description = string.Empty;

    [ObservableProperty] private bool _tagsLoaded;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsReady), nameof(StatusText), nameof(StatusIsReady), nameof(CanClose))]
    private bool _isSubmitting;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsReady), nameof(StatusText), nameof(StatusIsReady), nameof(CanClose))]
    private bool _isPickingImages;

    [ObservableProperty] private string? _errorMessage;

    private bool _isDisposed;

    public bool IsReady =>
        !ImageOrder.IsEmpty
        && TagSelector.SelectedTags.Any()
        && !IsSubmitting
        && !IsPickingImages
        && (ImageOrder.Items.Count <= 1 || !string.IsNullOrWhiteSpace(Description));

    public string StatusText => IsReady
        ? _preferencesService.T("Loc.Register.Ready")
        : _preferencesService.T("Loc.Register.FillFields");
    public bool StatusIsReady => IsReady;
    public bool CanClose => !IsSubmitting && !IsPickingImages;

    // ── Eventos ───────────────────────────────────────────────────────────────
    public event Action? CloseRequested;
    public event Action? SubmitSuccess;
    public event Action<bool, string>? BusyStateChanged;

    // ── Init ──────────────────────────────────────────────────────────────────
    public RegisterViewModel(
        ICardService cardService,
        IImageService imageService,
        ITagService tagService,
        IClipboardService clipboardService,
        AppPreferencesService preferencesService)
    {
        _cardService = cardService;
        _imageService = imageService;
        _tagService = tagService;
        _clipboardService = clipboardService;
        _preferencesService = preferencesService;
        _preferencesService.PreferencesChanged += NotifyReady;

        ImageOrder = new ImageOrderListViewModel(_preferencesService);
        TagSelector = new TagSelectorViewModel(_tagService, _preferencesService, showAddButton: true);

        ImageOrder.Items.CollectionChanged += OnImagesChanged;

        TagSelector.SelectionChanged += NotifyReady;

        _ = LoadTagsAsync();
    }

    private void NotifyReady()
    {
        if (_isDisposed)
            return;

        OnPropertyChanged(nameof(IsReady));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(StatusIsReady));
    }

    private async Task LoadTagsAsync()
    {
        try
        {
            await TagSelector.LoadAsync();
            if (_isDisposed)
                return;

            TagsLoaded = true;
            NotifyReady();
        }
        catch (Exception ex)
        {
            if (_isDisposed)
                return;

            ErrorMessage = $"Erro ao carregar tags: {ex.Message}";
            Console.WriteLine(ex);
            NotifyReady();
        }
    }

    // ── Comandos ──────────────────────────────────────────────────────────────
    [RelayCommand]
    private async Task PickImages(IStorageProvider storage)
    {
        IsPickingImages = true;
        BusyStateChanged?.Invoke(true, "Carregando imagens, aguarde...");

        try
        {
            var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Selecionar imagens",
                AllowMultiple = true,
                FileTypeFilter =
                [
                    new FilePickerFileType("Imagens")
                    {
                        Patterns = SupportedImagePatterns
                    }
                ]
            });

            var unownedFiles = files.ToList();

            try
            {
                foreach (var file in files)
                {
                    if (_isDisposed)
                        return;

                    unownedFiles.Remove(file);
                    await ImageOrder.AddImageAsync(file);
                }
            }
            finally
            {
                foreach (var file in unownedFiles)
                    file.Dispose();
            }
        }
        finally
        {
            if (!_isDisposed)
            {
                IsPickingImages = false;
                BusyStateChanged?.Invoke(false, string.Empty);
                NotifyReady();
            }
        }
    }

    public async Task<bool> TryPasteImagesAsync(IClipboard clipboard)
    {
        if (IsPickingImages || IsSubmitting)
            return false;

        IsPickingImages = true;
        BusyStateChanged?.Invoke(true, "Colando imagens, aguarde...");

        try
        {
            ErrorMessage = null;

            var images = await _clipboardService.GetImagesAsync(clipboard);
            if (_isDisposed)
                return false;

            if (images.Count == 0)
            {
                ErrorMessage = "Clipboard nao contem uma imagem suportada para colar.";
                return false;
            }

            foreach (var image in images)
            {
                if (_isDisposed)
                    return false;

                await ImageOrder.AddImageAsync(
                    filename: image.Filename,
                    mimeType: image.MimeType,
                    data: image.Data);
            }

            return true;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Erro ao colar imagem: {ex.Message}";
            return false;
        }
        finally
        {
            if (!_isDisposed)
            {
                IsPickingImages = false;
                BusyStateChanged?.Invoke(false, string.Empty);
                NotifyReady();
            }
        }
    }

    [RelayCommand]
    private async Task Submit()
    {
        if (!IsReady) return;

        IsSubmitting = true;
        ErrorMessage = null;
        BusyStateChanged?.Invoke(true, "Salvando imagens, aguarde...");

        Card? createdCard = null;

        try
        {
            var items = ImageOrder.Items.ToList();
            var selectedTags = TagSelector.SelectedTags.ToList();
            var description = string.IsNullOrWhiteSpace(Description)
                ? null
                : Description.Trim();
            var cardType = items.Count == 1 ? CardType.Single : CardType.Group;
            var title = items.Count == 1
                ? Path.GetFileNameWithoutExtension(items[0].Filename)
                : description ?? string.Empty;

            createdCard = await _cardService.CreateAsync(title, cardType);

            Image? firstImage = null;
            for (var i = 0; i < items.Count; i++)
            {
                if (_isDisposed)
                    throw new OperationCanceledException("Register was disposed during submit.");

                var item = items[i];
                try
                {
                    var imageDescription = i == 0 ? description : null;
                    Image image;

                    if (item.HasFileSource)
                    {
                        await using var stream = await item.OpenReadAsync();
                        if (_isDisposed)
                            throw new OperationCanceledException("Register was disposed during submit.");

                        image = await _imageService.CreateAsync(
                            cardId: createdCard.Id,
                            dataStream: stream,
                            thumbnail: item.ThumbnailData,
                            filename: item.Filename,
                            mimeType: item.MimeType,
                            description: imageDescription);
                    }
                    else
                    {
                        byte[]? data = await item.ReadDataAsync();
                        if (_isDisposed)
                            throw new OperationCanceledException("Register was disposed during submit.");

                        image = await _imageService.CreateAsync(
                            cardId: createdCard.Id,
                            data: data,
                            thumbnail: item.ThumbnailData,
                            filename: item.Filename,
                            mimeType: item.MimeType,
                            description: imageDescription);
                        data = null;
                    }

                    firstImage ??= image;

                    foreach (var tag in selectedTags)
                        await _imageService.AddTagAsync(image.Id, tag.Id);
                }
                finally
                {
                    item.RemoveRequested -= ImageOrder.Remove;
                    item.Dispose();
                }
            }

            if (firstImage is not null)
                await _cardService.SetCoverAsync(createdCard.Id, firstImage.Id);

            Cleanup(queueMemoryCompaction: true);
            SubmitSuccess?.Invoke();
        }
        catch (Exception ex)
        {
            if (createdCard is not null)
            {
                try
                {
                    await _cardService.DeleteAsync(createdCard.Id);
                }
                catch (Exception rollbackEx)
                {
                    Console.WriteLine(rollbackEx);
                }
            }

            if (!_isDisposed)
                ErrorMessage = $"Erro ao salvar: {ex.Message}";

            Console.WriteLine(ex);
        }
        finally
        {
            if (!_isDisposed)
            {
                IsSubmitting = false;
                BusyStateChanged?.Invoke(false, string.Empty);
            }
        }
    }

    [RelayCommand(CanExecute = nameof(CanClose))]
    private void Close()
    {
        Cleanup();

        CloseRequested?.Invoke();
    }

    partial void OnIsSubmittingChanged(bool value)
    {
        CloseCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsPickingImagesChanged(bool value)
    {
        CloseCommand.NotifyCanExecuteChanged();
    }

    public void Dispose()
    {
        Cleanup();
    }

    private void Cleanup(bool queueMemoryCompaction = false)
    {
        if (_isDisposed) return;
        _isDisposed = true;
        var shouldCompactMemory = queueMemoryCompaction || ImageOrder.Items.Count > 0;
        BusyStateChanged?.Invoke(false, string.Empty);

        TagSelector.SelectionChanged -= NotifyReady;
        ImageOrder.Items.CollectionChanged -= OnImagesChanged;
        ImageOrder.ClearItems();
        TagSelector.Dispose();
        ImageOrder.Dispose();
        _preferencesService.PreferencesChanged -= NotifyReady;

        Description = string.Empty;
        ErrorMessage = null;

        if (shouldCompactMemory)
            MemoryCleanupService.QueueLargeImageMemoryCompaction();
    }

    private void OnImagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        NotifyReady();
    }
}
