using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Rendering;
using Organizer.Application.ViewModels;

namespace Organizer.Application.Controls;

public class WorkspaceCanvas : Control, ICustomHitTest
{
    private const double DragThreshold = 1;
    private const double MinItemSize = 48;
    private const double ResizeHandleRadius = 28;
    private static readonly IBrush SelectionBrush = new SolidColorBrush(Color.Parse("#60a5fa"));
    private static readonly IBrush RemoveBrush = new SolidColorBrush(Color.Parse("#dc2626"));
    private static readonly IBrush RemoveGlyphBrush = Brushes.White;
    private static readonly IBrush HandleBrush = new SolidColorBrush(Color.Parse("#2563eb"));
    private static readonly IBrush MissingFillBrush = new SolidColorBrush(Color.FromArgb(210, 45, 21, 21));
    private static readonly IBrush MissingTextBrush = new SolidColorBrush(Color.Parse("#fecaca"));
    private static readonly Pen MissingBorderPen = new(new SolidColorBrush(Color.Parse("#ef4444")), 2);
    private static readonly IBrush BoxSelectionFillBrush = new SolidColorBrush(Color.FromArgb(45, 96, 165, 250));
    private static readonly Pen BoxSelectionPen = new(new SolidColorBrush(Color.Parse("#60a5fa")), 2);
    private static readonly Pen SelectionPen = new(SelectionBrush, WorkspaceCanvasItemViewModel.SelectionBorderThickness);
    private static readonly Pen HandlePen = new(new SolidColorBrush(Color.Parse("#bfdbfe")), 3);

    private readonly List<WorkspaceCanvasItemViewModel> _observedItems = [];
    private INotifyCollectionChanged? _observedCollection;
    private Rect _viewportBounds;
    private double _zoom = 1;
    private bool _useLowResBitmaps;
    private bool _isCameraPanMode;
    private bool _isPointerDownOnItem;
    private bool _isDraggingItem;
    private bool _isPointerDownOnBoxSelection;
    private bool _isBoxSelecting;
    private bool _isBoxSelectionAdditive;
    private Point _itemStartPoint;
    private Point _itemStartOrigin;
    private Point _boxSelectionStartPoint;
    private Point _boxSelectionCurrentPoint;
    private Size _itemStartSize;
    private double _interactionDeltaX;
    private double _interactionDeltaY;
    private Rect _resizePreviewRect;
    private bool _hasResizePreview;
    private WorkspacePointerAction _pointerAction;
    private ResizeHandleKind _activeResizeHandle;
    private WorkspaceCanvasItemViewModel? _activeItem;

    private enum WorkspacePointerAction
    {
        None,
        Drag,
        Resize
    }

    private enum ResizeHandleKind
    {
        None,
        TopLeft,
        TopCenter,
        TopRight,
        MiddleLeft,
        MiddleRight,
        BottomLeft,
        BottomCenter,
        BottomRight
    }

    private readonly record struct WorkspaceHitTestResult(
        WorkspaceCanvasItemViewModel? Item,
        ResizeHandleKind ResizeHandle,
        WorkspaceCanvasItemViewModel? RemoveItem);

    public static readonly StyledProperty<IEnumerable<WorkspaceCanvasItemViewModel>?> ItemsProperty =
        AvaloniaProperty.Register<WorkspaceCanvas, IEnumerable<WorkspaceCanvasItemViewModel>?>(nameof(Items));

    public IEnumerable<WorkspaceCanvasItemViewModel>? Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    public void SetViewportBounds(Rect viewportBounds)
    {
        _viewportBounds = viewportBounds;
        InvalidateVisual();
    }

    public void SetZoom(double zoom)
    {
        if (Math.Abs(_zoom - zoom) < 0.0001)
            return;

        _zoom = zoom;
        InvalidateVisual();
    }

    public void SetUseLowResBitmaps(bool useLowResBitmaps)
    {
        if (_useLowResBitmaps == useLowResBitmaps)
            return;

        _useLowResBitmaps = useLowResBitmaps;
        InvalidateVisual();
    }

    public void SetCameraPanMode(bool isCameraPanMode)
    {
        _isCameraPanMode = isCameraPanMode;
        Cursor = isCameraPanMode
            ? new Cursor(StandardCursorType.SizeAll)
            : Cursor.Default;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ItemsProperty)
            RefreshItemSubscriptions();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        RefreshItemSubscriptions();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        ClearItemSubscriptions();
        base.OnDetachedFromVisualTree(e);
    }

    public bool HitTest(Point point)
    {
        return new Rect(Bounds.Size).Contains(point);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var viewportBounds = _viewportBounds.Width > 0 && _viewportBounds.Height > 0
            ? _viewportBounds
            : new Rect(Bounds.Size);

        foreach (var item in GetOrderedItems())
        {
            var itemRect = GetRenderRect(item);

            if (!Intersects(itemRect, viewportBounds))
                continue;

            if (item.IsMissingOrCorrupted)
                DrawMissingItem(context, item, itemRect);
            else if (GetBitmap(item) is { } bitmap)
                context.DrawImage(bitmap, itemRect);

            if (item.IsSelected)
                DrawSelection(context, itemRect);
        }

        if (_isBoxSelecting)
            DrawBoxSelection(context, WorldRectToCanvasRect(GetBoxSelectionWorldRect()));
    }

    private IEnumerable<WorkspaceCanvasItemViewModel> GetOrderedItems()
    {
        var items = Items;
        return items is null
            ? []
            : items.OrderBy(item => item.ZIndex);
    }

    private Bitmap? GetBitmap(WorkspaceCanvasItemViewModel item)
    {
        if (_useLowResBitmaps && item.ThumbnailBitmap is not null)
            return item.ThumbnailBitmap;

        var itemScale = item.OriginalWidth > 0
            ? item.Width / item.OriginalWidth
            : 1;
        var effectiveScale = itemScale * _zoom;

        if (effectiveScale < 0.35 && item.QuarterBitmap is not null)
            return item.QuarterBitmap;

        if (effectiveScale < 0.75)
            return item.HalfBitmap ?? item.QuarterBitmap ?? item.ThumbnailBitmap ?? item.Bitmap;

        return item.Bitmap;
    }

    private static void DrawSelection(DrawingContext context, Rect itemRect)
    {
        context.DrawRectangle(null, SelectionPen, itemRect);
        DrawRemoveButton(context, itemRect);
        DrawResizeHandles(context, itemRect);
    }

    private static void DrawMissingItem(
        DrawingContext context,
        WorkspaceCanvasItemViewModel item,
        Rect itemRect)
    {
        context.DrawRectangle(MissingFillBrush, MissingBorderPen, itemRect, 8);

        var label = string.IsNullOrWhiteSpace(item.Label)
            ? "Imagem ausente"
            : item.Label;

        var title = new FormattedText(
            "Imagem ausente/corrompida",
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            18,
            MissingTextBrush);

        var subtitle = new FormattedText(
            label,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            13,
            MissingTextBrush)
        {
            MaxTextWidth = Math.Max(1, itemRect.Width - 32),
            MaxLineCount = 1,
            Trimming = TextTrimming.CharacterEllipsis
        };

        var textHeight = title.Height + subtitle.Height + 8;
        var y = itemRect.Y + Math.Max(16, (itemRect.Height - textHeight) / 2);
        context.DrawText(title, new Point(itemRect.X + 16, y));
        context.DrawText(subtitle, new Point(itemRect.X + 16, y + title.Height + 8));
    }

    private static void DrawBoxSelection(DrawingContext context, Rect selectionRect)
    {
        context.DrawRectangle(BoxSelectionFillBrush, BoxSelectionPen, selectionRect);
    }

    private static void DrawRemoveButton(DrawingContext context, Rect itemRect)
    {
        var rect = GetRemoveButtonRect(itemRect);
        var center = rect.Center;
        context.DrawEllipse(RemoveBrush, null, center, rect.Width / 2, rect.Height / 2);

        var pen = new Pen(RemoveGlyphBrush, 2);
        context.DrawLine(pen, new Point(center.X - 5, center.Y - 5), new Point(center.X + 5, center.Y + 5));
        context.DrawLine(pen, new Point(center.X + 5, center.Y - 5), new Point(center.X - 5, center.Y + 5));
    }

    private static void DrawResizeHandles(DrawingContext context, Rect itemRect)
    {
        foreach (var center in GetHandleCenters(itemRect))
            context.DrawEllipse(HandleBrush, HandlePen, center, 28, 28);
    }

    public static Rect GetRemoveButtonRect(Rect itemRect)
    {
        const double size = 30;
        const double margin = 10;
        const double pad = WorkspaceCanvasItemViewModel.SelectionChromePadding;

        return new Rect(
            itemRect.Right + pad - margin - size,
            itemRect.Top - pad + margin,
            size,
            size);
    }

    public static IReadOnlyList<Point> GetHandleCenters(Rect itemRect)
    {
        var centerX = itemRect.Left + itemRect.Width / 2;
        var centerY = itemRect.Top + itemRect.Height / 2;

        return
        [
            new Point(itemRect.Left, itemRect.Top),
            new Point(centerX, itemRect.Top),
            new Point(itemRect.Right, itemRect.Top),
            new Point(itemRect.Left, centerY),
            new Point(itemRect.Right, centerY),
            new Point(itemRect.Left, itemRect.Bottom),
            new Point(centerX, itemRect.Bottom),
            new Point(itemRect.Right, itemRect.Bottom)
        ];
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (_isCameraPanMode || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        var vm = WorkspaceViewModel;
        if (vm is null)
            return;

        var hit = HitTestWorkspace(e.GetPosition(this));
        if (hit.RemoveItem is not null)
        {
            vm.RemoveItem(hit.RemoveItem);
            e.Handled = true;
            return;
        }

        if (hit.Item is null)
        {
            StartBoxSelection(e);
            e.Handled = true;
            return;
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            vm.ToggleItemSelection(hit.Item);
            e.Handled = true;
            return;
        }

        if (!hit.Item.IsSelected)
            vm.SelectItem(hit.Item);

        StartItemInteraction(vm, hit.Item, hit.ResizeHandle, e);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (_isCameraPanMode)
            return;

        if (_isPointerDownOnItem)
        {
            UpdateItemInteraction(e);
            e.Handled = true;
            return;
        }

        if (_isPointerDownOnBoxSelection)
        {
            UpdateBoxSelection(e);
            e.Handled = true;
            return;
        }

        UpdateWorkspaceCursor(e.GetPosition(this));
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_isPointerDownOnBoxSelection)
        {
            EndBoxSelection(e.KeyModifiers);
            e.Pointer.Capture(null);
            e.Handled = true;
            return;
        }

        if (!_isPointerDownOnItem)
            return;

        EndItemInteraction();
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        EndItemInteraction();
        CancelBoxSelection();
    }

    private WorkspaceViewModel? WorkspaceViewModel => DataContext as WorkspaceViewModel;

    private void StartBoxSelection(PointerPressedEventArgs e)
    {
        _boxSelectionStartPoint = CanvasPointToWorldPoint(e.GetPosition(this));
        _boxSelectionCurrentPoint = _boxSelectionStartPoint;
        _isPointerDownOnBoxSelection = true;
        _isBoxSelecting = false;
        _isBoxSelectionAdditive = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        e.Pointer.Capture(this);
        e.Handled = true;
    }

    private void UpdateBoxSelection(PointerEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            EndBoxSelection(e.KeyModifiers);
            e.Pointer.Capture(null);
            return;
        }

        _boxSelectionCurrentPoint = CanvasPointToWorldPoint(e.GetPosition(this));

        if (!_isBoxSelecting)
        {
            var deltaX = _boxSelectionCurrentPoint.X - _boxSelectionStartPoint.X;
            var deltaY = _boxSelectionCurrentPoint.Y - _boxSelectionStartPoint.Y;

            if (Math.Abs(deltaX) < DragThreshold && Math.Abs(deltaY) < DragThreshold)
                return;

            _isBoxSelecting = true;
        }

        InvalidateVisual();
    }

    private void EndBoxSelection(KeyModifiers keyModifiers)
    {
        if (!_isPointerDownOnBoxSelection)
            return;

        var vm = WorkspaceViewModel;
        if (vm is not null)
        {
            if (_isBoxSelecting)
                vm.SelectItemsInBounds(GetBoxSelectionWorldRect(), _isBoxSelectionAdditive || keyModifiers.HasFlag(KeyModifiers.Shift));
            else
                vm.ClearSelection();
        }

        _isPointerDownOnBoxSelection = false;
        _isBoxSelecting = false;
        _isBoxSelectionAdditive = false;
        _boxSelectionStartPoint = default;
        _boxSelectionCurrentPoint = default;
        InvalidateVisual();
    }

    private void CancelBoxSelection()
    {
        if (!_isPointerDownOnBoxSelection)
            return;

        _isPointerDownOnBoxSelection = false;
        _isBoxSelecting = false;
        _isBoxSelectionAdditive = false;
        _boxSelectionStartPoint = default;
        _boxSelectionCurrentPoint = default;
        InvalidateVisual();
    }

    private void StartItemInteraction(
        WorkspaceViewModel vm,
        WorkspaceCanvasItemViewModel item,
        ResizeHandleKind resizeHandle,
        PointerPressedEventArgs e)
    {
        _activeItem = item;
        _activeResizeHandle = resizeHandle;
        _pointerAction = resizeHandle == ResizeHandleKind.None
            ? WorkspacePointerAction.Drag
            : WorkspacePointerAction.Resize;
        _isPointerDownOnItem = true;
        _isDraggingItem = false;
        _itemStartPoint = CanvasPointToWorldPoint(e.GetPosition(this));
        _itemStartOrigin = new Point(item.X, item.Y);
        _itemStartSize = new Size(item.Width, item.Height);
        _interactionDeltaX = 0;
        _interactionDeltaY = 0;
        _resizePreviewRect = default;
        _hasResizePreview = false;

        if (_pointerAction == WorkspacePointerAction.Drag)
            vm.BeginMoveSelectedItems(item);

        e.Pointer.Capture(this);
        e.Handled = true;
    }

    private void UpdateItemInteraction(PointerEventArgs e)
    {
        if (_activeItem is null || WorkspaceViewModel is not { } vm)
            return;

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            EndItemInteraction();
            return;
        }

        var current = CanvasPointToWorldPoint(e.GetPosition(this));
        var deltaX = current.X - _itemStartPoint.X;
        var deltaY = current.Y - _itemStartPoint.Y;

        if (!_isDraggingItem && _pointerAction == WorkspacePointerAction.Drag)
        {
            if (Math.Abs(deltaX) < DragThreshold && Math.Abs(deltaY) < DragThreshold)
                return;

            _isDraggingItem = true;
        }

        if (_pointerAction == WorkspacePointerAction.Resize)
        {
            ResizeActiveItem(deltaX, deltaY);
            return;
        }

        _interactionDeltaX = deltaX;
        _interactionDeltaY = deltaY;
        InvalidateVisual();
    }

    private void ResizeActiveItem(double deltaX, double deltaY)
    {
        if (_activeItem is null || WorkspaceViewModel is not { } vm)
            return;

        var aspectRatio = _itemStartSize.Width / Math.Max(1, _itemStartSize.Height);
        var scale = 1d;

        switch (_activeResizeHandle)
        {
            case ResizeHandleKind.TopLeft:
                scale = Math.Max(
                    (_itemStartSize.Width - deltaX) / _itemStartSize.Width,
                    (_itemStartSize.Height - deltaY) / _itemStartSize.Height);
                break;
            case ResizeHandleKind.TopCenter:
                scale = (_itemStartSize.Height - deltaY) / _itemStartSize.Height;
                break;
            case ResizeHandleKind.TopRight:
                scale = Math.Max(
                    (_itemStartSize.Width + deltaX) / _itemStartSize.Width,
                    (_itemStartSize.Height - deltaY) / _itemStartSize.Height);
                break;
            case ResizeHandleKind.MiddleLeft:
                scale = (_itemStartSize.Width - deltaX) / _itemStartSize.Width;
                break;
            case ResizeHandleKind.MiddleRight:
                scale = (_itemStartSize.Width + deltaX) / _itemStartSize.Width;
                break;
            case ResizeHandleKind.BottomLeft:
                scale = Math.Max(
                    (_itemStartSize.Width - deltaX) / _itemStartSize.Width,
                    (_itemStartSize.Height + deltaY) / _itemStartSize.Height);
                break;
            case ResizeHandleKind.BottomCenter:
                scale = (_itemStartSize.Height + deltaY) / _itemStartSize.Height;
                break;
            case ResizeHandleKind.BottomRight:
                scale = Math.Max(
                    (_itemStartSize.Width + deltaX) / _itemStartSize.Width,
                    (_itemStartSize.Height + deltaY) / _itemStartSize.Height);
                break;
        }

        scale = Math.Max(scale, MinItemSize / Math.Min(_itemStartSize.Width, _itemStartSize.Height));

        var newWidth = _itemStartSize.Width * scale;
        var newHeight = newWidth / aspectRatio;
        var newX = _itemStartOrigin.X;
        var newY = _itemStartOrigin.Y;

        switch (_activeResizeHandle)
        {
            case ResizeHandleKind.TopLeft:
                newX = _itemStartOrigin.X + (_itemStartSize.Width - newWidth);
                newY = _itemStartOrigin.Y + (_itemStartSize.Height - newHeight);
                break;
            case ResizeHandleKind.TopCenter:
                newX = _itemStartOrigin.X + (_itemStartSize.Width - newWidth) / 2;
                newY = _itemStartOrigin.Y + (_itemStartSize.Height - newHeight);
                break;
            case ResizeHandleKind.TopRight:
                newY = _itemStartOrigin.Y + (_itemStartSize.Height - newHeight);
                break;
            case ResizeHandleKind.MiddleLeft:
                newX = _itemStartOrigin.X + (_itemStartSize.Width - newWidth);
                newY = _itemStartOrigin.Y + (_itemStartSize.Height - newHeight) / 2;
                break;
            case ResizeHandleKind.MiddleRight:
                newY = _itemStartOrigin.Y + (_itemStartSize.Height - newHeight) / 2;
                break;
            case ResizeHandleKind.BottomLeft:
                newX = _itemStartOrigin.X + (_itemStartSize.Width - newWidth);
                break;
            case ResizeHandleKind.BottomCenter:
                newX = _itemStartOrigin.X + (_itemStartSize.Width - newWidth) / 2;
                break;
        }

        _resizePreviewRect = new Rect(newX, newY, newWidth, newHeight);
        _hasResizePreview = true;
        InvalidateVisual();
    }

    private void EndItemInteraction()
    {
        if (!_isPointerDownOnItem)
            return;

        if (WorkspaceViewModel is { } vm)
        {
            if (_pointerAction == WorkspacePointerAction.Drag)
            {
                if (_isDraggingItem)
                    vm.MoveSelectedItems(_interactionDeltaX, _interactionDeltaY);

                vm.EndMoveSelectedItems();
            }
            else if (_pointerAction == WorkspacePointerAction.Resize)
            {
                if (_activeItem is not null && _hasResizePreview)
                {
                    vm.ResizeItem(
                        _activeItem,
                        _resizePreviewRect.X,
                        _resizePreviewRect.Y,
                        _resizePreviewRect.Width,
                        _resizePreviewRect.Height);
                }

                vm.CommitInteractiveItemChanges();
            }
        }

        _isPointerDownOnItem = false;
        _isDraggingItem = false;
        _pointerAction = WorkspacePointerAction.None;
        _activeResizeHandle = ResizeHandleKind.None;
        _activeItem = null;
        _interactionDeltaX = 0;
        _interactionDeltaY = 0;
        _resizePreviewRect = default;
        _hasResizePreview = false;
        InvalidateVisual();
    }

    private WorkspaceHitTestResult HitTestWorkspace(Point canvasPoint)
    {
        var vm = WorkspaceViewModel;
        if (vm is null)
            return default;

        var worldPoint = CanvasPointToWorldPoint(canvasPoint);

        foreach (var item in vm.Items.OrderByDescending(item => item.ZIndex))
        {
            if (!item.IsSelected)
                continue;

            var itemRect = GetWorldItemRect(item);
            if (GetRemoveButtonRect(itemRect).Contains(worldPoint))
                return new WorkspaceHitTestResult(item, ResizeHandleKind.None, item);

            var resizeHandle = HitTestResizeHandle(itemRect, worldPoint);
            if (resizeHandle != ResizeHandleKind.None)
                return new WorkspaceHitTestResult(item, resizeHandle, null);
        }

        foreach (var item in vm.Items.OrderByDescending(item => item.ZIndex))
        {
            var itemRect = GetWorldItemRect(item);
            var hitRect = item.IsSelected
                ? itemRect.Inflate(WorkspaceCanvasItemViewModel.SelectionChromePadding)
                : itemRect;

            if (hitRect.Contains(worldPoint))
                return new WorkspaceHitTestResult(item, ResizeHandleKind.None, null);
        }

        return default;
    }

    private void UpdateWorkspaceCursor(Point canvasPoint)
    {
        var hit = HitTestWorkspace(canvasPoint);

        if (hit.RemoveItem is not null)
        {
            Cursor = new Cursor(StandardCursorType.Hand);
            return;
        }

        Cursor = hit.ResizeHandle switch
        {
            ResizeHandleKind.TopLeft or ResizeHandleKind.BottomRight => new Cursor(StandardCursorType.TopLeftCorner),
            ResizeHandleKind.TopRight or ResizeHandleKind.BottomLeft => new Cursor(StandardCursorType.TopRightCorner),
            ResizeHandleKind.TopCenter or ResizeHandleKind.BottomCenter => new Cursor(StandardCursorType.TopSide),
            ResizeHandleKind.MiddleLeft or ResizeHandleKind.MiddleRight => new Cursor(StandardCursorType.LeftSide),
            _ when hit.Item is not null => new Cursor(StandardCursorType.SizeAll),
            _ => Cursor.Default
        };
    }

    private Point CanvasPointToWorldPoint(Point canvasPoint)
    {
        var vm = WorkspaceViewModel;
        return vm is null
            ? canvasPoint
            : new Point(canvasPoint.X + vm.BoardStartX, canvasPoint.Y + vm.BoardStartY);
    }

    private Rect GetRenderRect(WorkspaceCanvasItemViewModel item)
    {
        if (_pointerAction == WorkspacePointerAction.Resize
            && _hasResizePreview
            && ReferenceEquals(item, _activeItem))
        {
            return WorldRectToCanvasRect(_resizePreviewRect);
        }

        var itemRect = new Rect(item.DisplayX, item.DisplayY, item.Width, item.Height);

        if (_pointerAction == WorkspacePointerAction.Drag && _isDraggingItem && item.IsSelected)
            itemRect = itemRect.Translate(new Vector(_interactionDeltaX, _interactionDeltaY));

        return itemRect;
    }

    private Rect WorldRectToCanvasRect(Rect worldRect)
    {
        var vm = WorkspaceViewModel;
        if (vm is null)
            return worldRect;

        return new Rect(
            worldRect.X - vm.BoardStartX,
            worldRect.Y - vm.BoardStartY,
            worldRect.Width,
            worldRect.Height);
    }

    private Rect GetBoxSelectionWorldRect()
    {
        var left = Math.Min(_boxSelectionStartPoint.X, _boxSelectionCurrentPoint.X);
        var top = Math.Min(_boxSelectionStartPoint.Y, _boxSelectionCurrentPoint.Y);
        var right = Math.Max(_boxSelectionStartPoint.X, _boxSelectionCurrentPoint.X);
        var bottom = Math.Max(_boxSelectionStartPoint.Y, _boxSelectionCurrentPoint.Y);

        return new Rect(left, top, right - left, bottom - top);
    }

    private static Rect GetWorldItemRect(WorkspaceCanvasItemViewModel item)
    {
        return new Rect(item.X, item.Y, item.Width, item.Height);
    }

    private static ResizeHandleKind HitTestResizeHandle(Rect itemRect, Point worldPoint)
    {
        var centers = GetHandleCenters(itemRect);

        for (var i = 0; i < centers.Count; i++)
        {
            var center = centers[i];
            var distanceX = worldPoint.X - center.X;
            var distanceY = worldPoint.Y - center.Y;

            if (distanceX * distanceX + distanceY * distanceY > ResizeHandleRadius * ResizeHandleRadius)
                continue;

            return i switch
            {
                0 => ResizeHandleKind.TopLeft,
                1 => ResizeHandleKind.TopCenter,
                2 => ResizeHandleKind.TopRight,
                3 => ResizeHandleKind.MiddleLeft,
                4 => ResizeHandleKind.MiddleRight,
                5 => ResizeHandleKind.BottomLeft,
                6 => ResizeHandleKind.BottomCenter,
                7 => ResizeHandleKind.BottomRight,
                _ => ResizeHandleKind.None
            };
        }

        return ResizeHandleKind.None;
    }

    private void RefreshItemSubscriptions()
    {
        ClearItemSubscriptions();

        _observedCollection = Items as INotifyCollectionChanged;

        if (_observedCollection is not null)
            _observedCollection.CollectionChanged += OnItemsCollectionChanged;

        if (Items is not null)
        {
            foreach (var item in Items)
                SubscribeItem(item);
        }

        InvalidateVisual();
    }

    private void ClearItemSubscriptions()
    {
        if (_observedCollection is not null)
            _observedCollection.CollectionChanged -= OnItemsCollectionChanged;

        foreach (var item in _observedItems)
            item.PropertyChanged -= OnItemPropertyChanged;

        _observedItems.Clear();
        _observedCollection = null;
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            RefreshItemSubscriptions();
            return;
        }

        if (e.OldItems is not null)
        {
            foreach (WorkspaceCanvasItemViewModel item in e.OldItems)
                UnsubscribeItem(item);
        }

        if (e.NewItems is not null)
        {
            foreach (WorkspaceCanvasItemViewModel item in e.NewItems)
                SubscribeItem(item);
        }

        InvalidateVisual();
    }

    private void SubscribeItem(WorkspaceCanvasItemViewModel item)
    {
        if (_observedItems.Contains(item))
            return;

        _observedItems.Add(item);
        item.PropertyChanged += OnItemPropertyChanged;
    }

    private void UnsubscribeItem(WorkspaceCanvasItemViewModel item)
    {
        if (!_observedItems.Remove(item))
            return;

        item.PropertyChanged -= OnItemPropertyChanged;
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(WorkspaceCanvasItemViewModel.X)
            or nameof(WorkspaceCanvasItemViewModel.Y)
            or nameof(WorkspaceCanvasItemViewModel.DisplayX)
            or nameof(WorkspaceCanvasItemViewModel.DisplayY)
            or nameof(WorkspaceCanvasItemViewModel.Width)
            or nameof(WorkspaceCanvasItemViewModel.Height)
            or nameof(WorkspaceCanvasItemViewModel.ZIndex)
            or nameof(WorkspaceCanvasItemViewModel.IsSelected)
            or nameof(WorkspaceCanvasItemViewModel.Bitmap)
            or nameof(WorkspaceCanvasItemViewModel.ThumbnailBitmap)
            or nameof(WorkspaceCanvasItemViewModel.HalfBitmap)
            or nameof(WorkspaceCanvasItemViewModel.QuarterBitmap))
        {
            InvalidateVisual();
        }
    }

    private static bool Intersects(Rect a, Rect b)
    {
        return a.Right >= b.Left
            && a.Left <= b.Right
            && a.Bottom >= b.Top
            && a.Top <= b.Bottom;
    }
}
