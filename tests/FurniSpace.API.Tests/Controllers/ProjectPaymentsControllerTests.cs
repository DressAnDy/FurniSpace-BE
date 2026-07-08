#nullable enable

using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.API.Controllers.Projects;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Orders;
using FurniSpace.Application.DTOs.Payments;
using FurniSpace.Application.Interfaces.Payments;
using FurniSpace.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FurniSpace.API.Tests.Controllers;

public sealed class ProjectPaymentsControllerTests
{
    [Fact]
    public void CreateProjectStartFeePayment_RequiresSalesOrAdmin()
    {
        var authorize = GetMethodAuthorizeAttribute(nameof(ProjectPaymentsController.CreateProjectStartFeePayment));

        Assert.NotNull(authorize);
        Assert.Equal("SALES,ADMIN", authorize.Roles);
    }

    [Fact]
    public async Task CreateProjectStartFeePayment_WithoutUser_ReturnsUnauthorized()
    {
        var controller = CreateController(new FakePaymentService(), userId: null);

        var result = await controller.CreateProjectStartFeePayment(
            Guid.NewGuid(),
            new CreateProjectStartFeePaymentRequestDto());

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task CreateProjectStartFeePayment_ReturnsServiceResult()
    {
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var service = new FakePaymentService(
            createStartFeeResult: ServiceResult<PaymentDetailDto>.Created(
                new PaymentDetailDto { PaymentId = Guid.NewGuid() },
                "created"));
        var controller = CreateController(service, userId);

        var result = await controller.CreateProjectStartFeePayment(
            projectId,
            new CreateProjectStartFeePaymentRequestDto { Amount = 500000m });

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(201, objectResult.StatusCode);
    }

    [Fact]
    public async Task GetProjectStartFeeStatus_ReturnsServiceResult()
    {
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var service = new FakePaymentService(
            statusResult: ServiceResult<ProjectStartFeeStatusDto>.Success(
                new ProjectStartFeeStatusDto { ProjectId = projectId, ProjectStartFeeStatus = PaymentStatus.PENDING },
                "ok"));
        var controller = CreateController(service, userId);

        var result = await controller.GetProjectStartFeeStatus(projectId);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(200, objectResult.StatusCode);
    }

    private static AuthorizeAttribute? GetMethodAuthorizeAttribute(string methodName)
    {
        var method = typeof(ProjectPaymentsController)
            .GetMethods()
            .Single(methodInfo => methodInfo.Name == methodName);

        return method.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .SingleOrDefault();
    }

    private static ProjectPaymentsController CreateController(FakePaymentService service, Guid? userId)
    {
        var controller = new ProjectPaymentsController(service);
        if (userId.HasValue)
        {
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString())
                    ], "TestAuth"))
                }
            };
        }

        return controller;
    }

    private sealed class FakePaymentService : IPaymentService
    {
        private readonly ServiceResult<PaymentDetailDto>? _createStartFeeResult;
        private readonly ServiceResult<ProjectStartFeeStatusDto>? _statusResult;

        public FakePaymentService(
            ServiceResult<PaymentDetailDto>? createStartFeeResult = null,
            ServiceResult<ProjectStartFeeStatusDto>? statusResult = null)
        {
            _createStartFeeResult = createStartFeeResult;
            _statusResult = statusResult;
        }

        public Task<ServiceResult<PaymentDetailDto>> CreateProjectStartFeePaymentAsync(
            Guid projectId,
            Guid currentUserId,
            CreateProjectStartFeePaymentRequestDto request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                _createStartFeeResult ?? ServiceResult<PaymentDetailDto>.Unauthorized());
        }

        public Task<ServiceResult<ProjectStartFeeStatusDto>> GetProjectStartFeeStatusAsync(
            Guid projectId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                _statusResult ?? ServiceResult<ProjectStartFeeStatusDto>.Unauthorized());
        }

        public Task<ServiceResult<PaymentDetailDto>> CreateDepositPaymentForOrderAsync(
            Guid orderId,
            Guid currentUserId,
            CreateOrderDepositPaymentRequestDto request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<PaymentDetailDto>.Unauthorized());

        public Task<ServiceResult<PaymentDetailDto>> CreateRemainingPaymentForOrderAsync(
            Guid orderId,
            Guid currentUserId,
            CreateOrderRemainingPaymentRequestDto request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<PaymentDetailDto>.Unauthorized());

        public Task<ServiceResult<PaymentDetailDto>> GetByIdAsync(
            Guid paymentId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<PaymentDetailDto>.Unauthorized());

        public Task<ServiceResult<PaymentListResponseDto>> GetListAsync(
            Guid currentUserId,
            PaymentQueryDto query,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<PaymentListResponseDto>.Unauthorized());

        public Task<ServiceResult<PaymentTransactionListResponseDto>> GetTransactionsAsync(
            Guid paymentId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<PaymentTransactionListResponseDto>.Unauthorized());

        public Task<ServiceResult<PaymentStatusByCodeDto>> GetStatusByCodeAsync(
            string paymentCode,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<PaymentStatusByCodeDto>.Unauthorized());

        public Task<ServiceResult<SePayVietQrResponseDto>> GenerateSePayVietQrAsync(
            Guid paymentId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<SePayVietQrResponseDto>.Unauthorized());

        public Task<bool> CanAccessPaymentAsync(
            Guid paymentId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<ServiceResult<PayOsPaymentLinkResponseDto>> CreatePayOsPaymentLinkAsync(
            Guid paymentId,
            Guid currentUserId,
            CreatePayOsPaymentLinkRequestDto request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<PayOsPaymentLinkResponseDto>.Unauthorized());

        public Task<ServiceResult<PayOsConfirmWebhookResponseDto>> ConfirmPayOsWebhookAsync(
            PayOsConfirmWebhookRequestDto request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<PayOsConfirmWebhookResponseDto>.Unauthorized());

        public Task<ServiceResult<PaymentDetailDto>> CreateTestPaymentAsync(
            Guid currentUserId,
            CreateTestPaymentRequestDto request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<PaymentDetailDto>.Unauthorized());
    }
}
