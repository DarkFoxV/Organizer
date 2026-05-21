using System;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Runtime;
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

    // ── Componentes ───────────────────────────────────────────────────────────
    public TagSelectorViewModel TagSelector { get; }
    public ImageOrderListViewModel ImageOrder { get; } = new();

    // ── Estado ────────────────────────────────────────────────────────────────
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsReady), nameof(StatusText), nameof(StatusIsReady))]
    private string _description = string.Empty;

    [ObservableProperty] private bool _tagsLoaded;

    [ObservableProperty] private bool _isSubmitting;

    [ObservableProperty] private bool _isPickingImages;

    [ObservableProperty] private string? _errorMessage;

    private bool _isDisposed;

    public bool IsReady =>
        !ImageOrder.IsEmpty
        && TagSelector.SelectedTags.Any()
        && !IsSubmitting
        && !IsPickingImages
        && (ImageOrder.Items.Count <= 1 || !string.IsNullOrWhiteSpace(Description));

    public string StatusText => IsReady ? "✓ Pronto para salvar" : "Preencha todos os campos";
    public bool StatusIsReady => IsReady;

    // ── Eventos ───────────────────────────────────────────────────────────────
    public event Action? CloseRequested;
    public event Action? SubmitSuccess;
    public event Action<bool, string>? BusyStateChanged;

    // ── Init ──────────────────────────────────────────────────────────────────
    public RegisterViewModel(
        ICardService cardService,
        IImageService imageService,
        ITagService tagService,
        IClipboardService clipboardService)
    {
        _cardService = cardService;
        _imageService = imageService;
        _tagService = tagService;
        _clipboardService = clipboardService;

        TagSelector = new TagSelectorViewModel(_tagService, showAddButton: true);

        ImageOrder.Items.CollectionChanged += OnImagesChanged;

        // SelectionChanged dispara quando qualquer tag é selecionada/deselecionada
        TagSelector.SelectionChanged += NotifyReady;

        _ = LoadTagsAsync();
    }

    private void NotifyReady()
    {
        OnPropertyChanged(nameof(IsReady));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(StatusIsReady));
    }

    private async Task LoadTagsAsync()
    {
        await TagSelector.LoadAsync();
        TagsLoaded = true;
        NotifyReady();
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

            if (files.Count == 0)
                return;

            foreach (var file in files)
                await ImageOrder.AddImageAsync(file);
        }
        finally
        {
            IsPickingImages = false;
            BusyStateChanged?.Invoke(false, string.Empty);
            NotifyReady();
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
            if (images.Count == 0)
            {
                ErrorMessage = "Clipboard nao contem uma imagem suportada para colar.";
                return false;
            }

            foreach (var image in images)
            {
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
            IsPickingImages = false;
            BusyStateChanged?.Invoke(false, string.Empty);
            NotifyReady();
        }
    }

    [RelayCommand]
    private async Task Submit()
    {
        if (!IsReady) return;

        IsSubmitting = true;
        ErrorMessage = null;
        BusyStateChanged?.Invoke(true, "Salvando imagens, aguarde...");

        try
        {
            var items = ImageOrder.Items.ToList();
            var cardType = items.Count == 1 ? CardType.Single : CardType.Group;
            var title = items.Count == 1
                ? Path.GetFileNameWithoutExtension(items[0].Filename)
                : Description.Trim();

            // 1. Cria o card
            var card = await _cardService.CreateAsync(title, cardType);

            // 2. Salva cada imagem na ordem definida pelo usuário
            Image? firstImage = null;
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var mime = item.MimeType;
                byte[]? data = await item.ReadDataAsync();
                var image = await _imageService.CreateAsync(
                    cardId: card.Id,
                    data: data,
                    filename: item.Filename,
                    mimeType: mime,
                    description: i == 0 ? Description.Trim() : null);
                data = null;
                item.RemoveRequested -= ImageOrder.Remove;
                item.Dispose();

                firstImage ??= image;

                // 3. Associa as tags selecionadas em cada imagem
                foreach (var tag in TagSelector.SelectedTags)
                    await _imageService.AddTagAsync(image.Id, tag.Id);
            }

            // 4. Define a primeira imagem como capa do card
            if (firstImage is not null)
                await _cardService.SetCoverAsync(card.Id, firstImage.Id);

            Cleanup();
            SubmitSuccess?.Invoke();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Erro ao salvar: {ex.Message}";
            Console.WriteLine(ex);
        }
        finally
        {
            IsSubmitting = false;
            BusyStateChanged?.Invoke(false, string.Empty);
        }
    }

    [RelayCommand]
    private void Close()
    {
        Cleanup();

        CloseRequested?.Invoke();
    }

    public void Dispose()
    {
        Cleanup();
    }

    private void Cleanup()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        var hadImages = ImageOrder.Items.Count > 0;
        BusyStateChanged?.Invoke(false, string.Empty);

        foreach (var item in ImageOrder.Items.ToList())
        {
            item.RemoveRequested -= ImageOrder.Remove;
            item.Dispose();
        }

        ImageOrder.Items.Clear();

        TagSelector.SelectionChanged -= NotifyReady;
        ImageOrder.Items.CollectionChanged -= OnImagesChanged;

        Description = string.Empty;
        ErrorMessage = null;

        if (hadImages)
            CompactLargeImageMemory();
    }

    private static void CompactLargeImageMemory()
    {
        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
    }

    private void OnImagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        NotifyReady();
    }
}