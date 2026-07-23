#nullable enable

using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.API.Controllers.Payments;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Orders;
using FurniSpace.Application.DTOs.Payments;
using FurniSpace.Application.Interfaces.Payments;
using FurniSpace.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FurniSpace.API.Tests.Controllers.Payments;

public sealed class PaymentsControllerTests
{
    [Fact]
    public void GetById_RequiresPaymentRoles()
    {
        var authorize = GetMethodAuthorizeAttribute(nameof(PaymentsController.GetById));

        Assert.NotNull(authorize);
        Assert.Equal("CUSTOMER,SALES,DESIGNER,ADMIN", authorize.Roles);
    }

    [Fact]
    public async Task GetById_WithoutUser_ReturnsUnauthorized()
    {
        var controller = CreateController(new FakePaymentService(), userId: null);

        var result = await controller.GetById(Guid.NewGuid());

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task GetById_ReturnsServiceResult()
    {
        var paymentId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var service = new FakePaymentService(
            getByIdResult: ServiceResult<PaymentDetailDto>.Success(
                new PaymentDetailDto { PaymentId = paymentId },
                "ok"));
        var controller = CreateController(service, userId);

        var result = await controller.GetById(paymentId);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(200, objectResult.StatusCode);
    }

    [Fact]
    public async Task GetList_ReturnsServiceResult()
    {
        var userId = Guid.NewGuid();
        var service = new FakePaymentService(
            getListResult: ServiceResult<PaymentListResponseDto>.Success(
                new PaymentListResponseDto { Items = [] },
                "ok"));
        var controller = CreateController(service, userId);

        var result = await controller.GetList(projectId: Guid.NewGuid(), status: PaymentStatus.PENDING);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(200, objectResult.StatusCode);
    }

    [Fact]
    public async Task GetTransactions_ReturnsServiceResult()
    {
        var paymentId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var service = new FakePaymentService(
            getTransactionsResult: ServiceResult<PaymentTransactionListResponseDto>.Success(
                new PaymentTransactionListResponseDto { Items = [] },
                "ok"));
        var controller = CreateController(service, userId);

        var result = await controller.GetTransactions(paymentId);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(200, objectResult.StatusCode);
    }

    [Fact]
    public async Task GetStatusByCode_ReturnsServiceResult()
    {
        var userId = Guid.NewGuid();
        var service = new FakePaymentService(
            getStatusByCodeResult: ServiceResult<PaymentStatusByCodeDto>.Success(
                new PaymentStatusByCodeDto { PaymentCode = "FS12345678" },
                "ok"));
        var controller = CreateController(service, userId);

        var result = await controller.GetStatusByCode("FS12345678");

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(200, objectResult.StatusCode);
    }

    [Fact]
    public async Task GenerateSePayVietQr_ReturnsServiceResult()
    {
        var paymentId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var service = new FakePaymentService(
            generateVietQrResult: ServiceResult<SePayVietQrResponseDto>.Success(
                new SePayVietQrResponseDto { PaymentId = paymentId, VietQrUrl = "https://vietqr.test" },
                "ok"));
        var controller = CreateController(service, userId);

        var result = await controller.GenerateSePayVietQr(paymentId);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(200, objectResult.StatusCode);
    }

    [Fact]
    public async Task CreatePayOsPaymentLink_ReturnsServiceResult()
    {
        var paymentId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var service = new FakePaymentService(
            createPayOsLinkResult: ServiceResult<PayOsPaymentLinkResponseDto>.Success(
                new PayOsPaymentLinkResponseDto { PaymentId = paymentId, CheckoutUrl = "https://payos.test" },
                "ok"));
        var controller = CreateController(service, userId);

        var result = await controller.CreatePayOsPaymentLink(
            paymentId,
            new CreatePayOsPaymentLinkRequestDto());

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(200, objectResult.StatusCode);
    }

    [Fact]
    public async Task GetList_WithoutUser_ReturnsUnauthorized()
    {
        var controller = CreateController(new FakePaymentService(), userId: null);

        var result = await controller.GetList();

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task GetTransactions_WithoutUser_ReturnsUnauthorized()
    {
        var controller = CreateController(new FakePaymentService(), userId: null);

        var result = await controller.GetTransactions(Guid.NewGuid());

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task GetStatusByCode_WithoutUser_ReturnsUnauthorized()
    {
        var controller = CreateController(new FakePaymentService(), userId: null);

        var result = await controller.GetStatusByCode("FS12345678");

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task GenerateSePayVietQr_WithoutUser_ReturnsUnauthorized()
    {
        var controller = CreateController(new FakePaymentService(), userId: null);

        var result = await controller.GenerateSePayVietQr(Guid.NewGuid());

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task CreatePayOsPaymentLink_WithoutUser_ReturnsUnauthorized()
    {
        var controller = CreateController(new FakePaymentService(), userId: null);

        var result = await controller.CreatePayOsPaymentLink(
            Guid.NewGuid(),
            new CreatePayOsPaymentLinkRequestDto());

        Assert.IsType<UnauthorizedResult>(result);
    }

    private static AuthorizeAttribute? GetMethodAuthorizeAttribute(string methodName)
    {
        var method = typeof(PaymentsController)
            .GetMethods()
            .Single(methodInfo => methodInfo.Name == methodName);

        return method.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .SingleOrDefault();
    }

    private static PaymentsController CreateController(FakePaymentService service, Guid? userId)
    {
        var controller = new PaymentsController(service);
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
        private readonly ServiceResult<PaymentDetailDto>? _getByIdResult;
        private readonly ServiceResult<PaymentListResponseDto>? _getListResult;
        private readonly ServiceResult<PaymentTransactionListResponseDto>? _getTransactionsResult;
        private readonly ServiceResult<PaymentStatusByCodeDto>? _getStatusByCodeResult;
        private readonly ServiceResult<SePayVietQrResponseDto>? _generateVietQrResult;
        private readonly ServiceResult<PayOsPaymentLinkResponseDto>? _createPayOsLinkResult;

        public FakePaymentService(
            ServiceResult<PaymentDetailDto>? getByIdResult = null,
            ServiceResult<PaymentListResponseDto>? getListResult = null,
            ServiceResult<PaymentTransactionListResponseDto>? getTransactionsResult = null,
            ServiceResult<PaymentStatusByCodeDto>? getStatusByCodeResult = null,
            ServiceResult<SePayVietQrResponseDto>? generateVietQrResult = null,
            ServiceResult<PayOsPaymentLinkResponseDto>? createPayOsLinkResult = null)
        {
            _getByIdResult = getByIdResult;
            _getListResult = getListResult;
            _getTransactionsResult = getTransactionsResult;
            _getStatusByCodeResult = getStatusByCodeResult;
            _generateVietQrResult = generateVietQrResult;
            _createPayOsLinkResult = createPayOsLinkResult;
        }

        public Task<ServiceResult<PaymentDetailDto>> GetByIdAsync(
            Guid paymentId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(_getByIdResult ?? ServiceResult<PaymentDetailDto>.Unauthorized());

        public Task<ServiceResult<PaymentListResponseDto>> GetListAsync(
            Guid currentUserId,
            PaymentQueryDto query,
            CancellationToken cancellationToken = default)
            => Task.FromResult(_getListResult ?? ServiceResult<PaymentListResponseDto>.Unauthorized());

        public Task<ServiceResult<PaymentTransactionListResponseDto>> GetTransactionsAsync(
            Guid paymentId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(_getTransactionsResult ?? ServiceResult<PaymentTransactionListResponseDto>.Unauthorized());

        public Task<ServiceResult<PaymentStatusByCodeDto>> GetStatusByCodeAsync(
            string paymentCode,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(_getStatusByCodeResult ?? ServiceResult<PaymentStatusByCodeDto>.Unauthorized());

        public Task<ServiceResult<SePayVietQrResponseDto>> GenerateSePayVietQrAsync(
            Guid paymentId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(_generateVietQrResult ?? ServiceResult<SePayVietQrResponseDto>.Unauthorized());

        public Task<ServiceResult<PayOsPaymentLinkResponseDto>> CreatePayOsPaymentLinkAsync(
            Guid paymentId,
            Guid currentUserId,
            CreatePayOsPaymentLinkRequestDto request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(_createPayOsLinkResult ?? ServiceResult<PayOsPaymentLinkResponseDto>.Unauthorized());

        public Task<ServiceResult<PaymentTransactionAttemptResponseDto>> CreatePaymentTransactionAttemptAsync(
            Guid paymentId,
            Guid currentUserId,
            CreatePaymentTransactionAttemptRequestDto request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<PaymentTransactionAttemptResponseDto>.Unauthorized());

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

        public Task<bool> CanAccessPaymentAsync(
            Guid paymentId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(false);

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
