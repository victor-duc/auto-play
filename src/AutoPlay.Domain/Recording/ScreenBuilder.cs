using AutoPlay.Domain.Geometry;
using AutoPlay.Domain.Imaging;
using AutoPlay.Domain.Model;

namespace AutoPlay.Domain.Recording;

/// <summary>Validates screens being edited and converts them to and from the stored model.</summary>
public static class ScreenBuilder
{
    /// <summary>Returns the validation errors, or an empty list if the screen can be saved.</summary>
    /// <param name="otherScreenNames">Names of the other screens of the profile.</param>
    public static IReadOnlyList<string> Validate(
        string screenName,
        IReadOnlyList<LocationDraft> locations,
        PixelSize captureSize,
        IEnumerable<string> otherScreenNames)
    {
        var errors = new List<string>();
        var name = screenName.Trim();

        if (name.Length == 0)
        {
            errors.Add("The screen name is required.");
        }
        else if (otherScreenNames.Any(other => string.Equals(other.Trim(), name, StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add($"Another screen is already named '{name}'.");
        }

        if (locations.Any(l => string.IsNullOrWhiteSpace(l.Name)))
        {
            errors.Add("Every location needs a name.");
        }

        foreach (var duplicate in locations
            .Where(l => !string.IsNullOrWhiteSpace(l.Name))
            .GroupBy(l => l.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1))
        {
            errors.Add($"Several locations are named '{duplicate.Key}'.");
        }

        var captureBounds = new PixelRect(0, 0, captureSize.Width, captureSize.Height);
        foreach (var location in locations)
        {
            if (location.Bounds.IsEmpty || !captureBounds.Contains(location.Bounds))
            {
                errors.Add($"The rectangle of '{location.Name}' must lie within the capture.");
            }
            else if (!location.Bounds.Contains(location.ClickPoint))
            {
                errors.Add($"The click point of '{location.Name}' must lie within its rectangle.");
            }

            if (location.MatchThreshold is < 0 or > 1)
            {
                errors.Add($"The match threshold of '{location.Name}' must be between 0 and 1.");
            }
        }

        return errors;
    }

    /// <summary>Builds the screen to store, with the template image of each location cropped from the capture.</summary>
    public static (Screen Screen, IReadOnlyDictionary<Guid, RawImage> Templates) Build(
        Guid screenId,
        string screenName,
        DelayRange defaultDelay,
        RawImage capture,
        IReadOnlyList<LocationDraft> locations)
    {
        var region = new PixelRect(0, 0, capture.Width, capture.Height);
        var screen = new Screen
        {
            Id = screenId,
            Name = screenName.Trim(),
            RecordedRegionSize = capture.Size,
            DefaultDelay = defaultDelay,
            Locations = locations
                .Select(draft => new Location
                {
                    Id = draft.Id,
                    Name = draft.Name.Trim(),
                    Positioning = PositioningMode.Proportional,
                    Bounds = CoordinateMapper.ToNormalized(draft.Bounds, region),
                    ClickPoint = CoordinateMapper.ToNormalized(draft.ClickPoint, region),
                    MatchThreshold = draft.MatchThreshold,
                })
                .ToList(),
        };

        var templates = locations.ToDictionary(l => l.Id, l => capture.Crop(l.Bounds));
        return (screen, templates);
    }

    /// <summary>Converts a stored screen back to drafts, in pixel coordinates of its capture.</summary>
    public static IReadOnlyList<LocationDraft> ToDrafts(Screen screen, PixelSize captureSize)
    {
        var region = new PixelRect(0, 0, captureSize.Width, captureSize.Height);
        return screen.Locations
            .Select(location =>
            {
                var bounds = RectEditing.KeepInside(CoordinateMapper.ToPixel(location.Bounds, region), captureSize);
                return new LocationDraft
                {
                    Id = location.Id,
                    Name = location.Name,
                    Bounds = bounds,
                    ClickPoint = RectEditing.Clamp(CoordinateMapper.ToPixel(location.ClickPoint, region), bounds),
                    MatchThreshold = location.MatchThreshold,
                };
            })
            .ToList();
    }
}
