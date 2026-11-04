using DocManagement.Application.Options;
using DocManagement.Application.Helpers;
using DocManagement.UnitTests.Fakes;
using Microsoft.Extensions.Options;

namespace DocManagement.UnitTests.Helpers;

public class UrlHelperTests
{
    private readonly UrlHelper _sut = new(Options.Create(new PreviewOptions { BaseUrl = "https://previews.test/" }));

    [Fact]
    public void Generate_BuildsUrlFromBaseUrlAndContentHash()
    {
        var url = _sut.Generate(TestDocuments.Create());

        Assert.Matches("^https://previews\\.test/previews/[0-9a-f]{16}\\.png$", url);
    }

    [Fact]
    public void Generate_ReturnsDifferentUrls_ForDifferentDocumentsWithSameContent()
    {
        var first = TestDocuments.Create(name: "Report", description: "Q3");
        var second = TestDocuments.Create(name: "Report", description: "Q3");

        Assert.NotEqual(_sut.Generate(first), _sut.Generate(second));
    }

    [Fact]
    public void Generate_IsDeterministic_ForSameContent()
    {
        var document = TestDocuments.Create();

        Assert.Equal(_sut.Generate(document), _sut.Generate(document));
    }

    [Fact]
    public void Generate_ChangesUrl_WhenContentChanges()
    {
        var document = TestDocuments.Create(name: "Report", description: "v1");
        var before = _sut.Generate(document);

        document.Description = "v2";

        Assert.NotEqual(before, _sut.Generate(document));
    }

    [Fact]
    public void Generate_DistinguishesFieldBoundaries()
    {
        var first = TestDocuments.Create(name: "ab", description: "c");
        var second = TestDocuments.Create(name: "a", description: "bc");
        second.Id = first.Id;

        Assert.NotEqual(_sut.Generate(first), _sut.Generate(second));
    }
}
