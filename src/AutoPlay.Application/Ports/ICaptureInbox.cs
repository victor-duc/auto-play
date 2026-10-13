using AutoPlay.Domain.Imaging;
using AutoPlay.Domain.Model;

namespace AutoPlay.Application.Ports;

/// <summary>Driven port: storage of the quick captures waiting to be annotated.</summary>
public interface ICaptureInbox
{
    IReadOnlyList<PendingCapture> LoadPendingCaptures(Guid profileId);

    void SavePendingCapture(Guid profileId, PendingCapture capture, RawImage image);

    RawImage LoadPendingCaptureImage(Guid profileId, Guid captureId);

    void DeletePendingCapture(Guid profileId, Guid captureId);
}
