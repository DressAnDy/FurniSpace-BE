using System;
using FurniSpace.Application.Common.Payments;
using Xunit;

namespace FurniSpace.Application.Tests.Payments;

public sealed class ProjectStartFeeAccessEvaluatorTests
{
    [Theory]
    [InlineData("ADMIN", true)]
    [InlineData("SALES", true)]
    [InlineData("CUSTOMER", false)]
    public void CanManage_ReturnsExpectedAccess(string role, bool expected)
    {
        var salesId = Guid.NewGuid();
        var currentUserId = role == "SALES" ? salesId : Guid.NewGuid();

        var result = ProjectStartFeeAccessEvaluator.CanManage(role, salesId, currentUserId);

        Assert.Equal(expected, result);
    }
}
