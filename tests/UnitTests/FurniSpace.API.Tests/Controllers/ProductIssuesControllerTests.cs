#nullable enable

using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.API.Controllers.Projects;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.ProductIssues;
using FurniSpace.Application.Interfaces.ProductIssues;
using FurniSpace.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FurniSpace.API.Tests.Controllers;

public sealed class ProductIssuesControllerTests
{
    [Theory]
    [InlineData(nameof(ProductIssuesController.Create), "CUSTOMER")]
    [InlineData(nameof(ProductIssuesController.GetByOrder), "CUSTOMER,SALES,PRODUCTION,ADMIN")]
    [InlineData(nameof(ProductIssuesController.GetByProject), "CUSTOMER,SALES,PRODUCTION,ADMIN")]
    [InlineData(nameof(ProductIssuesController.GetDetail), "CUSTOMER,SALES,PRODUCTION,ADMIN")]
    public void Actions_UseExpectedRoles(string actionName, string expectedRoles)
    {
        var authorize = typeof(ProductIssuesController)
            .GetMethods()
            .Single(method => method.Name == actionName)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .Single();

        Assert.Equal(expectedRoles, authorize.Roles);
    }

    [Fact]
    public async Task Create_ReturnsServiceResultAndPassesJsonRequest()
    {
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var orderItemId = Guid.NewGuid();
        var evidenceFileId = Guid.NewGuid();
        var service = new FakeProductIssueService();
        var controller = BuildController(service, userId);
        var request = new CreateProductIssueRequestDto
        {
            OrderItemId = orderItemId,
            IssueType = DeliveryProductIssueType.DAMAGED,
            Description = "Corner chipped",
            EvidenceFileIds = [evidenceFileId]
        };

        var actionResult = await controller.Create(orderId, request);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(201, objectResult.StatusCode);
        Assert.Equal(orderId, service.OrderId);
        Assert.Equal(userId, service.CurrentUserId);
        Assert.Equal(orderItemId, service.CreateRequest!.OrderItemId);
        Assert.Single(service.CreateRequest.EvidenceFileIds);
        Assert.Equal(evidenceFileId, service.CreateRequest.EvidenceFileIds[0]);
    }

    [Fact]
    public async Task GetByOrder_WithoutUser_ReturnsUnauthorized()
    {
        var controller = BuildController(new FakeProductIssueService(), userId: null);

        var actionResult = await controller.GetByOrder(Guid.NewGuid());

        Assert.IsType<UnauthorizedResult>(actionResult);
    }

    private static ProductIssuesController BuildController(
        IDeliveryProductIssueReportService service,
        Guid? userId)
    {
        var controller = new ProductIssuesController(service);
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

    private sealed class FakeProductIssueService : IDeliveryProductIssueReportService
    {
        public Guid OrderId { get; private set; }
        public Guid CurrentUserId { get; private set; }
        public CreateProductIssueRequestDto? CreateRequest { get; private set; }

        public Task<ServiceResult<ProductIssueReportDto>> CreateAsync(
            Guid orderId,
            Guid currentUserId,
            CreateProductIssueRequestDto request,
            CancellationToken cancellationToken = default)
        {
            OrderId = orderId;
            CurrentUserId = currentUserId;
            CreateRequest = request;
            return Task.FromResult(ServiceResult<ProductIssueReportDto>.Created(
                new ProductIssueReportDto(),
                "Product issue report submitted successfully."));
        }

        public Task<ServiceResult<PrepareProductIssueEvidenceUploadResponseDto>> PrepareEvidenceUploadAsync(
            Guid orderId,
            Guid currentUserId,
            PrepareProductIssueEvidenceUploadRequestDto request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ServiceResult<PrepareProductIssueEvidenceUploadResponseDto>.Created(
                new PrepareProductIssueEvidenceUploadResponseDto(),
                "Product issue evidence upload URL created successfully."));

        public Task<ServiceResult<CompleteProductIssueEvidenceUploadResponseDto>> CompleteEvidenceUploadAsync(
            Guid orderId,
            Guid currentUserId,
            CompleteProductIssueEvidenceUploadRequestDto request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ServiceResult<CompleteProductIssueEvidenceUploadResponseDto>.Success(
                new CompleteProductIssueEvidenceUploadResponseDto(),
                "Product issue evidence uploaded successfully."));

        public Task<ServiceResult<ProductIssueReportListResponseDto>> GetByOrderAsync(
            Guid orderId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<ProductIssueReportListResponseDto>.Success(
                new ProductIssueReportListResponseDto(),
                "Product issue reports retrieved successfully."));

        public Task<ServiceResult<ProductIssueReportListResponseDto>> GetByProjectAsync(
            Guid projectId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<ProductIssueReportListResponseDto>.Success(
                new ProductIssueReportListResponseDto(),
                "Product issue reports retrieved successfully."));

        public Task<ServiceResult<ProductIssueReportDto>> GetDetailAsync(
            Guid issueId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<ProductIssueReportDto>.Success(
                new ProductIssueReportDto(),
                "Product issue report retrieved successfully."));

        public Task<ServiceResult<ProductIssueReportDto>> ResolveAsync(
            Guid issueId,
            Guid currentUserId,
            ResolveProductIssueRequestDto request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<ProductIssueReportDto>.Success(
                new ProductIssueReportDto(),
                "Product issue report resolved successfully."));
    }
}
