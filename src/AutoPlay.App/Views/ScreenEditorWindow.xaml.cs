using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AutoPlay.Presentation.ViewModels;
using AutoPlay.Domain.Geometry;
using AutoPlay.Domain.Recording;

namespace AutoPlay.App.Views;

/// <summary>
/// Displays a frozen capture and lets the user draw, move and resize location rectangles on it.
/// Mouse positions are read relative to <c>Surface</c>, whose units are capture pixels.
/// </summary>
public partial class ScreenEditorWindow : Window
{
    private enum DragMode
    {
        None,
        Create,
        Redraw,
        Move,
        Resize,
    }

    private readonly ScreenEditorViewModel _viewModel;
    private DragMode _dragMode;
    private PixelPoint _dragStart;
    private PixelRect _originalBounds;
    private RectHandle _resizeHandle;
    private LocationViewModel? _dragTarget;
    private bool _closeConfirmed;

    /// <summary>Margin around the capture when it is fitted to the window.</summary>
    private const double ViewportPadding = 16;
    private const double MinZoom = 0.1;
    private const double MaxZoom = 16;

    /// <summary>Zoom factor of one mouse wheel notch.</summary>
    private const double ZoomStep = 1.25;

    private bool _fitToWindow = true;
    private bool _spacePressed;
    private bool _isPanning;
    private Point _panStart;
    private Point _panOrigin;

    public ScreenEditorWindow(ScreenEditorViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.CloseRequested += OnCloseRequested;
        Loaded += OnLoaded;
        Viewport.LostMouseCapture += (_, _) =>
        {
            _isPanning = false;
            Viewport.Cursor = null;
        };
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_viewModel.ScreenName))
        {
            ScreenNameBox.Focus();
        }
        else
        {
            InputLayer.Focus();
        }
    }

    private void OnCloseRequested(object? sender, bool saved)
    {
        _closeConfirmed = true;
        DialogResult = saved;
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!_closeConfirmed && !_viewModel.ConfirmDiscard())
        {
            e.Cancel = true;
        }
    }

    private void OnViewportSizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Until the user zooms or pans, the capture follows the size of the window.
        if (_fitToWindow)
        {
            FitToWindow();
        }
    }

    private void OnFitClick(object sender, RoutedEventArgs e) => FitToWindow();

    private void OnActualSizeClick(object sender, RoutedEventArgs e) =>
        ZoomAt(new Point(Viewport.ActualWidth / 2, Viewport.ActualHeight / 2), 1);

    /// <summary>Shows the whole capture, never enlarged, centered in the viewport.</summary>
    private void FitToWindow()
    {
        var availableWidth = Math.Max(1, Viewport.ActualWidth - (2 * ViewportPadding));
        var availableHeight = Math.Max(1, Viewport.ActualHeight - (2 * ViewportPadding));
        var scale = Math.Min(1, Math.Min(availableWidth / _viewModel.PixelWidth, availableHeight / _viewModel.PixelHeight));
        SetView(
            scale,
            (Viewport.ActualWidth - (_viewModel.PixelWidth * scale)) / 2,
            (Viewport.ActualHeight - (_viewModel.PixelHeight * scale)) / 2);
        _fitToWindow = true;
    }

    /// <summary>Zooms to <paramref name="scale"/> keeping the capture point under <paramref name="anchor"/> (viewport coordinates) in place.</summary>
    private void ZoomAt(Point anchor, double scale)
    {
        var matrix = SurfaceTransform.Matrix;
        var current = matrix.M11;
        scale = Math.Clamp(scale, MinZoom, MaxZoom);
        SetView(
            scale,
            anchor.X - ((anchor.X - matrix.OffsetX) * scale / current),
            anchor.Y - ((anchor.Y - matrix.OffsetY) * scale / current));
        _fitToWindow = false;
    }

    private void SetView(double scale, double offsetX, double offsetY)
    {
        SurfaceTransform.Matrix = new Matrix(scale, 0, 0, scale, offsetX, offsetY);
        _viewModel.DisplayScale = scale;
        ZoomText.Text = $"{scale * 100:0}%";

        // Sharp pixels when zoomed in, to place rectangles precisely; smooth when zoomed out.
        RenderOptions.SetBitmapScalingMode(CaptureImage, scale >= 2 ? BitmapScalingMode.NearestNeighbor : BitmapScalingMode.HighQuality);
    }

    private void OnViewportMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var factor = Math.Pow(ZoomStep, e.Delta / 120.0);
        ZoomAt(e.GetPosition(Viewport), SurfaceTransform.Matrix.M11 * factor);
        e.Handled = true;
    }

    private void OnViewportPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        var startsPan = e.ChangedButton == MouseButton.Middle || (e.ChangedButton == MouseButton.Left && _spacePressed);
        if (!startsPan || _dragMode != DragMode.None)
        {
            return;
        }

        _panStart = e.GetPosition(Viewport);
        _panOrigin = new Point(SurfaceTransform.Matrix.OffsetX, SurfaceTransform.Matrix.OffsetY);
        _isPanning = Viewport.CaptureMouse();
        Viewport.Cursor = Cursors.ScrollAll;
        e.Handled = true;
    }

    private void OnViewportPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isPanning)
        {
            return;
        }

        var position = e.GetPosition(Viewport);
        SetView(SurfaceTransform.Matrix.M11, _panOrigin.X + position.X - _panStart.X, _panOrigin.Y + position.Y - _panStart.Y);
        _fitToWindow = false;
        e.Handled = true;
    }

    private void OnViewportPreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isPanning)
        {
            return;
        }

        _isPanning = false;
        Viewport.ReleaseMouseCapture();
        Viewport.Cursor = null;
        e.Handled = true;
    }

    private void OnPreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space)
        {
            _spacePressed = false;
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var typing = Keyboard.FocusedElement is TextBox;
        if (e.Key == Key.Space && !typing && (InputLayer.IsKeyboardFocused || Viewport.IsMouseOver))
        {
            // Space held down turns a left drag into a pan (and must not press the focused button).
            _spacePressed = true;
            e.Handled = true;
        }
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key is Key.D0 or Key.NumPad0)
        {
            FitToWindow();
            e.Handled = true;
        }
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key is Key.D1 or Key.NumPad1)
        {
            OnActualSizeClick(this, e);
            e.Handled = true;
        }
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key is Key.OemPlus or Key.Add or Key.OemMinus or Key.Subtract)
        {
            var factor = e.Key is Key.OemPlus or Key.Add ? ZoomStep : 1 / ZoomStep;
            ZoomAt(new Point(Viewport.ActualWidth / 2, Viewport.ActualHeight / 2), SurfaceTransform.Matrix.M11 * factor);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            if (_dragMode != DragMode.None)
            {
                CancelDrag();
            }
            else
            {
                _viewModel.CancelCommand.Execute(null);
            }

            e.Handled = true;
        }
        else if (e.Key == Key.Delete && Keyboard.FocusedElement is not TextBox)
        {
            _viewModel.DeleteLocationCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnInputMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        InputLayer.Focus();
        var point = GetPixelPosition(e);
        var selected = _viewModel.SelectedLocation;

        if (selected is not null && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) && selected.Bounds.Contains(point))
        {
            selected.SetClickPoint(point);
            e.Handled = true;
            return;
        }

        if (selected is not null && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            // Ctrl+drag redraws the selected location (re-positioning), keeping its name and settings.
            var corner = GetEdgePosition(e);
            StartDrag(DragMode.Redraw, corner, selected);
            UpdateDraftRectangle(new PixelRect(corner.X, corner.Y, 0, 0));
            DraftRectangle.Visibility = Visibility.Visible;
            e.Handled = true;
            return;
        }

        var handle = selected is null ? RectHandle.None : RectEditing.HitTest(selected.Bounds, point, _viewModel.HandleTolerance);
        if (handle is not (RectHandle.None or RectHandle.Body))
        {
            StartDrag(DragMode.Resize, point, selected);
            _resizeHandle = handle;
        }
        else if (FindLocationAt(point) is { } hit)
        {
            _viewModel.SelectedLocation = hit;
            StartDrag(DragMode.Move, point, hit);
        }
        else
        {
            _viewModel.SelectedLocation = null;
            var corner = GetEdgePosition(e);
            StartDrag(DragMode.Create, corner, null);
            UpdateDraftRectangle(new PixelRect(corner.X, corner.Y, 0, 0));
            DraftRectangle.Visibility = Visibility.Visible;
        }

        e.Handled = true;
    }

    private void OnInputMouseMove(object sender, MouseEventArgs e)
    {
        var point = GetPixelPosition(e);
        switch (_dragMode)
        {
            case DragMode.None:
                InputLayer.Cursor = GetHoverCursor(point);
                break;
            case DragMode.Create or DragMode.Redraw:
                UpdateDraftRectangle(RectEditing.FromCorners(_dragStart, GetEdgePosition(e)));
                break;
            case DragMode.Move:
                var moved = _originalBounds.Offset(point.X - _dragStart.X, point.Y - _dragStart.Y);
                _dragTarget!.MoveTo(RectEditing.KeepInside(moved, _viewModel.CaptureSize));
                break;
            case DragMode.Resize:
                _dragTarget!.ResizeTo(RectEditing.Resize(_originalBounds, _resizeHandle, GetEdgePosition(e), _viewModel.CaptureSize));
                break;
        }
    }

    private void OnInputMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragMode == DragMode.Create)
        {
            var point = GetEdgePosition(e);
            var bounds = RectEditing.IsClick(_dragStart, point)
                ? RectEditing.CenteredAt(_dragStart, RectEditing.DefaultLocationSize, _viewModel.CaptureSize)
                : RectEditing.Normalize(RectEditing.FromCorners(_dragStart, point), _viewModel.CaptureSize);
            _viewModel.AddLocation(bounds);
        }
        else if (_dragMode == DragMode.Redraw && !RectEditing.IsClick(_dragStart, GetEdgePosition(e)))
        {
            _dragTarget!.RedrawTo(RectEditing.Normalize(RectEditing.FromCorners(_dragStart, GetEdgePosition(e)), _viewModel.CaptureSize));
        }

        EndDrag();
        e.Handled = true;
    }

    private void OnInputLostMouseCapture(object sender, MouseEventArgs e)
    {
        // Capture lost in the middle of a drag (e.g. Alt+Tab): restore the location.
        if (_dragMode != DragMode.None)
        {
            CancelDrag();
        }
    }

    private void StartDrag(DragMode mode, PixelPoint start, LocationViewModel? target)
    {
        _dragMode = mode;
        _dragStart = start;
        _dragTarget = target;
        _originalBounds = target?.Bounds ?? default;
        InputLayer.CaptureMouse();
    }

    private void CancelDrag()
    {
        if (_dragMode == DragMode.Move)
        {
            _dragTarget?.MoveTo(_originalBounds);
        }
        else if (_dragMode == DragMode.Resize)
        {
            _dragTarget?.ResizeTo(_originalBounds);
        }

        EndDrag();
    }

    private void EndDrag()
    {
        _dragMode = DragMode.None;
        _dragTarget = null;
        DraftRectangle.Visibility = Visibility.Collapsed;
        if (InputLayer.IsMouseCaptured)
        {
            InputLayer.ReleaseMouseCapture();
        }
    }

    private LocationViewModel? FindLocationAt(PixelPoint point) =>
        _viewModel.Locations.LastOrDefault(l => l.Bounds.Contains(point));

    private Cursor GetHoverCursor(PixelPoint point)
    {
        if (_viewModel.SelectedLocation is { } selected)
        {
            switch (RectEditing.HitTest(selected.Bounds, point, _viewModel.HandleTolerance))
            {
                case RectHandle.TopLeft or RectHandle.BottomRight:
                    return Cursors.SizeNWSE;
                case RectHandle.TopRight or RectHandle.BottomLeft:
                    return Cursors.SizeNESW;
            }
        }

        return FindLocationAt(point) is null ? Cursors.Cross : Cursors.SizeAll;
    }

    /// <summary>The pixel under the mouse.</summary>
    private PixelPoint GetPixelPosition(MouseEventArgs e)
    {
        var position = e.GetPosition(Surface);
        return new PixelPoint(
            Math.Clamp((int)Math.Floor(position.X), 0, _viewModel.PixelWidth - 1),
            Math.Clamp((int)Math.Floor(position.Y), 0, _viewModel.PixelHeight - 1));
    }

    /// <summary>The pixel boundary nearest to the mouse, used for rectangle corners (0 to width inclusive).</summary>
    private PixelPoint GetEdgePosition(MouseEventArgs e)
    {
        var position = e.GetPosition(Surface);
        return new PixelPoint(
            Math.Clamp((int)Math.Round(position.X), 0, _viewModel.PixelWidth),
            Math.Clamp((int)Math.Round(position.Y), 0, _viewModel.PixelHeight));
    }

    private void UpdateDraftRectangle(PixelRect rect)
    {
        Canvas.SetLeft(DraftRectangle, rect.Left);
        Canvas.SetTop(DraftRectangle, rect.Top);
        DraftRectangle.Width = rect.Width;
        DraftRectangle.Height = rect.Height;
    }
}
