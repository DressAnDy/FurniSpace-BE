#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.API.Controllers.Payments;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Orders;
using FurniSpace.Application.DTOs.Payments;
using FurniSpace.Application.Interfaces.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FurniSpace.API.Tests.Controllers.Payments;

public sealed class PayOsAdminPaymentsControllerTests
{
    [Fact]
    public void ConfirmWebhook_RequiresAdminRole()
    {
        var authorize = typeof(PayOsAdminPaymentsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .SingleOrDefault();

        Assert.NotNull(authorize);
        Assert.Equal("ADMIN", authorize.Roles);
    }

    [Fact]
    public async Task ConfirmWebhook_ReturnsServiceResult()
    {
        var service = new FakePaymentService(
            ServiceResult<PayOsConfirmWebhookResponseDto>.Success(
                new PayOsConfirmWebhookResponseDto { Success = true, WebhookUrl = "https://example.com/hook" },
                "ok"));
        var controller = new PayOsAdminPaymentsController(service);

        var result = await controller.ConfirmWebhook(new PayOsConfirmWebhookRequestDto());

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(200, objectResult.StatusCode);
    }

    private sealed class FakePaymentService(ServiceResult<PayOsConfirmWebhookResponseDto> result) : IPaymentService
    {
        public Task<ServiceResult<PayOsConfirmWebhookResponseDto>> ConfirmPayOsWebhookAsync(
            PayOsConfirmWebhookRequestDto request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(result);

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

        public Task<ServiceResult<PaymentDetailDto>> CreateProjectStartFeePaymentAsync(
            Guid projectId,
            Guid currentUserId,
            CreateProjectStartFeePaymentRequestDto request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<PaymentDetailDto>.Unauthorized());

        public Task<ServiceResult<ProjectStartFeeStatusDto>> GetProjectStartFeeStatusAsync(
            Guid projectId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<ProjectStartFeeStatusDto>.Unauthorized());

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

        public Task<ServiceResult<PaymentTransactionAttemptResponseDto>> CreatePaymentTransactionAttemptAsync(
            Guid paymentId,
            Guid currentUserId,
            CreatePaymentTransactionAttemptRequestDto request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<PaymentTransactionAttemptResponseDto>.Unauthorized());

        public Task<ServiceResult<PaymentDetailDto>> CreateTestPaymentAsync(
            Guid currentUserId,
            CreateTestPaymentRequestDto request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<PaymentDetailDto>.Unauthorized());
    }
}
