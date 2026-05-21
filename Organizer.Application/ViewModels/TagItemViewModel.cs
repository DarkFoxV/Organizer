using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Organize.Organizer.Core.Enums;
using Organizer.Application.Services;

namespace Organizer.Application.ViewModels.Components;

public partial class TagItemViewModel : ObservableObject
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

    private string SelectedBackground => Color switch
    {
        TagColor.Red => "#dc2626",
        TagColor.Green => "#16a34a",
        TagColor.Blue => "#3b82f6",
        TagColor.Orange => "#d97706",
        TagColor.Purple => "#9333ea",
        TagColor.Pink => "#db2777",
        TagColor.Indigo => "#4f46e5",
        TagColor.Teal => "#0d9488",
        TagColor.Gray => "#6b7280",
        _ => "#3b82f6"
    };

    private string DarkDimBackground => Color switch
    {
        TagColor.Red => "#1f0a0a",
        TagColor.Green => "#0a1f0f",
        TagColor.Blue => "#0d1f3c",
        TagColor.Orange => "#1f160a",
        TagColor.Purple => "#160a1f",
        TagColor.Pink => "#1f0a14",
        TagColor.Indigo => "#0e0d1f",
        TagColor.Teal => "#0a1a1f",
        TagColor.Gray => "#141414",
        _ => "#0d1f3c"
    };

    private string DarkDimForeground => Color switch
    {
        TagColor.Red => "#f87171",
        TagColor.Green => "#4ade80",
        TagColor.Blue => "#60a5fa",
        TagColor.Orange => "#fb923c",
        TagColor.Purple => "#c084fc",
        TagColor.Pink => "#f472b6",
        TagColor.Indigo => "#818cf8",
        TagColor.Teal => "#2dd4bf",
        TagColor.Gray => "#9ca3af",
        _ => "#60a5fa"
    };

    private string LightDimBackground => Color switch
    {
        TagColor.Red => "#fee2e2",
        TagColor.Green => "#dcfce7",
        TagColor.Blue => "#dbeafe",
        TagColor.Orange => "#ffedd5",
        TagColor.Purple => "#f3e8ff",
        TagColor.Pink => "#fce7f3",
        TagColor.Indigo => "#e0e7ff",
        TagColor.Teal => "#ccfbf1",
        TagColor.Gray => "#f1f5f9",
        _ => "#dbeafe"
    };

    private string LightDimForeground => Color switch
    {
        TagColor.Red => "#991b1b",
        TagColor.Green => "#166534",
        TagColor.Blue => "#1d4ed8",
        TagColor.Orange => "#9a3412",
        TagColor.Purple => "#7e22ce",
        TagColor.Pink => "#be185d",
        TagColor.Indigo => "#4338ca",
        TagColor.Teal => "#0f766e",
        TagColor.Gray => "#475569",
        _ => "#1d4ed8"
    };

    private string LightDimBorder => Color switch
    {
        TagColor.Red => "#fecaca",
        TagColor.Green => "#bbf7d0",
        TagColor.Blue => "#bfdbfe",
        TagColor.Orange => "#fed7aa",
        TagColor.Purple => "#e9d5ff",
        TagColor.Pink => "#fbcfe8",
        TagColor.Indigo => "#c7d2fe",
        TagColor.Teal => "#99f6e4",
        TagColor.Gray => "#cbd5e1",
        _ => "#bfdbfe"
    };

    public event Action<TagItemViewModel>? Toggled;

    [RelayCommand]
    private void Toggle()
    {
        IsSelected = !IsSelected;
        Toggled?.Invoke(this);
    }

    private void OnPreferencesChanged()
    {
        OnPropertyChanged(nameof(BackgroundColor));
        OnPropertyChanged(nameof(ForegroundColor));
        OnPropertyChanged(nameof(BorderColor));
    }
}
