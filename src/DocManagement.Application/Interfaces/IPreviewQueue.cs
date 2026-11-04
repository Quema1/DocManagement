namespace DocManagement.Application.Interfaces;

public interface IPreviewQueue
{
    ValueTask EnqueueAsync(Guid documentId, CancellationToken cancellationToken = default);
}
