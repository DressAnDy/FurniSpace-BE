#nullable enable

using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.API.Controllers.Projects;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.OperationalDelayReports;
using FurniSpace.Application.Interfaces.OperationalDelayReports;
using FurniSpace.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FurniSpace.API.Tests.Controllers;

public sealed class OperationalDelayReportsControllerTests
{
    [Theory]
    [InlineData(nameof(OperationalDelayReportsController.CreateProductionReport), "SALES,PRODUCTION,ADMIN")]
    [InlineData(nameof(OperationalDelayReportsController.CreateDeliveryReport), "SALES,PRODUCTION,ADMIN")]
    [InlineData(nameof(OperationalDelayReportsController.GetByProject), "SALES,PRODUCTION,ADMIN")]
    [InlineData(nameof(OperationalDelayReportsController.GetDetail), "SALES,PRODUCTION,ADMIN")]
    public void Actions_UseExpectedRoles(string actionName, string expectedRoles)
    {
        var authorize = typeof(OperationalDelayReportsController)
            .GetMethods()
            .Single(method => method.Name == actionName)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .Single();

        Assert.Equal(expectedRoles, authorize.Roles);
    }

    [Fact]
    public async Task CreateProductionReport_ReturnsServiceResultAndPassesRequest()
    {
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var request = new CreateProductionDelayReportRequestDto
        {
            ProductionRequestId = Guid.NewGuid(),
            ReasonDetail = "Supplier delay"
        };
        var service = new FakeOperationalDelayReportService();
        var controller = BuildController(service, userId);

        var actionResult = await controller.CreateProductionReport(projectId, request);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(201, objectResult.StatusCode);
        Assert.Equal(projectId, service.ProjectId);
        Assert.Equal(userId, service.CurrentUserId);
        Assert.Same(request, service.ProductionRequest);
    }

    [Fact]
    public async Task GetByProject_ReturnsServiceResultAndPassesPhase()
    {
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var service = new FakeOperationalDelayReportService();
        var controller = BuildController(service, userId);

        var actionResult = await controller.GetByProject(projectId, OperationalDelayPhase.DELIVERY);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal(projectId, service.ProjectId);
        Assert.Equal(OperationalDelayPhase.DELIVERY, service.Phase);
    }

    [Fact]
    public async Task GetDetail_WithoutUser_ReturnsUnauthorized()
    {
        var controller = BuildController(new FakeOperationalDelayReportService(), userId: null);

        var actionResult = await controller.GetDetail(Guid.NewGuid());

        Assert.IsType<UnauthorizedResult>(actionResult);
    }

    private static OperationalDelayReportsController BuildController(
        IOperationalDelayReportService service,
        Guid? userId)
    {
        var controller = new OperationalDelayReportsController(service);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = userId.HasValue
                    ? new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString())
                    ], "Test"))
                    : new ClaimsPrincipal(new ClaimsIdentity())
            }
        };

        return controller;
    }

    private sealed class FakeOperationalDelayReportService : IOperationalDelayReportService
    {
        public Guid ProjectId { get; private set; }
        public Guid CurrentUserId { get; private set; }
        public Guid ReportId { get; private set; }
        public OperationalDelayPhase Phase { get; private set; }
        public CreateProductionDelayReportRequestDto? ProductionRequest { get; private set; }

        public Task<ServiceResult<OperationalDelayReportDto>> CreateProductionReportAsync(
            Guid projectId,
            Guid currentUserId,
            CreateProductionDelayReportRequestDto request,
            CancellationToken cancellationToken = default)
        {
            ProjectId = projectId;
            CurrentUserId = currentUserId;
            ProductionRequest = request;
            return Task.FromResult(ServiceResult<OperationalDelayReportDto>.Created(
                new OperationalDelayReportDto(),
                "Production delay report recorded successfully."));
        }

        public Task<ServiceResult<OperationalDelayReportDto>> CreateDeliveryReportAsync(
            Guid projectId,
            Guid currentUserId,
            CreateDeliveryDelayReportRequestDto request,
            CancellationToken cancellationToken = default)
        {
            ProjectId = projectId;
            CurrentUserId = currentUserId;
            return Task.FromResult(ServiceResult<OperationalDelayReportDto>.Created(
                new OperationalDelayReportDto(),
                "Delivery delay report recorded successfully."));
        }

        public Task<ServiceResult<OperationalDelayReportListResponseDto>> GetByProjectAsync(
            Guid projectId,
            Guid currentUserId,
            OperationalDelayPhase phase,
            CancellationToken cancellationToken = default)
        {
            ProjectId = projectId;
            CurrentUserId = currentUserId;
            Phase = phase;
            return Task.FromResult(ServiceResult<OperationalDelayReportListResponseDto>.Success(
                new OperationalDelayReportListResponseDto(),
                "Operational delay reports retrieved successfully."));
        }

        public Task<ServiceResult<OperationalDelayReportDto>> GetDetailAsync(
            Guid reportId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            ReportId = reportId;
            CurrentUserId = currentUserId;
            return Task.FromResult(ServiceResult<OperationalDelayReportDto>.Success(
                new OperationalDelayReportDto(),
                "Operational delay report retrieved successfully."));
        }
    }
}
