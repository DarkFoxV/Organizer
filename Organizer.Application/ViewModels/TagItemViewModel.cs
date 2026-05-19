using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Organize.Organizer.Core.Enums;

namespace Organizer.Application.ViewModels.Components;

public partial class TagItemViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BackgroundColor))]
    [NotifyPropertyChangedFor(nameof(ForegroundColor))]
    private TagColor _color = TagColor.Blue;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BackgroundColor))]
    [NotifyPropertyChangedFor(nameof(ForegroundColor))]
    private bool _isSelected;

    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;

    // Cores baseadas no estado e na cor da tag (igual ao Rust)
    public string BackgroundColor => IsSelected ? SelectedBackground : DimBackground;
    public string ForegroundColor => IsSelected ? "#ffffff" : DimForeground;

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

    private string DimBackground => Color switch
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

    private string DimForeground => Color switch
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

    public event Action<TagItemViewModel>? Toggled;

    [RelayCommand]
    private void Toggle()
    {
        IsSelected = !IsSelected;
        Toggled?.Invoke(this);
    }
}
