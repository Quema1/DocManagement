using DocManagement.Application.Dtos;
using DocManagement.Application.Services;
using DocManagement.Domain;
using DocManagement.UnitTests.Fakes;
using Microsoft.Extensions.Time.Testing;
using DocManagement.Domain.Constants;

namespace DocManagement.UnitTests.Services;

public class DocumentServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 10, 0, 0, TimeSpan.Zero);

    private readonly InMemoryDocumentRepository _repository = new();
    private readonly FakeTimeProvider _time = new(Now);
    private readonly DocumentService _sut;

    public DocumentServiceTests()
    {
        _sut = new DocumentService(_repository, _time);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CreateAsync_StoresDocumentWithTimestampsAndNoPreview()
    {
        var result = await _sut.CreateAsync(new DocumentRequest { Name = "Report", Description = "Q3" }, Ct);

        var stored = Assert.Single(_repository.All);
        Assert.Equal(stored.Id, result.Id);
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("Report", result.Name);
        Assert.Equal("Q3", result.Description);
        Assert.Equal(Now.UtcDateTime, result.CreatedAt);
        Assert.Equal(Now.UtcDateTime, result.UpdatedAt);
        Assert.Equal(PreviewStatus.None, result.PreviewStatus);
        Assert.Null(result.PreviewUrl);
    }

    [Fact]
    public async Task CreateAsync_TrimsNameAndTurnsBlankDescriptionIntoNull()
    {
        var result = await _sut.CreateAsync(new DocumentRequest { Name = "  Report  ", Description = "   " }, Ct);

        Assert.Equal("Report", result.Name);
        Assert.Null(result.Description);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenDocumentDoesNotExist()
    {
        Assert.Null(await _sut.GetByIdAsync(Guid.NewGuid(), Ct));
    }

    [Fact]
    public async Task GetListAsync_ReturnsRequestedPageAndTotalCount()
    {
        var oldest = _repository.Seed(TestDocuments.Create(createdAt: TestDocuments.CreatedAt));
        _repository.Seed(TestDocuments.Create(createdAt: TestDocuments.CreatedAt.AddDays(1)));
        _repository.Seed(TestDocuments.Create(createdAt: TestDocuments.CreatedAt.AddDays(2)));

        var result = await _sut.GetListAsync(page: 2, pageSize: 2, Ct);

        Assert.Equal(3, result.TotalCount);
        Assert.Equal(2, result.Page);
        Assert.Equal(2, result.PageSize);
        Assert.Equal(oldest.Id, Assert.Single(result.Items).Id);
    }

    [Theory]
    [InlineData(0, 0, 1, 1)]
    [InlineData(-5, 1000, 1, PaginationConstants.MaxPageSize)]
    public async Task GetListAsync_ClampsInvalidPaging(int page, int pageSize, int expectedPage, int expectedPageSize)
    {
        var result = await _sut.GetListAsync(page, pageSize, Ct);

        Assert.Equal(expectedPage, result.Page);
        Assert.Equal(expectedPageSize, result.PageSize);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNull_WhenDocumentDoesNotExist()
    {
        var result = await _sut.UpdateAsync(Guid.NewGuid(), new DocumentRequest { Name = "New" }, Ct);

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateAsync_WhenContentChanges_UpdatesTimestampAndResetsPreview()
    {
        var document = _repository.Seed(TestDocuments.Create(previewStatus: PreviewStatus.Ready, previewUrl: "https://x/old.png"));
        _time.Advance(TimeSpan.FromHours(1));

        var result = await _sut.UpdateAsync(document.Id, new DocumentRequest { Name = "Renamed", Description = "Q3 numbers" }, Ct);

        Assert.NotNull(result);
        Assert.Equal("Renamed", result.Name);
        Assert.Equal(TestDocuments.CreatedAt, result.CreatedAt);
        Assert.Equal(Now.AddHours(1).UtcDateTime, result.UpdatedAt);
        Assert.Equal(PreviewStatus.None, result.PreviewStatus);
        Assert.Null(result.PreviewUrl);
        Assert.Equal(1, _repository.SuccessfulUpdates);
    }

    [Fact]
    public async Task UpdateAsync_WhenContentIsUnchanged_KeepsPreviewAndDoesNotSave()
    {
        var document = _repository.Seed(TestDocuments.Create(previewStatus: PreviewStatus.Ready, previewUrl: "https://x/p.png"));
        _time.Advance(TimeSpan.FromHours(1));

        var result = await _sut.UpdateAsync(document.Id, new DocumentRequest { Name = " Report ", Description = "Q3 numbers" }, Ct);

        Assert.NotNull(result);
        Assert.Equal(TestDocuments.CreatedAt, result.UpdatedAt);
        Assert.Equal(PreviewStatus.Ready, result.PreviewStatus);
        Assert.Equal("https://x/p.png", result.PreviewUrl);
        Assert.Equal(0, _repository.SuccessfulUpdates);
    }

    [Fact]
    public async Task UpdateAsync_WhenPreviewIsPending_ResetsPreview()
    {
        var document = _repository.Seed(TestDocuments.Create(previewStatus: PreviewStatus.Pending));

        var result = await _sut.UpdateAsync(document.Id, new DocumentRequest { Name = "Renamed", Description = "Q3 numbers" }, Ct);

        Assert.Equal(PreviewStatus.None, result!.PreviewStatus);
        Assert.Equal(PreviewStatus.None, _repository.Stored(document.Id).PreviewStatus);
    }

    [Fact]
    public async Task UpdateAsync_TreatsBlankDescriptionAsUnchanged_WhenStoredDescriptionIsNull()
    {
        var document = _repository.Seed(TestDocuments.Create(description: null, previewStatus: PreviewStatus.Ready, previewUrl: "https://x/p.png"));

        var result = await _sut.UpdateAsync(document.Id, new DocumentRequest { Name = "Report", Description = "  " }, Ct);

        Assert.Equal(PreviewStatus.Ready, result!.PreviewStatus);
        Assert.Equal(0, _repository.SuccessfulUpdates);
    }

    [Fact]
    public async Task DeleteAsync_RemovesDocument()
    {
        var document = _repository.Seed(TestDocuments.Create());

        Assert.True(await _sut.DeleteAsync(document.Id, Ct));
        Assert.Empty(_repository.All);
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalse_WhenDocumentDoesNotExist()
    {
        Assert.False(await _sut.DeleteAsync(Guid.NewGuid(), Ct));
    }
}
