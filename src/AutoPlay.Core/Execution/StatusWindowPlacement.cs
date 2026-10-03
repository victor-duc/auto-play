using AutoPlay.Core.Geometry;

namespace AutoPlay.Core.Execution;

/// <summary>
/// Chooses where to show the run status window so that it does not cover the target region:
/// a covered region would break the verification and could receive clicks.
/// </summary>
public static class StatusWindowPlacement
{
    /// <param name="windowSize">Size of the status window.</param>
    /// <param name="targetRegion">The region being automated.</param>
    /// <param name="workArea">Work area of the monitor showing the target region.</param>
    /// <param name="margin">Gap between the window and the region or the work area edges.</param>
    public static PixelRect Place(PixelSize windowSize, PixelRect targetRegion, PixelRect workArea, int margin = 8)
    {
        int w = windowSize.Width, h = windowSize.Height;
        PixelRect[] candidates =
        [
            // Right of the region, aligned with its top.
            new(targetRegion.Right + margin, targetRegion.Top, w, h),

            // Left of the region, aligned with its top.
            new(targetRegion.Left - margin - w, targetRegion.Top, w, h),

            // Below the region, aligned with its right edge.
            new(targetRegion.Right - w, targetRegion.Bottom + margin, w, h),

            // Above the region, aligned with its right edge.
            new(targetRegion.Right - w, targetRegion.Top - margin - h, w, h),
        ];

        foreach (var candidate in candidates)
        {
            // Slide along the edge to fit the work area, then check that it still avoids the region.
            var fitted = FitInside(candidate, workArea);
            if (workArea.Contains(fitted) && fitted.Intersect(targetRegion).IsEmpty)
            {
                return fitted;
            }
        }

        // The region fills the screen: use the bottom-right corner of the work area.
        return FitInside(new PixelRect(workArea.Right - margin - w, workArea.Bottom - margin - h, w, h), workArea);
    }

    private static PixelRect FitInside(PixelRect rect, PixelRect area) =>
        rect with
        {
            Left = Math.Clamp(rect.Left, area.Left, Math.Max(area.Left, area.Right - rect.Width)),
            Top = Math.Clamp(rect.Top, area.Top, Math.Max(area.Top, area.Bottom - rect.Height)),
        };
}
