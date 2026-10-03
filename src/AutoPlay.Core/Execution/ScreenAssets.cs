using AutoPlay.Domain.Imaging;
using AutoPlay.Domain.Model;

namespace AutoPlay.Core.Execution;

/// <summary>A screen and the template images of its locations, loaded for execution.</summary>
public sealed record ScreenAssets(Screen Screen, IReadOnlyDictionary<Guid, RawImage> Templates);
