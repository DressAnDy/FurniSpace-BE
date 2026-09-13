using System;
using System.Globalization;
using FurniSpace.Application.Common.Payments;
using Xunit;

namespace FurniSpace.Application.Tests.Payments;

public sealed class PayOsOrderCodeGeneratorTests
{
    [Fact]
    public void Generate_ReturnsPositiveNumericOrderCode()
    {
        var orderCode = PayOsOrderCodeGenerator.Generate();

        Assert.True(orderCode > 0);
        Assert.Equal(12, orderCode.ToString(CultureInfo.InvariantCulture).Length);
    }
}
