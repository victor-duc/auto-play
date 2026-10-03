using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using AutoPlay.Domain.Geometry;
using AutoPlay.Windows.Windowing;

namespace AutoPlay.App.Views;

/// <summary>
/// A semi-transparent, movable and resizable window that the user places over the target area.
/// Its bounds, in physical pixels, become the target region.
/// </summary>
public partial class FramingWindow : Window
{
    private readonly PixelRect? _initialBounds;

    public FramingWindow(string confirmText, string instructions, PixelRect? initialBounds)
    {
        InitializeComponent();
        ConfirmButton.Content = confirmText;
        InstructionsText.Text = instructions;
        _initialBounds = initialBounds;
        if (initialBounds is not null)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
        }

        SourceInitialized += OnSourceInitialized;
        SizeChanged += (_, _) => UpdateSizeText();
        LocationChanged += (_, _) => UpdateSizeText();
    }

    /// <summary>The selected target region, in physical screen pixels; null if the user cancelled.</summary>
    public PixelRect? TargetRegion { get; private set; }

    private nint Handle => new WindowInteropHelper(this).Handle;

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        if (_initialBounds is { } bounds)
        {
            WindowGeometry.SetBounds(Handle, bounds);
        }

        UpdateSizeText();
    }

    private void UpdateSizeText()
    {
        if (Handle != 0)
        {
            var bounds = WindowGeometry.GetBounds(Handle);
            SizeText.Text = $"{bounds.Width} × {bounds.Height} px";
        }
    }

    private void OnDragAreaMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void OnConfirmClick(object sender, RoutedEventArgs e)
    {
        TargetRegion = WindowGeometry.GetBounds(Handle);
        DialogResult = true;
    }
}
