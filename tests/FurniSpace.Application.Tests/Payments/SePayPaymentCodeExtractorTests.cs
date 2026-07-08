#nullable enable

using FurniSpace.Application.Common.Payments;
using Xunit;

namespace FurniSpace.Application.Tests.Payments;

public sealed class SePayPaymentCodeExtractorTests
{
    private const string Pattern = @"FS[0-9]{8,10}";

    [Fact]
    public void Extract_WithPrimaryCode_ReturnsPrimaryCode()
    {
        var result = SePayPaymentCodeExtractor.Extract("FS12345678", null, null, Pattern);

        Assert.Equal("FS12345678", result);
    }

    [Fact]
    public void Extract_WithContentFallback_ReturnsMatchedCode()
    {
        var result = SePayPaymentCodeExtractor.Extract(null, "Chuyen tien FS87654321 cho don hang", null, Pattern);

        Assert.Equal("FS87654321", result);
    }

    [Fact]
    public void Extract_WithNoMatch_ReturnsNull()
    {
        var result = SePayPaymentCodeExtractor.Extract(null, "Chuyen tien khong co ma", null, Pattern);

        Assert.Null(result);
    }
}
