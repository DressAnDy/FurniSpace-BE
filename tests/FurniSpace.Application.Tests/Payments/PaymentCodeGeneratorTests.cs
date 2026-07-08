#nullable enable

using System;
using System.Linq;
using FurniSpace.Application.Common.Payments;
using Xunit;

namespace FurniSpace.Application.Tests.Payments;

public sealed class PaymentCodeGeneratorTests
{
    [Theory]
    [InlineData("FS", 8)]
    [InlineData("FS", 10)]
    public void Generate_WithValidPrefix_ReturnsExpectedFormat(string prefix, int randomDigits)
    {
        var code = PaymentCodeGenerator.Generate(prefix, randomDigits);

        Assert.StartsWith(prefix, code, StringComparison.Ordinal);
        Assert.Equal(prefix.Length + randomDigits, code.Length);
        Assert.True(code.AsSpan(prefix.Length).ToString().All(char.IsDigit));
    }

    [Fact]
    public void Generate_WithEmptyPrefix_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => PaymentCodeGenerator.Generate(string.Empty, 8));
    }

    [Theory]
    [InlineData(7)]
    [InlineData(11)]
    public void Generate_WithInvalidDigitCount_ThrowsArgumentOutOfRangeException(int randomDigits)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PaymentCodeGenerator.Generate("FS", randomDigits));
    }
}
