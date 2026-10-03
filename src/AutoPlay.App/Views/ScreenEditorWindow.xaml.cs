using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AutoPlay.App.ViewModels;
using AutoPlay.Core.Geometry;
using AutoPlay.Core.Recording;

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

    public ScreenEditorWindow(ScreenEditorViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.CloseRequested += OnCloseRequested;
        Loaded += OnLoaded;
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

    private void OnViewboxSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var scale = Math.Min(
            CaptureViewbox.ActualWidth / _viewModel.PixelWidth,
            CaptureViewbox.ActualHeight / _viewModel.PixelHeight);
        _viewModel.DisplayScale = scale > 0 ? Math.Min(1, scale) : 1;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
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
            case DragMode.Create:
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
