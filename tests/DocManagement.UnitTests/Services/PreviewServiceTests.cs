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
        Assert.Equal(PreviewStatus.Pending, document.PreviewStatus);

        var message = Assert.Single(_repository.OutboxMessages);
        Assert.Equal(OutboxConstants.PreviewRequestedType, message.Type);
        Assert.Equal(document.Id.ToString(), message.Payload);
        Assert.Null(message.ProcessedAt);
    }

    [Fact]
    public async Task CreateAsync_DoesNotWriteOutboxMessage_WhenUpdateFails()
    {
        var document = _repository.Seed(TestDocuments.Create());
        _repository.UpdateFailures.Enqueue(new ConcurrencyConflictException("changed meanwhile"));

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => _sut.CreateAsync(document.Id, Ct));

        Assert.Empty(_repository.OutboxMessages);
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

        Assert.Equal(PreviewStatus.Ready, document.PreviewStatus);
        Assert.Equal(_urlHelper.Generate(document), document.PreviewUrl);
        Assert.Equal(1, _repository.SuccessfulUpdates);
    }

    [Theory]
    [InlineData(PreviewStatus.None)]
    [InlineData(PreviewStatus.Ready)]
    public async Task GenerateAsync_Skips_WhenDocumentIsNoLongerPending(PreviewStatus status)
    {
        var document = _repository.Seed(TestDocuments.Create(previewStatus: status));

        await _sut.GenerateAsync(document.Id, Ct);

        Assert.Equal(status, document.PreviewStatus);
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

        Assert.Equal(0, _repository.SuccessfulUpdates);
        Assert.Empty(_repository.UpdateFailures);
    }

    [Fact]
    public async Task GenerateAsync_MarksFailed_OnUnexpectedError()
    {
        var document = _repository.Seed(TestDocuments.Create(previewStatus: PreviewStatus.Pending));
        _repository.UpdateFailures.Enqueue(new InvalidOperationException("storage is down"));

        await _sut.GenerateAsync(document.Id, Ct);

        Assert.Equal(PreviewStatus.Failed, document.PreviewStatus);
        Assert.Null(document.PreviewUrl);
        Assert.Equal(1, _repository.SuccessfulUpdates);
    }
}
