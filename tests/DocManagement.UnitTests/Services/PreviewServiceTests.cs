using DocManagement.Application.Exceptions;
using DocManagement.Application.Options;
using DocManagement.Application.Helpers;
using DocManagement.Application.Services;
using DocManagement.Domain;
using DocManagement.Domain.Constants;
using DocManagement.UnitTests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace DocManagement.UnitTests.Services;

public class PreviewServiceTests
{
    private readonly InMemoryDocumentRepository _repository = new();
    private readonly UrlHelper _urlHelper;
    private readonly PreviewService _sut;

    public PreviewServiceTests()
    {
        var options = Options.Create(new PreviewOptions
        {
            BaseUrl = "https://previews.test",
            SimulatedDelay = TimeSpan.Zero,
        });
        _urlHelper = new UrlHelper(options);
        _sut = new PreviewService(_repository, _urlHelper, options, new FakeTimeProvider(), NullLogger<PreviewService>.Instance);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CreateAsync_ReturnsNull_WhenDocumentDoesNotExist()
    {
        Assert.Null(await _sut.CreateAsync(Guid.NewGuid(), Ct));
        Assert.Empty(_repository.OutboxMessages);
    }

    [Theory]
    [InlineData(PreviewStatus.None)]
    [InlineData(PreviewStatus.Failed)]
    public async Task CreateAsync_MarksPendingAndWritesOutboxMessage_WhenNoPreviewYet(PreviewStatus initialStatus)
    {
        var document = _repository.Seed(TestDocuments.Create(previewStatus: initialStatus));

        var result = await _sut.CreateAsync(document.Id, Ct);

        Assert.NotNull(result);
        Assert.Equal(PreviewStatus.Pending, result.Status);
        Assert.Null(result.Url);
        Assert.Equal(PreviewStatus.Pending, _repository.Stored(document.Id).PreviewStatus);

        var message = Assert.Single(_repository.OutboxMessages);
        Assert.Equal(OutboxConstants.PreviewRequestedType, message.Type);
        Assert.Equal(document.Id.ToString(), message.Payload);
        Assert.Null(message.ProcessedAt);
    }

    [Fact]
    public async Task CreateAsync_DoesNotRequestTwice_WhenAlreadyPending()
    {
        var document = _repository.Seed(TestDocuments.Create(previewStatus: PreviewStatus.Pending));

        var result = await _sut.CreateAsync(document.Id, Ct);

        Assert.Equal(PreviewStatus.Pending, result!.Status);
        Assert.Empty(_repository.OutboxMessages);
        Assert.Equal(0, _repository.SuccessfulUpdates);
    }

    [Fact]
    public async Task CreateAsync_ReturnsExistingUrl_WhenAlreadyReady()
    {
        var document = _repository.Seed(TestDocuments.Create(previewStatus: PreviewStatus.Ready, previewUrl: "https://previews.test/p.png"));

        var result = await _sut.CreateAsync(document.Id, Ct);

        Assert.Equal(PreviewStatus.Ready, result!.Status);
        Assert.Equal("https://previews.test/p.png", result.Url);
        Assert.Empty(_repository.OutboxMessages);
    }

    [Fact]
    public async Task GetAsync_ReturnsCurrentState()
    {
        var document = _repository.Seed(TestDocuments.Create(previewStatus: PreviewStatus.Pending));

        var result = await _sut.GetAsync(document.Id, Ct);

        Assert.Equal(document.Id, result!.DocumentId);
        Assert.Equal(PreviewStatus.Pending, result.Status);
    }

    [Fact]
    public async Task GenerateAsync_SetsReadyWithDeterministicUrl()
    {
        var document = _repository.Seed(TestDocuments.Create(previewStatus: PreviewStatus.Pending));

        await _sut.GenerateAsync(document.Id, Ct);

        var stored = _repository.Stored(document.Id);
        Assert.Equal(PreviewStatus.Ready, stored.PreviewStatus);
        Assert.Equal(_urlHelper.Generate(document), stored.PreviewUrl);
        Assert.Equal(1, _repository.SuccessfulUpdates);
    }

    [Theory]
    [InlineData(PreviewStatus.None)]
    [InlineData(PreviewStatus.Ready)]
    public async Task GenerateAsync_Skips_WhenDocumentIsNoLongerPending(PreviewStatus status)
    {
        var document = _repository.Seed(TestDocuments.Create(previewStatus: status));

        await _sut.GenerateAsync(document.Id, Ct);

        Assert.Equal(status, _repository.Stored(document.Id).PreviewStatus);
        Assert.Equal(0, _repository.SuccessfulUpdates);
    }

    [Fact]
    public async Task GenerateAsync_DoesNothing_WhenDocumentWasDeleted()
    {
        await _sut.GenerateAsync(Guid.NewGuid(), Ct);

        Assert.Equal(0, _repository.SuccessfulUpdates);
    }

    [Fact]
    public async Task GenerateAsync_DiscardsResult_OnConcurrencyConflict()
    {
        var document = _repository.Seed(TestDocuments.Create(previewStatus: PreviewStatus.Pending));
        _repository.UpdateFailures.Enqueue(new ConcurrencyConflictException("changed meanwhile"));

        await _sut.GenerateAsync(document.Id, Ct);

        var stored = _repository.Stored(document.Id);
        Assert.Equal(PreviewStatus.Pending, stored.PreviewStatus);
        Assert.Null(stored.PreviewUrl);
        Assert.Equal(0, _repository.SuccessfulUpdates);
    }

    [Fact]
    public async Task GenerateAsync_MarksFailed_OnUnexpectedError()
    {
        var document = _repository.Seed(TestDocuments.Create(previewStatus: PreviewStatus.Pending));
        _repository.UpdateFailures.Enqueue(new InvalidOperationException("storage is down"));

        await _sut.GenerateAsync(document.Id, Ct);

        var stored = _repository.Stored(document.Id);
        Assert.Equal(PreviewStatus.Failed, stored.PreviewStatus);
        Assert.Null(stored.PreviewUrl);
        Assert.Equal(1, _repository.SuccessfulUpdates);
    }

    [Fact]
    public async Task GenerateAsync_DoesNotMarkFailed_WhenCancelled()
    {
        var document = _repository.Seed(TestDocuments.Create(previewStatus: PreviewStatus.Pending));
        _repository.UpdateFailures.Enqueue(new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(() => _sut.GenerateAsync(document.Id, Ct));

        Assert.Equal(PreviewStatus.Pending, _repository.Stored(document.Id).PreviewStatus);
    }

    [Fact]
    public async Task CreateAsync_PropagatesConcurrencyConflict()
    {
        var document = _repository.Seed(TestDocuments.Create());
        _repository.UpdateFailures.Enqueue(new ConcurrencyConflictException("changed meanwhile"));

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => _sut.CreateAsync(document.Id, Ct));

        Assert.Equal(PreviewStatus.None, _repository.Stored(document.Id).PreviewStatus);
        Assert.Empty(_repository.OutboxMessages);
    }

    [Fact]
    public async Task MarkFailedAsync_MarksPendingDocumentFailed()
    {
        var document = _repository.Seed(TestDocuments.Create(previewStatus: PreviewStatus.Pending));

        await _sut.MarkFailedAsync(document.Id, Ct);

        Assert.Equal(PreviewStatus.Failed, _repository.Stored(document.Id).PreviewStatus);
    }

    [Theory]
    [InlineData(PreviewStatus.None)]
    [InlineData(PreviewStatus.Ready)]
    public async Task MarkFailedAsync_LeavesDocument_WhenNotPending(PreviewStatus status)
    {
        var document = _repository.Seed(TestDocuments.Create(previewStatus: status));

        await _sut.MarkFailedAsync(document.Id, Ct);

        Assert.Equal(status, _repository.Stored(document.Id).PreviewStatus);
        Assert.Equal(0, _repository.SuccessfulUpdates);
    }

    [Fact]
    public async Task MarkFailedAsync_SwallowsStorageErrors()
    {
        var document = _repository.Seed(TestDocuments.Create(previewStatus: PreviewStatus.Pending));
        _repository.UpdateFailures.Enqueue(new InvalidOperationException("storage is down"));

        await _sut.MarkFailedAsync(document.Id, Ct);

        Assert.Equal(PreviewStatus.Pending, _repository.Stored(document.Id).PreviewStatus);
    }
}
