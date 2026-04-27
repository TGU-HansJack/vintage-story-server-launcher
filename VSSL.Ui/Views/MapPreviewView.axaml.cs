using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using VSSL.Ui.ViewModels;

namespace VSSL.Ui.Views;

public partial class MapPreviewView : UserControl
{
    private const double MinZoom = 0.2;
    private const double MaxZoom = 16.0;
    private const double ZoomStep = 1.15;

    private bool _isDragging;
    private bool _isInitialized;
    private Point _dragStartPoint;
    private Vector _dragStartOffset;
    private ScrollViewer? _dragScrollViewer;
    private Cursor? _previousCursor;
    private readonly ScaleTransform _colorScaleTransform = new();
    private readonly ScaleTransform _grayscaleScaleTransform = new();

    private ScaleTransform ColorScaleTransform => _colorScaleTransform;

    private ScaleTransform GrayscaleScaleTransform => _grayscaleScaleTransform;

    public MapPreviewView()
    {
        InitializeComponent();
        ColorTransformHost.LayoutTransform = _colorScaleTransform;
        GrayscaleTransformHost.LayoutTransform = _grayscaleScaleTransform;
        ColorScrollViewer.AddHandler(
            InputElement.PointerWheelChangedEvent,
            ColorScrollViewer_OnPointerWheelChanged,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
            handledEventsToo: true);
        GrayscaleScrollViewer.AddHandler(
            InputElement.PointerWheelChangedEvent,
            GrayscaleScrollViewer_OnPointerWheelChanged,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
            handledEventsToo: true);
        _isInitialized = true;
        ResetCoordinateDisplay();
    }

    public MapPreviewView(MapPreviewViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private void MapPreviewTabs_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        ResetCoordinateDisplay();
    }

    private void ColorScrollViewer_OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        HandleMouseWheel(ColorScrollViewer, ColorScaleTransform, e);
    }

    private void GrayscaleScrollViewer_OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        HandleMouseWheel(GrayscaleScrollViewer, GrayscaleScaleTransform, e);
    }

    private void ColorScrollViewer_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        BeginDrag(ColorScrollViewer, e);
    }

    private void GrayscaleScrollViewer_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        BeginDrag(GrayscaleScrollViewer, e);
    }

    private void ColorScrollViewer_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        EndDrag(ColorScrollViewer, e);
    }

    private void GrayscaleScrollViewer_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        EndDrag(GrayscaleScrollViewer, e);
    }

    private void ColorScrollViewer_OnPointerMoved(object? sender, PointerEventArgs e)
    {
        HandleMouseMove(ColorScrollViewer, ColorScaleTransform, e);
    }

    private void GrayscaleScrollViewer_OnPointerMoved(object? sender, PointerEventArgs e)
    {
        HandleMouseMove(GrayscaleScrollViewer, GrayscaleScaleTransform, e);
    }

    private void ColorScrollViewer_OnPointerExited(object? sender, PointerEventArgs e)
    {
        if (!_isDragging)
            ResetCoordinateDisplay();
    }

    private void GrayscaleScrollViewer_OnPointerExited(object? sender, PointerEventArgs e)
    {
        if (!_isDragging)
            ResetCoordinateDisplay();
    }

    private void HandleMouseWheel(ScrollViewer scrollViewer, ScaleTransform scaleTransform, PointerWheelEventArgs e)
    {
        var oldScale = Math.Max(0.0001, scaleTransform.ScaleX);
        var factor = e.Delta.Y > 0 ? ZoomStep : 1d / ZoomStep;
        var newScale = Math.Clamp(oldScale * factor, MinZoom, MaxZoom);
        if (Math.Abs(newScale - oldScale) < 0.0001)
            return;

        var pointer = e.GetPosition(scrollViewer);
        var contentX = (scrollViewer.Offset.X + pointer.X) / oldScale;
        var contentY = (scrollViewer.Offset.Y + pointer.Y) / oldScale;

        scaleTransform.ScaleX = newScale;
        scaleTransform.ScaleY = newScale;
        scrollViewer.Offset = new Vector(
            Math.Max(0, contentX * newScale - pointer.X),
            Math.Max(0, contentY * newScale - pointer.Y));

        UpdateCoordinateDisplay(scrollViewer, scaleTransform, pointer);
        e.Handled = true;
    }

    private void BeginDrag(ScrollViewer scrollViewer, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(scrollViewer);
        if (!point.Properties.IsLeftButtonPressed)
            return;

        _isDragging = true;
        _dragScrollViewer = scrollViewer;
        _dragStartPoint = e.GetPosition(scrollViewer);
        _dragStartOffset = scrollViewer.Offset;
        _previousCursor = scrollViewer.Cursor;
        scrollViewer.Cursor = new Cursor(StandardCursorType.SizeAll);
        e.Pointer.Capture(scrollViewer);
        e.Handled = true;
    }

    private void EndDrag(ScrollViewer scrollViewer, PointerReleasedEventArgs e)
    {
        if (!_isDragging || !ReferenceEquals(_dragScrollViewer, scrollViewer))
            return;

        _isDragging = false;
        _dragScrollViewer = null;
        scrollViewer.Cursor = _previousCursor;
        _previousCursor = null;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void HandleMouseMove(ScrollViewer scrollViewer, ScaleTransform scaleTransform, PointerEventArgs e)
    {
        var pointer = e.GetPosition(scrollViewer);
        if (_isDragging && ReferenceEquals(_dragScrollViewer, scrollViewer))
        {
            var delta = pointer - _dragStartPoint;
            scrollViewer.Offset = new Vector(
                Math.Max(0, _dragStartOffset.X - delta.X),
                Math.Max(0, _dragStartOffset.Y - delta.Y));
        }

        UpdateCoordinateDisplay(scrollViewer, scaleTransform, pointer);
    }

    private void UpdateCoordinateDisplay(ScrollViewer scrollViewer, ScaleTransform scaleTransform, Point pointer)
    {
        if (DataContext is not MapPreviewViewModel vm
            || !vm.HasMapPreview
            || vm.MapPreviewWidth <= 0
            || vm.MapPreviewHeight <= 0)
        {
            ResetCoordinateDisplay();
            return;
        }

        var scale = Math.Max(0.0001, scaleTransform.ScaleX);
        var imageX = (scrollViewer.Offset.X + pointer.X) / scale;
        var imageY = (scrollViewer.Offset.Y + pointer.Y) / scale;
        var pixelX = (int)Math.Floor(imageX);
        var pixelY = (int)Math.Floor(imageY);

        if (pixelX < 0 || pixelY < 0 || pixelX >= vm.MapPreviewWidth || pixelY >= vm.MapPreviewHeight)
        {
            ResetCoordinateDisplay();
            return;
        }

        var samplingStep = Math.Max(1, vm.MapPreviewSamplingStep);
        var internalBlockX = vm.MapPreviewMinChunkX * 32 + pixelX * samplingStep;
        var internalBlockZ = vm.MapPreviewMinChunkZ * 32 + pixelY * samplingStep;
        var worldBlockX = vm.MapPreviewMapSizeX > 0 ? internalBlockX - vm.MapPreviewMapSizeX / 2 : internalBlockX;
        var worldBlockZ = vm.MapPreviewMapSizeZ > 0 ? internalBlockZ - vm.MapPreviewMapSizeZ / 2 : internalBlockZ;
        var chunkX = FloorDiv(worldBlockX, 32);
        var chunkZ = FloorDiv(worldBlockZ, 32);

        if (!_isInitialized)
            return;

        MapCoordinateText.Text = vm.BuildCoordinateText(worldBlockX, worldBlockZ, chunkX, chunkZ);
    }

    private void ResetCoordinateDisplay()
    {
        if (!_isInitialized)
            return;

        if (DataContext is MapPreviewViewModel vm)
            MapCoordinateText.Text = vm.CoordinatePlaceholderText;
        else
            MapCoordinateText.Text = "Coordinates: -";
    }

    private static int FloorDiv(int value, int divisor)
    {
        var quotient = value / divisor;
        var remainder = value % divisor;
        if (remainder != 0 && ((value < 0) ^ (divisor < 0)))
            quotient--;
        return quotient;
    }
}
