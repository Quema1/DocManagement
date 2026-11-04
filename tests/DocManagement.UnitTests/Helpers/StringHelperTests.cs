using DocManagement.Application.Helpers;

namespace DocManagement.UnitTests.Helpers;

public class StringHelperTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TrimToNull_ReturnsNull_WhenValueIsBlank(string? value)
    {
        Assert.Null(StringHelper.TrimToNull(value));
    }

    [Fact]
    public void TrimToNull_TrimsValue()
    {
        Assert.Equal("Report", StringHelper.TrimToNull("  Report  "));
    }
}
