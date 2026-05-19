using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Organizer.Application.ViewModels.Components;

namespace Organizer.Application.Components;

public partial class ImageOrderList : UserControl
{
    // CreateInProcessFormat<T> — transporta objeto custom dentro do processo
    private static readonly DataFormat<ImageOrderItemViewModel> DragFormat =
        DataFormat.CreateInProcessFormat<ImageOrderItemViewModel>("organizer.image-order-item");

    private ImageOrderItemViewModel? _dragging;
    private bool _dragStarted;
    private PointerPressedEventArgs? _pressedArgs;

    public ImageOrderList()
    {
        InitializeComponent();

        OrderList.AddHandler(PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel);
        OrderList.AddHandler(PointerMovedEvent, OnPointerMoved, RoutingStrategies.Tunnel);
        OrderList.AddHandler(PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Tunnel);

        DragDrop.AddDragOverHandler(OrderList, OnDragOver);
        DragDrop.AddDropHandler(OrderList, OnDrop);
        DragDrop.SetAllowDrop(OrderList, true);
    }

    private ImageOrderListViewModel? VM => DataContext as ImageOrderListViewModel;

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (FindNamed(e.Source as Visual, "DragHandle") is null) return;
        _dragging = GetItem(e.Source as Visual);
        _pressedArgs = e;
        _dragStarted = false;
    }

    private async void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragging is null || _dragStarted) return;
        _dragStarted = true;
        _dragging.IsDragging = true;

        var item = new DataTransferItem();
        item.Set(DragFormat, _dragging);

        var data = new DataTransfer();
        data.Add(item);

        await DragDrop.DoDragDropAsync(_pressedArgs!, data, DragDropEffects.Move);

        if (_dragging is not null)
            _dragging.IsDragging = false;

        _dragging = null;
        _dragStarted = false;
        ClearDropTargets();
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_dragging is null) return;
        _dragging.IsDragging = false;
        _dragging = null;
        _dragStarted = false;
        ClearDropTargets();
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (!e.DataTransfer.Formats.Contains(DragFormat))
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        e.DragEffects = DragDropEffects.Move;
        ClearDropTargets();
        var target = GetItem(e.Source as Visual);
        if (target is not null && target != _dragging)
            target.IsDropTarget = true;

        e.Handled = true;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (!e.DataTransfer.Formats.Contains(DragFormat) || VM is null)
        {
            ClearDropTargets();
            return;
        }

        // TryGetValue extension method para DataFormat<T>
        var dragged = e.DataTransfer.TryGetValue(DragFormat);
        var target = GetItem(e.Source as Visual);

        if (dragged is null || target is null || dragged == target)
        {
            ClearDropTargets();
            return;
        }

        var from = VM.Items.IndexOf(dragged);
        var to = VM.Items.IndexOf(target);
        if (from >= 0 && to >= 0)
            VM.Move(from, to);

        ClearDropTargets();
        e.Handled = true;
    }

    private static ImageOrderItemViewModel? GetItem(Visual? v)
    {
        while (v is not null)
        {
            if (v is Border { DataContext: ImageOrderItemViewModel vm }) return vm;
            v = v.GetVisualParent();
        }

        return null;
    }

    private static Visual? FindNamed(Visual? v, string name)
    {
        while (v is not null)
        {
            if (v is Control c && c.Name == name) return v;
            v = v.GetVisualParent();
        }

        return null;
    }

    private void ClearDropTargets()
    {
        if (VM is null) return;
        foreach (var item in VM.Items)
            item.IsDropTarget = false;
    }
}