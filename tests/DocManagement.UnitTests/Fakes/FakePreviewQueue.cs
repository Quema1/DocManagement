using DocManagement.Application.Interfaces;

namespace DocManagement.UnitTests.Fakes;

internal sealed class FakePreviewQueue : IPreviewQueue
{
    public List<Guid> Enqueued { get; } = [];

    public Exception? FailWith { get; set; }

    public ValueTask EnqueueAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        if (FailWith is not null)
        {
            return ValueTask.FromException(FailWith);
        }

        Enqueued.Add(documentId);
        return ValueTask.CompletedTask;
    }
}
