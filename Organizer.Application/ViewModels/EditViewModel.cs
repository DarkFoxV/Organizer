using System;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Organize.Organizer.Core.Interfaces;
using Organizer.Application.ViewModels.Components;

namespace Organizer.Application.ViewModels;

public partial class EditViewModel : ObservableObject
{
    private readonly IImageService _imageService;
    private readonly ITagService _tagService;

    private int _imageId;
    private int[] _initialTagIds = [];
    private string _initialDescription = string.Empty;

    public TagSelectorViewModel TagSelector { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsReady), nameof(StatusText), nameof(StatusIsReady))]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _title = "Editar";

    [ObservableProperty]
    private string _subtitle = string.Empty;

    [ObservableProperty]
    private bool _tagsLoaded;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsReady), nameof(StatusText), nameof(StatusIsReady))]
    private bool _isSubmitting;

    [ObservableProperty]
    private string? _errorMessage;

    public bool IsReady => TagsLoaded && !IsSubmitting;
    public string StatusText => IsSubmitting ? "Salvando alterações..." : "Atualize a descrição e as tags da imagem de capa";
    public bool StatusIsReady => !IsSubmitting;

    public event Action? CloseRequested;
    public event Action? SubmitSuccess;

    public EditViewModel(IImageService imageService, ITagService tagService)
    {
        _imageService = imageService;
        _tagService = tagService;

        TagSelector = new TagSelectorViewModel(_tagService, showAddButton: false);
        TagSelector.SelectionChanged += NotifyReady;
    }

    public async Task LoadAsync(CardItemViewModel card)
    {
        _imageId = card.Id;
        Title = "Editar";
        Subtitle = card.IsGroup
            ? $"{card.ImageCount} imagens no card • editando a capa"
            : "Editando a imagem de capa";
        ErrorMessage = null;

        var image = await _imageService.GetByIdAsync(card.Id)
            ?? throw new InvalidOperationException("Imagem de capa não encontrada.");

        _initialDescription = image.Description ?? string.Empty;
        _initialTagIds = image.ImageTags.Select(tag => tag.TagId).ToArray();

        Description = _initialDescription;

        await TagSelector.LoadAsync(_tagService);
        TagSelector.SetSelectedTagIds(_initialTagIds);

        TagsLoaded = true;
        NotifyReady();
    }

    private void NotifyReady()
    {
        OnPropertyChanged(nameof(IsReady));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(StatusIsReady));
    }

    [RelayCommand]
    private async Task Submit()
    {
        if (!IsReady || _imageId == 0) return;

        IsSubmitting = true;
        ErrorMessage = null;

        try
        {
            await _imageService.UpdateDescriptionAsync(_imageId, string.IsNullOrWhiteSpace(Description) ? null : Description.Trim());

            var selectedTagIds = TagSelector.SelectedTags.Select(tag => tag.Id).OrderBy(id => id).ToArray();
            var tagsToAdd = selectedTagIds.Except(_initialTagIds).ToArray();
            var tagsToRemove = _initialTagIds.Except(selectedTagIds).ToArray();

            foreach (var tagId in tagsToAdd)
                await _imageService.AddTagAsync(_imageId, tagId);

            foreach (var tagId in tagsToRemove)
                await _imageService.RemoveTagAsync(_imageId, tagId);

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
            NotifyReady();
        }
    }

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke();
}
