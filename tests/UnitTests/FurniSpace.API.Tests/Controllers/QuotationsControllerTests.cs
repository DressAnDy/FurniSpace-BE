#nullable enable

using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.API.Controllers.Projects;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Quotations;
using FurniSpace.Application.Interfaces.Quotations;
using FurniSpace.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FurniSpace.API.Tests.Controllers;

public sealed class QuotationsControllerTests
{
    [Fact]
    public void Controller_RequiresAuthorization()
    {
        var authorize = typeof(QuotationsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .Single();

        Assert.Null(authorize.Roles);
    }

    [Theory]
    [InlineData(nameof(QuotationsController.GetByProject), "CUSTOMER,SALES,DESIGNER,ADMIN")]
    [InlineData(nameof(QuotationsController.GetDetail), "CUSTOMER,SALES,DESIGNER,ADMIN")]
    [InlineData(nameof(QuotationsController.CreateDraft), "SALES,ADMIN")]
    [InlineData(nameof(QuotationsController.Update), "SALES,ADMIN")]
    [InlineData(nameof(QuotationsController.UpdateItemFinancials), "SALES,ADMIN")]
    [InlineData(nameof(QuotationsController.BulkUpdateItemFinancials), "SALES,ADMIN")]
    [InlineData(nameof(QuotationsController.Send), "SALES,ADMIN")]
    [InlineData(nameof(QuotationsController.Accept), "CUSTOMER")]
    [InlineData(nameof(QuotationsController.RequestRevision), "CUSTOMER")]
    [InlineData(nameof(QuotationsController.Revise), "SALES,ADMIN")]
    [InlineData(nameof(QuotationsController.Cancel), "SALES,ADMIN")]
    [InlineData(nameof(QuotationsController.Reject), "CUSTOMER")]
    public void Actions_UseExpectedRoles(string actionName, string expectedRoles)
    {
        var authorize = typeof(QuotationsController)
            .GetMethods()
            .Single(method => method.Name == actionName)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .Single();

        Assert.Equal(expectedRoles, authorize.Roles);
    }

    [Fact]
    public async Task GetByProject_ReturnsServiceResultAndPassesQuery()
    {
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var service = new FakeQuotationService();
        var controller = BuildController(service, userId);

        var actionResult = await controller.GetByProject(projectId, QuotationStatus.SENT);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal(projectId, service.ProjectId);
        Assert.Equal(userId, service.CurrentUserId);
        Assert.Equal(QuotationStatus.SENT, service.Query!.Status);
    }

    [Fact]
    public async Task GetDetail_ReturnsServiceResultAndPassesId()
    {
        var quotationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var service = new FakeQuotationService();
        var controller = BuildController(service, userId);

        var actionResult = await controller.GetDetail(quotationId);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal(quotationId, service.QuotationId);
        Assert.Equal(userId, service.CurrentUserId);
    }

    [Fact]
    public async Task CreateDraft_ReturnsCreatedResultAndPassesId()
    {
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var service = new FakeQuotationService
        {
            CreateResult = ServiceResult<QuotationDetailDto>.Created(new QuotationDetailDto())
        };
        var controller = BuildController(service, userId);

        var actionResult = await controller.CreateDraft(projectId);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(201, objectResult.StatusCode);
        Assert.Equal(projectId, service.ProjectId);
        Assert.Equal(userId, service.CurrentUserId);
    }

    [Fact]
    public async Task Update_ReturnsServiceResultAndPassesRequest()
    {
        var quotationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var request = new UpdateQuotationRequestDto { SalesNote = "Ready" };
        var service = new FakeQuotationService();
        var controller = BuildController(service, userId);

        var actionResult = await controller.Update(quotationId, request);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal(quotationId, service.QuotationId);
        Assert.Same(request, service.UpdateRequest);
    }

    [Fact]
    public async Task UpdateItemFinancials_ReturnsServiceResultAndPassesRequest()
    {
        var quotationId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var request = new UpdateQuotationItemFinancialsRequestDto { Quantity = 2 };
        var service = new FakeQuotationService();
        var controller = BuildController(service, userId);

        var actionResult = await controller.UpdateItemFinancials(quotationId, itemId, request);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal(quotationId, service.QuotationId);
        Assert.Equal(itemId, service.QuotationItemId);
        Assert.Same(request, service.FinancialsRequest);
    }

    [Fact]
    public async Task BulkUpdateItemFinancials_ReturnsServiceResultAndPassesRequest()
    {
        var quotationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var request = new BulkUpdateQuotationItemFinancialsRequestDto();
        var service = new FakeQuotationService();
        var controller = BuildController(service, userId);

        var actionResult = await controller.BulkUpdateItemFinancials(quotationId, request);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal(quotationId, service.QuotationId);
        Assert.Same(request, service.BulkFinancialsRequest);
    }

    [Fact]
    public async Task Send_ReturnsServiceResultAndPassesId()
    {
        var quotationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var service = new FakeQuotationService();
        var controller = BuildController(service, userId);

        var actionResult = await controller.Send(quotationId);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal(quotationId, service.QuotationId);
        Assert.Equal(userId, service.CurrentUserId);
    }

    [Fact]
    public async Task Accept_ReturnsServiceResultAndPassesId()
    {
        var quotationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var service = new FakeQuotationService();
        var controller = BuildController(service, userId);

        var actionResult = await controller.Accept(quotationId);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal(quotationId, service.QuotationId);
        Assert.Equal(userId, service.CurrentUserId);
    }

    [Fact]
    public async Task RequestRevision_ReturnsServiceResultAndPassesRequest()
    {
        var quotationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var request = new RequestQuotationRevisionDto { RevisionReason = "Update warranty." };
        var service = new FakeQuotationService();
        var controller = BuildController(service, userId);

        var actionResult = await controller.RequestRevision(quotationId, request);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal(quotationId, service.QuotationId);
        Assert.Equal(userId, service.CurrentUserId);
        Assert.Same(request, service.RevisionRequest);
    }

    [Fact]
    public async Task Revise_ReturnsServiceResultAndPassesId()
    {
        var quotationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var service = new FakeQuotationService();
        var controller = BuildController(service, userId);

        var actionResult = await controller.Revise(quotationId);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal(quotationId, service.QuotationId);
        Assert.Equal(userId, service.CurrentUserId);
    }

    [Fact]
    public async Task Cancel_ReturnsServiceResultAndPassesId()
    {
        var quotationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var service = new FakeQuotationService();
        var controller = BuildController(service, userId);

        var actionResult = await controller.Cancel(quotationId);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal(quotationId, service.QuotationId);
        Assert.Equal(userId, service.CurrentUserId);
    }

    [Fact]
    public async Task Reject_ReturnsServiceResultAndPassesRequest()
    {
        var quotationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var request = new RejectQuotationRequestDto { RejectReason = "Too expensive." };
        var service = new FakeQuotationService();
        var controller = BuildController(service, userId);

        var actionResult = await controller.Reject(quotationId, request);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal(quotationId, service.QuotationId);
        Assert.Equal(userId, service.CurrentUserId);
        Assert.Same(request, service.RejectRequest);
    }

    [Theory]
    [InlineData(nameof(QuotationsController.GetByProject))]
    [InlineData(nameof(QuotationsController.GetDetail))]
    [InlineData(nameof(QuotationsController.CreateDraft))]
    [InlineData(nameof(QuotationsController.Update))]
    [InlineData(nameof(QuotationsController.UpdateItemFinancials))]
    [InlineData(nameof(QuotationsController.BulkUpdateItemFinancials))]
    [InlineData(nameof(QuotationsController.Send))]
    [InlineData(nameof(QuotationsController.Accept))]
    [InlineData(nameof(QuotationsController.RequestRevision))]
    [InlineData(nameof(QuotationsController.Revise))]
    [InlineData(nameof(QuotationsController.Cancel))]
    [InlineData(nameof(QuotationsController.Reject))]
    public async Task Actions_WithoutUserClaim_ReturnUnauthorized(string actionName)
    {
        var controller = BuildController(new FakeQuotationService(), userId: null);

        var actionResult = actionName switch
        {
            nameof(QuotationsController.GetByProject) => await controller.GetByProject(Guid.NewGuid()),
            nameof(QuotationsController.GetDetail) => await controller.GetDetail(Guid.NewGuid()),
            nameof(QuotationsController.CreateDraft) => await controller.CreateDraft(Guid.NewGuid()),
            nameof(QuotationsController.Update) => await controller.Update(Guid.NewGuid(), new UpdateQuotationRequestDto()),
            nameof(QuotationsController.UpdateItemFinancials) => await controller.UpdateItemFinancials(Guid.NewGuid(), Guid.NewGuid(), new UpdateQuotationItemFinancialsRequestDto()),
            nameof(QuotationsController.BulkUpdateItemFinancials) => await controller.BulkUpdateItemFinancials(Guid.NewGuid(), new BulkUpdateQuotationItemFinancialsRequestDto()),
            nameof(QuotationsController.Send) => await controller.Send(Guid.NewGuid()),
            nameof(QuotationsController.Accept) => await controller.Accept(Guid.NewGuid()),
            nameof(QuotationsController.RequestRevision) => await controller.RequestRevision(Guid.NewGuid(), new RequestQuotationRevisionDto()),
            nameof(QuotationsController.Revise) => await controller.Revise(Guid.NewGuid()),
            nameof(QuotationsController.Cancel) => await controller.Cancel(Guid.NewGuid()),
            _ => await controller.Reject(Guid.NewGuid(), new RejectQuotationRequestDto())
        };

        Assert.IsType<UnauthorizedResult>(actionResult);
    }

    private static QuotationsController BuildController(
        IQuotationService service,
        Guid? userId)
    {
        var controller = new QuotationsController(service);
        if (userId.HasValue)
        {
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString())
                    ], "Test"))
                }
            };
        }

        return controller;
    }

    private sealed class FakeQuotationService : IQuotationService
    {
        public ServiceResult<QuotationListResponseDto> ListResult { get; init; } =
            ServiceResult<QuotationListResponseDto>.Success(new QuotationListResponseDto());

        public ServiceResult<QuotationDetailDto> DetailResult { get; init; } =
            ServiceResult<QuotationDetailDto>.Success(new QuotationDetailDto());

        public ServiceResult<QuotationDetailDto> CreateResult { get; init; } =
            ServiceResult<QuotationDetailDto>.Success(new QuotationDetailDto());

        public Guid ProjectId { get; private set; }
        public Guid QuotationId { get; private set; }
        public Guid QuotationItemId { get; private set; }
        public Guid CurrentUserId { get; private set; }
        public QuotationQueryDto? Query { get; private set; }
        public UpdateQuotationRequestDto? UpdateRequest { get; private set; }
        public UpdateQuotationItemFinancialsRequestDto? FinancialsRequest { get; private set; }
        public BulkUpdateQuotationItemFinancialsRequestDto? BulkFinancialsRequest { get; private set; }
        public RequestQuotationRevisionDto? RevisionRequest { get; private set; }
        public RejectQuotationRequestDto? RejectRequest { get; private set; }

        public Task<ServiceResult<QuotationListResponseDto>> GetByProjectAsync(
            Guid projectId,
            Guid currentUserId,
            QuotationQueryDto query,
            CancellationToken cancellationToken = default)
        {
            ProjectId = projectId;
            CurrentUserId = currentUserId;
            Query = query;
            return Task.FromResult(ListResult);
        }

        public Task<ServiceResult<QuotationDetailDto>> GetDetailAsync(
            Guid quotationId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            QuotationId = quotationId;
            CurrentUserId = currentUserId;
            return Task.FromResult(DetailResult);
        }

        public Task<ServiceResult<QuotationDetailDto>> CreateDraftAsync(
            Guid projectId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            ProjectId = projectId;
            CurrentUserId = currentUserId;
            return Task.FromResult(CreateResult);
        }

        public Task<ServiceResult<QuotationDetailDto>> UpdateAsync(
            Guid quotationId,
            Guid currentUserId,
            UpdateQuotationRequestDto request,
            CancellationToken cancellationToken = default)
        {
            QuotationId = quotationId;
            CurrentUserId = currentUserId;
            UpdateRequest = request;
            return Task.FromResult(DetailResult);
        }

        public Task<ServiceResult<QuotationDetailDto>> UpdateItemFinancialsAsync(
            Guid quotationId,
            Guid quotationItemId,
            Guid currentUserId,
            UpdateQuotationItemFinancialsRequestDto request,
            CancellationToken cancellationToken = default)
        {
            QuotationId = quotationId;
            QuotationItemId = quotationItemId;
            CurrentUserId = currentUserId;
            FinancialsRequest = request;
            return Task.FromResult(DetailResult);
        }

        public Task<ServiceResult<QuotationDetailDto>> BulkUpdateItemFinancialsAsync(
            Guid quotationId,
            Guid currentUserId,
            BulkUpdateQuotationItemFinancialsRequestDto request,
            CancellationToken cancellationToken = default)
        {
            QuotationId = quotationId;
            CurrentUserId = currentUserId;
            BulkFinancialsRequest = request;
            return Task.FromResult(DetailResult);
        }

        public Task<ServiceResult<QuotationDetailDto>> SendAsync(
            Guid quotationId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            QuotationId = quotationId;
            CurrentUserId = currentUserId;
            return Task.FromResult(DetailResult);
        }

        public Task<ServiceResult<QuotationDetailDto>> AcceptAsync(
            Guid quotationId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            QuotationId = quotationId;
            CurrentUserId = currentUserId;
            return Task.FromResult(DetailResult);
        }

        public Task<ServiceResult<QuotationDetailDto>> RequestRevisionAsync(
            Guid quotationId,
            Guid currentUserId,
            RequestQuotationRevisionDto request,
            CancellationToken cancellationToken = default)
        {
            QuotationId = quotationId;
            CurrentUserId = currentUserId;
            RevisionRequest = request;
            return Task.FromResult(DetailResult);
        }

        public Task<ServiceResult<QuotationDetailDto>> ReviseAsync(
            Guid quotationId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            QuotationId = quotationId;
            CurrentUserId = currentUserId;
            return Task.FromResult(DetailResult);
        }

        public Task<ServiceResult<QuotationDetailDto>> CancelAsync(
            Guid quotationId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            QuotationId = quotationId;
            CurrentUserId = currentUserId;
            return Task.FromResult(DetailResult);
        }

        public Task<ServiceResult<QuotationDetailDto>> RejectAsync(
            Guid quotationId,
            Guid currentUserId,
            RejectQuotationRequestDto request,
            CancellationToken cancellationToken = default)
        {
            QuotationId = quotationId;
            CurrentUserId = currentUserId;
            RejectRequest = request;
            return Task.FromResult(DetailResult);
        }
    }
}
