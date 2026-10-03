using AutoPlay.Domain.Imaging;
using AutoPlay.Domain.Model;
using AutoPlay.Domain.Recording;

namespace AutoPlay.Application.UseCases;

/// <summary>A screen as edited in the screen editor, before validation.</summary>
/// <param name="Id">The ID of the screen (a new one for a new screen).</param>
/// <param name="Capture">The frozen capture the locations were drawn on.</param>
public sealed record ScreenDraft(
    Guid Id,
    string Name,
    DelayRange DefaultDelay,
    RawImage Capture,
    IReadOnlyList<LocationDraft> Locations);
