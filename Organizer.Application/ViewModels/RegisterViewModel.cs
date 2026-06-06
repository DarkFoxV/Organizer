using System;
using System.Collections.Generic;
using System.ComponentModel;
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
    private readonly RegisterImageItemFactory _imageItemFactory;
    private readonly AppPreferencesService _preferencesService;
    private readonly IToastService _toastService;

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
        && !string.IsNullOrWhiteSpace(Description);

    public string StatusText => GetStatusText();
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
        RegisterImageItemFactory imageItemFactory,
        AppPreferencesService preferencesService,
        IToastService toastService)
    {
        _cardService = cardService;
        _imageService = imageService;
        _tagService = tagService;
        _clipboardService = clipboardService;
        _imageItemFactory = imageItemFactory;
        _preferencesService = preferencesService;
        _toastService = toastService;
        _preferencesService.PreferencesChanged += NotifyReady;

        ImageOrder = new ImageOrderListViewModel(_preferencesService);
        TagSelector = new TagSelectorViewModel(_tagService, _preferencesService, showAddButton: true);

        ImageOrder.PropertyChanged += OnImageOrderPropertyChanged;

        TagSelector.SelectionChanged += NotifyReady;

        _ = LoadTagsAsync();
    }

    private void NotifyReady(
        object? sender,
        AppPreferencesChangedEventArgs e)
    {
        if (e.LanguageChanged)
            NotifyReady();
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

            var pendingFiles = new Queue<IStorageFile>(files);
            var preparedItems = new List<ImageOrderItemViewModel>();

            try
            {
                while (pendingFiles.TryDequeue(out var file))
                {
                    if (_isDisposed)
                    {
                        file.Dispose();
                        break;
                    }

                    var item = await _imageItemFactory.CreateAsync(file);
                    if (_isDisposed)
                    {
                        item.Dispose();
                        break;
                    }

                    preparedItems.Add(item);
                }

                if (_isDisposed)
                    return;

                ImageOrder.AddRange(preparedItems);
                preparedItems.Clear();
            }
            finally
            {
                foreach (var file in pendingFiles)
                    file.Dispose();

                foreach (var item in preparedItems)
                    item.Dispose();
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

            var preparedItems = new List<ImageOrderItemViewModel>();

            try
            {
                foreach (var image in images)
                {
                    if (_isDisposed)
                        return false;

                    var item = await _imageItemFactory.CreateAsync(
                        image.Filename,
                        image.MimeType,
                        image.Data);

                    if (_isDisposed)
                    {
                        item.Dispose();
                        return false;
                    }

                    preparedItems.Add(item);
                }

                ImageOrder.AddRange(preparedItems);
                preparedItems.Clear();
            }
            finally
            {
                foreach (var item in preparedItems)
                    item.Dispose();
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
            var title = Description.Trim();
            var cardType = items.Count == 1 ? CardType.Single : CardType.Group;

            createdCard = await _cardService.CreateAsync(title, cardType);

            Image? firstImage = null;
            for (var i = 0; i < items.Count; i++)
            {
                if (_isDisposed)
                    throw new OperationCanceledException("Register was disposed during submit.");

                var item = items[i];
                try
                {
                    var imageDescription = i == 0 ? title : null;
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
            _toastService.Success(
                _preferencesService.T("Loc.Register.ToastSavedTitle"),
                items.Count == 1
                    ? _preferencesService.T("Loc.Register.ToastSingleSavedMessage")
                    : _preferencesService.T("Loc.Register.ToastGroupSavedMessage", items.Count));
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

            _toastService.Error(
                _preferencesService.T("Loc.Register.ToastSaveFailedTitle"),
                _preferencesService.T("Loc.Register.ToastSaveFailedMessage"));
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
        ImageOrder.PropertyChanged -= OnImageOrderPropertyChanged;
        ImageOrder.ClearItems();
        TagSelector.Dispose();
        ImageOrder.Dispose();
        _preferencesService.PreferencesChanged -= NotifyReady;

        Description = string.Empty;
        ErrorMessage = null;

        if (shouldCompactMemory)
            MemoryCleanupService.QueueLargeImageMemoryCompaction();
    }

    private void OnImageOrderPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ImageOrder.IsEmpty) or nameof(ImageOrder.Items) or nameof(ImageOrder.CountLabel))
            NotifyReady();
    }

    private string GetStatusText()
    {
        if (IsReady)
            return _preferencesService.T("Loc.Register.Ready");

        if (ImageOrder.IsEmpty)
            return _preferencesService.T("Loc.Register.MissingImages");

        if (string.IsNullOrWhiteSpace(Description))
            return _preferencesService.T("Loc.Register.MissingTitle");

        if (!TagSelector.SelectedTags.Any())
            return _preferencesService.T("Loc.Register.MissingTags");

        return _preferencesService.T("Loc.Register.FillFields");
    }
}
