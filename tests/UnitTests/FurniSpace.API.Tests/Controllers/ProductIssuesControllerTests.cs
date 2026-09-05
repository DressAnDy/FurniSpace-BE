#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.API.Controllers.Projects;
using FurniSpace.API.DTOs.ProductIssues;
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
    public async Task Create_ReturnsServiceResultAndMapsFormRequest()
    {
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var orderItemId = Guid.NewGuid();
        var service = new FakeProductIssueService();
        var controller = BuildController(service, userId);
        var form = new CreateProductIssueFormRequest
        {
            OrderItemId = orderItemId,
            IssueType = DeliveryProductIssueType.DAMAGED,
            Description = "Corner chipped",
            Files = [CreateFormFile("damage.jpg", "photo")]
        };

        var actionResult = await controller.Create(orderId, form);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(201, objectResult.StatusCode);
        Assert.Equal(orderId, service.OrderId);
        Assert.Equal(userId, service.CurrentUserId);
        Assert.Equal(orderItemId, service.CreateRequest!.OrderItemId);
        Assert.Single(service.CreateRequest.EvidenceFiles);
    }

    [Fact]
    public void CreateProductIssueFormRequest_ToRequestDto_SkipsEmptyFiles()
    {
        var request = new CreateProductIssueFormRequest
        {
            OrderItemId = Guid.NewGuid(),
            IssueType = DeliveryProductIssueType.OTHER,
            Description = "Issue",
            Files =
            [
                CreateFormFile("valid.jpg", "photo"),
                CreateFormFile("empty.jpg", "")
            ]
        };

        var dto = request.ToRequestDto();

        Assert.Equal(request.OrderItemId, dto.OrderItemId);
        Assert.Equal(DeliveryProductIssueType.OTHER, dto.IssueType);
        Assert.Single(dto.EvidenceFiles);
        Assert.Equal("valid.jpg", dto.EvidenceFiles[0].OriginalFileName);
    }

    [Fact]
    public async Task GetByOrder_WithoutUser_ReturnsUnauthorized()
    {
        var controller = BuildController(new FakeProductIssueService(), userId: null);

        var actionResult = await controller.GetByOrder(Guid.NewGuid());

        Assert.IsType<UnauthorizedResult>(actionResult);
    }

    private static IFormFile CreateFormFile(string fileName, string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "Files", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/jpeg"
        };
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
    }
}
