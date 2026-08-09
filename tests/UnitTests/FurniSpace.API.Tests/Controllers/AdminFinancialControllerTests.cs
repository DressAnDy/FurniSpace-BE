#nullable enable

using System.Threading;
using System.Threading.Tasks;
using FurniSpace.API.Controllers.Admin;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Financial;
using FurniSpace.Application.Interfaces.Financial;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FurniSpace.API.Tests.Controllers;

public sealed class AdminFinancialControllerTests
{
    [Fact]
    public async Task GetSummary_ReturnsServiceResultThroughBaseController()
    {
        var response = new AdminFinancialSummaryDto
        {
            Currency = "VND",
            CollectedAmount = 100m
        };
        var service = new FakeAdminFinancialService(
            ServiceResult<AdminFinancialSummaryDto>.Success(
                response,
                "Admin financial summary retrieved successfully."));
        var controller = new AdminFinancialController(service);
        var query = new AdminFinancialSummaryQueryDto { Period = "THIS_MONTH" };

        var actionResult = await controller.GetSummary(query);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Same(query, service.Query);
    }

    private sealed class FakeAdminFinancialService : IAdminFinancialService
    {
        private readonly ServiceResult<AdminFinancialSummaryDto> _result;

        public FakeAdminFinancialService(ServiceResult<AdminFinancialSummaryDto> result)
        {
            _result = result;
        }

        public AdminFinancialSummaryQueryDto? Query { get; private set; }

        public Task<ServiceResult<AdminFinancialSummaryDto>> GetSummaryAsync(
            AdminFinancialSummaryQueryDto query,
            CancellationToken cancellationToken = default)
        {
            Query = query;
            return Task.FromResult(_result);
        }
    }
}
