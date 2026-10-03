using AutoPlay.Core.Abstractions;
using AutoPlay.Domain.Geometry;
using AutoPlay.Domain.Imaging;

namespace AutoPlay.Core.Tests.Fakes;

internal sealed class FakeScreenCapture : IScreenCapture
{
    public List<PixelRect> Captures { get; } = [];

    public RawImage Capture(PixelRect area)
    {
        Captures.Add(area);
        return TestImages.Solid(area.Width, area.Height);
    }
}

internal sealed class FakeInputDriver : IInputDriver
{
    public List<PixelPoint> Clicks { get; } = [];

    public PixelPoint CursorPosition { get; set; }

    /// <summary>When set, the cursor is moved to this position after the given click (simulates the user).</summary>
    public (int AfterClick, PixelPoint Position)? UserMove { get; set; }

    public void Click(PixelPoint point)
    {
        Clicks.Add(point);
        CursorPosition = point;
        if (UserMove is { } move && move.AfterClick == Clicks.Count)
        {
            CursorPosition = move.Position;
        }
    }

    public PixelPoint GetCursorPosition() => CursorPosition;
}

/// <summary>Returns scripted results; the last result is repeated once the script is exhausted.</summary>
internal sealed class FakeTemplateMatcher(params MatchResult[] results) : ITemplateMatcher
{
    private int _calls;

    public List<PixelSize> RequestedSizes { get; } = [];

    public MatchResult FindBestMatch(RawImage image, RawImage templateImage, PixelSize templateSize)
    {
        RequestedSizes.Add(templateSize);
        return results[Math.Min(_calls++, results.Length - 1)];
    }
}

internal sealed class FakePowerManager : IPowerManager
{
    public int ActiveRequests { get; private set; }

    public int TotalRequests { get; private set; }

    public IDisposable PreventSleep()
    {
        ActiveRequests++;
        TotalRequests++;
        return new Release(this);
    }

    private sealed class Release(FakePowerManager owner) : IDisposable
    {
        public void Dispose() => owner.ActiveRequests--;
    }
}

/// <summary>A codec storing raw pixels with a small header, to test storage without OpenCV.</summary>
internal sealed class FakeImageCodec : IImageCodec
{
    public byte[] EncodePng(RawImage image) =>
        [.. BitConverter.GetBytes(image.Width), .. BitConverter.GetBytes(image.Height), .. image.Pixels];

    public RawImage Decode(byte[] data) =>
        new(BitConverter.ToInt32(data, 0), BitConverter.ToInt32(data, 4), data[8..]);
}
