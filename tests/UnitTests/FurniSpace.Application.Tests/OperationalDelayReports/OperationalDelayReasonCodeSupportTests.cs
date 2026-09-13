#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.Common.OperationalDelayReports;
using FurniSpace.Application.DTOs.OperationalDelayReports;
using FurniSpace.Application.Services.OperationalDelayReports;
using FurniSpace.Domain.Enums;
using Xunit;

namespace FurniSpace.Application.Tests.OperationalDelayReports;

public sealed class OperationalDelayReasonCodeSupportTests
{
    [Theory]
    [InlineData("MATERIAL_DELAY", ProductionDelayReasonCode.MATERIAL_DELAY)]
    [InlineData("material_delay", ProductionDelayReasonCode.MATERIAL_DELAY)]
    [InlineData("OTHER", ProductionDelayReasonCode.OTHER)]
    public void TryParseProductionReasonCode_WithValidValue_ReturnsTrue(
        string input,
        ProductionDelayReasonCode expected)
    {
        var parsed = OperationalDelayReasonCodeSupport.TryParseProductionReasonCode(input, out var reasonCode);

        Assert.True(parsed);
        Assert.Equal(expected, reasonCode);
    }

    [Theory]
    [InlineData("WEATHER")]
    [InlineData("SITE_NOT_READY")]
    [InlineData("INVALID")]
    public void TryParseProductionReasonCode_WithInvalidValue_ReturnsFalse(string input)
    {
        var parsed = OperationalDelayReasonCodeSupport.TryParseProductionReasonCode(input, out _);

        Assert.False(parsed);
    }

    [Theory]
    [InlineData("WEATHER", DeliveryDelayReasonCode.WEATHER)]
    [InlineData("site_not_ready", DeliveryDelayReasonCode.SITE_NOT_READY)]
    public void TryParseDeliveryReasonCode_WithValidValue_ReturnsTrue(
        string input,
        DeliveryDelayReasonCode expected)
    {
        var parsed = OperationalDelayReasonCodeSupport.TryParseDeliveryReasonCode(input, out var reasonCode);

        Assert.True(parsed);
        Assert.Equal(expected, reasonCode);
    }

    [Fact]
    public void ValidateProductionReasonCode_RejectsDeliveryReasonField()
    {
        var result = OperationalDelayReasonCodeSupport.ValidateProductionReasonCode<OperationalDelayReportDto>(
            "MATERIAL_DELAY",
            "WEATHER",
            "Detail");

        Assert.NotNull(result);
        Assert.Equal(400, result!.Status);
        Assert.Contains("Delivery reason code is not accepted", result.Message);
    }

    [Fact]
    public void ValidateDeliveryReasonCode_RejectsProductionReasonField()
    {
        var result = OperationalDelayReasonCodeSupport.ValidateDeliveryReasonCode<OperationalDelayReportDto>(
            "MATERIAL_DELAY",
            "WEATHER",
            "Detail");

        Assert.NotNull(result);
        Assert.Equal(400, result!.Status);
        Assert.Contains("Production reason code is not accepted", result.Message);
    }

    [Fact]
    public void ValidateProductionReasonCode_RequiresReasonDetail()
    {
        var result = OperationalDelayReasonCodeSupport.ValidateProductionReasonCode<OperationalDelayReportDto>(
            "MATERIAL_DELAY",
            null,
            "   ");

        Assert.NotNull(result);
        Assert.Equal(400, result!.Status);
        Assert.Equal("Reason detail is required.", result.Message);
    }
}
