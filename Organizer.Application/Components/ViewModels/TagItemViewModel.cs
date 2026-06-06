using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Organize.Organizer.Core.Enums;
using Organizer.Application.Services;

namespace Organizer.Application.ViewModels.Components;

public partial class TagItemViewModel : ObservableObject, IDisposable
{
    private readonly AppPreferencesService? _preferencesService;

    public TagItemViewModel()
    {
    }

    public TagItemViewModel(AppPreferencesService preferencesService)
    {
        _preferencesService = preferencesService;
        _preferencesService.PreferencesChanged += OnPreferencesChanged;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BackgroundColor))]
    [NotifyPropertyChangedFor(nameof(ForegroundColor))]
    [NotifyPropertyChangedFor(nameof(BorderColor))]
    private TagColor _color = TagColor.Blue;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BackgroundColor))]
    [NotifyPropertyChangedFor(nameof(ForegroundColor))]
    [NotifyPropertyChangedFor(nameof(BorderColor))]
    private bool _isSelected;

    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    
    public string BackgroundColor => IsSelected
        ? SelectedBackground
        : UseLightPalette
            ? LightDimBackground
            : DarkDimBackground;

    public string ForegroundColor => IsSelected
        ? "#ffffff"
        : UseLightPalette
            ? LightDimForeground
            : DarkDimForeground;

    public string BorderColor => IsSelected
        ? SelectedBackground
        : UseLightPalette
            ? LightDimBorder
            : DarkDimBackground;

    private bool UseLightPalette => _preferencesService?.Current.Theme == AppThemePreference.Light;

    private TagColorPaletteEntry Palette => TagColorPalette.Get(Color);

    private string SelectedBackground => Palette.SelectedBackground;

    private string DarkDimBackground => Palette.DarkDimBackground;

    private string DarkDimForeground => Palette.DarkDimForeground;

    private string LightDimBackground => Palette.LightDimBackground;

    private string LightDimForeground => Palette.LightDimForeground;

    private string LightDimBorder => Palette.LightDimBorder;

    public event Action<TagItemViewModel>? Toggled;

    [RelayCommand]
    private void Toggle()
    {
        IsSelected = !IsSelected;
        Toggled?.Invoke(this);
    }

    private void OnPreferencesChanged(
        object? sender,
        AppPreferencesChangedEventArgs e)
    {
        if (!e.ThemeChanged)
            return;

        OnPropertyChanged(nameof(BackgroundColor));
        OnPropertyChanged(nameof(ForegroundColor));
        OnPropertyChanged(nameof(BorderColor));
    }

    public void Dispose()
    {
        if (_preferencesService is not null)
            _preferencesService.PreferencesChanged -= OnPreferencesChanged;
    }
}
