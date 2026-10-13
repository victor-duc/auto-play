using AutoPlay.Domain.Imaging;
using AutoPlay.Domain.Recording;

namespace AutoPlay.Application.UseCases;

/// <summary>A new capture of an existing screen, with its locations re-positioned on it.</summary>
/// <param name="Capture">The new capture.</param>
/// <param name="Locations">The locations of the screen, in pixels of the new capture.</param>
/// <param name="NotFound">Locations whose image was not found in the new capture: left at their proportional position.</param>
public sealed record RecaptureResult(RawImage Capture, IReadOnlyList<LocationDraft> Locations, IReadOnlyList<Guid> NotFound);
