using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Organizer.Application.ViewModels.Components;

namespace Organizer.Application.Components;

public partial class ImagePreviewModal : UserControl
{
    private TopLevel? _topLevel;
    private ImagePreviewViewModel? _viewModel;

    public ImagePreviewModal()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;
        AttachedToVisualTree += (_, _) =>
        {
            _topLevel = TopLevel.GetTopLevel(this);
            _topLevel?.AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        };

        DetachedFromVisualTree += (_, _) =>
        {
            if (_topLevel is null)
                return;

            _topLevel.RemoveHandler(KeyDownEvent, OnKeyDown);
            _topLevel = null;
        };
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (_viewModel is not null)
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        _viewModel = DataContext as ImagePreviewViewModel;

        if (_viewModel is not null)
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ImagePreviewViewModel.CurrentBitmap) or nameof(ImagePreviewViewModel.IsVisible))
            ZoomArea.Reset();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || _viewModel?.IsVisible != true)
            return;

        _viewModel.CloseCommand.Execute(null);
        e.Handled = true;
    }
}