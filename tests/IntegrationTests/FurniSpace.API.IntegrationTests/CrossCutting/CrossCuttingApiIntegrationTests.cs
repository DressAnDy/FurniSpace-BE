using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using FurniSpace.API.IntegrationTests.Fixtures;
using FurniSpace.API.IntegrationTests.Support;
using FurniSpace.Application.Common.Notifications;
using FurniSpace.Application.DTOs.CustomizationRequests;
using FurniSpace.Application.DTOs.Orders;
using FurniSpace.Application.DTOs.Payments;
using FurniSpace.Application.DTOs.Production;
using FurniSpace.Application.DTOs.ProjectFiles;
using FurniSpace.Application.Interfaces.Payments;
using FurniSpace.Domain.Enums;
using FurniSpace.Testing.Fakes;
using FurniSpace.Testing.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FurniSpace.API.IntegrationTests.CrossCutting;

[Collection(ApiIntegrationCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "Core")]
public sealed class CrossCuttingApiIntegrationTests : IAsyncLifetime
{
    private const string ReturnUrl = "https://frontend.integration.test/payments/cross-return";
    private const string CancelUrl = "https://frontend.integration.test/payments/cross-cancel";
    private const string WebhookPayload = "{}";
    private const string WebhookReference = "PAYOS-CROSS-CUTTING";

    private readonly ApiIntegrationFixture _fixture;

    public CrossCuttingApiIntegrationTests(ApiIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await _fixture.Database.ResetAsync();
        Notifications.Clear();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task AuthorizationBoundaries_BlockWrongRoleAndWrongProjectOwner()
    {
        var production = await SeedProductionScenarioAsync();
        var (delivery, otherCustomerId) = await SeedDeliveryForWrongOwnerAsync();

        using var wrongRoleRequest = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Post,
            $"/orders/{production.OrderId}/production-request",
            production.CustomerAccountId,
            CoreRoles.Customer,
            new CreateProductionRequestDto { AssignedTo = production.ProductionAccountId });
        var wrongRoleResponse = await _fixture.Client.SendAsync(wrongRoleRequest);

        using var wrongOwnerRequest = IntegrationHttp.Authenticated(
            HttpMethod.Patch,
            $"/orders/{delivery.OrderId}/confirm-delivery",
            otherCustomerId,
            CoreRoles.Customer);
        var wrongOwnerResponse = await _fixture.Client.SendAsync(wrongOwnerRequest);

        Assert.Equal(HttpStatusCode.Forbidden, wrongRoleResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, wrongOwnerResponse.StatusCode);
    }

    [Fact]
    public async Task ProjectFileUpload_UsesFakeStorageAndPersistsMetadata()
    {
        var scenario = await SeedProjectScenarioAsync();
        using var request = BuildProjectFileUploadRequest(scenario);

        var response = await _fixture.Client.SendAsync(request);
        var uploaded = await IntegrationHttp.ReadDataAsync<ProjectFileUploadResponseDto>(
            response,
            HttpStatusCode.Created);

        Assert.Equal(scenario.ProjectId, uploaded.ProjectId);
        Assert.Equal(scenario.CustomerAccountId, uploaded.UploadedBy);
        Assert.Equal("floor-plan.png", uploaded.OriginalFileName);
        Assert.Equal(FileType.FLOOR_PLAN, uploaded.FileType);
        Assert.Equal(FileVisibility.CUSTOMER_VISIBLE, uploaded.Visibility);
        Assert.StartsWith("integration/", uploaded.StoragePath, StringComparison.Ordinal);
        Assert.StartsWith("https://storage.integration.test/integration/", uploaded.PublicUrl, StringComparison.Ordinal);

        using var listRequest = IntegrationHttp.Authenticated(
            HttpMethod.Get,
            $"/projects/{scenario.ProjectId}/files?fileType=FLOOR_PLAN&visibility=CUSTOMER_VISIBLE",
            scenario.SalesAccountId,
            CoreRoles.Sales);
        var listResponse = await _fixture.Client.SendAsync(listRequest);
        var files = await IntegrationHttp.ReadDataAsync<ProjectFilesResponseDto>(
            listResponse,
            HttpStatusCode.OK);

        var file = Assert.Single(files.Items);
        Assert.Equal(uploaded.FileId, file.FileId);
        Assert.Equal(uploaded.FileLinkId, file.FileLinkId);
        Assert.Equal(uploaded.PublicUrl, file.PublicUrl);
    }

    [Fact]
    public async Task RemainingPaymentRetry_ReusesActivePaymentAndDispatchesPaymentCreatedOnce()
    {
        var scenario = await SeedFinalPaymentScenarioAsync();
        await PrepareFinalPaymentAsync(scenario);

        var firstPayment = await CreateRemainingPaymentAsync(scenario, HttpStatusCode.Created);
        var reusedPayment = await CreateRemainingPaymentAsync(scenario, HttpStatusCode.OK);

        Assert.Equal(firstPayment.PaymentId, reusedPayment.PaymentId);
        Assert.True(reusedPayment.Reused);

        await using var context = _fixture.Database.CreateDbContext();
        Assert.Equal(1, await context.PaymentSet.CountAsync(payment =>
            payment.OrderId == scenario.OrderId
            && payment.PaymentType == PaymentType.REMAINING_PAYMENT));

        var notification = Assert.Single(Notifications.Notifications.Where(item =>
            item.Type == NotificationType.PaymentCreated));
        Assert.Contains(scenario.CustomerAccountId, notification.ReceiverIds);
    }

    [Fact]
    public async Task ConcurrentPayOsWebhook_ForSameRemainingPayment_DoesNotDoubleApply()
    {
        var scenario = await SeedFinalPaymentScenarioAsync();
        await PrepareFinalPaymentAsync(scenario);
        var payment = await CreateRemainingPaymentAsync(scenario, HttpStatusCode.Created);
        await CreatePayOsAttemptAsync(scenario.CustomerAccountId, payment.PaymentId);
        await ConfigureSuccessfulPayOsWebhookAsync(payment.PaymentId, payment.Amount);

        var responses = await Task.WhenAll(PostPayOsWebhookAsync(), PostPayOsWebhookAsync());

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        foreach (var response in responses)
        {
            response.Dispose();
        }

        await using var context = _fixture.Database.CreateDbContext();
        var order = await context.OrderSet.SingleAsync(item => item.OrderId == scenario.OrderId);
        var paidPayment = await context.PaymentSet.SingleAsync(item => item.PaymentId == payment.PaymentId);
        Assert.Equal(PaymentStatus.PAID, paidPayment.Status);
        Assert.Equal(scenario.FinalTotalAmount, order.PaidAmount);
        Assert.Equal(0m, order.RemainingAmount);
        Assert.Equal(1, await context.PaymentTransactionSet.CountAsync(transaction =>
            transaction.PaymentId == payment.PaymentId
            && transaction.Status == PaymentTransactionStatus.SUCCESS));
    }

    [Fact]
    public async Task ConcurrentCustomizationAccept_ForSameVersion_EndsAcceptedOnce()
    {
        CustomizationRequestScenario scenario;
        Guid versionId;
        await using (var context = _fixture.Database.CreateDbContext())
        {
            (scenario, versionId) = await CustomizationScenarioSeeder.SeedFeasibleVersionAsync(context);
        }

        var responses = await Task.WhenAll(
            AcceptCustomizationVersionAsync(scenario, versionId),
            AcceptCustomizationVersionAsync(scenario, versionId));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        foreach (var response in responses)
        {
            response.Dispose();
        }

        await using var verification = _fixture.Database.CreateDbContext();
        var request = await verification.CustomizationRequestSet.SingleAsync();
        var version = await verification.CustomizationRequestVersionSet.SingleAsync();
        Assert.Equal(CustomizationStatus.ACCEPTED, request.Status);
        Assert.Equal(versionId, request.AcceptedRequestVersionId);
        Assert.Equal(CustomizationVersionStatus.ACCEPTED, version.Status);
    }

    private CapturingNotificationDispatcher Notifications =>
        _fixture.Factory.Services.GetRequiredService<CapturingNotificationDispatcher>();

    private async Task<ProductionOrderScenario> SeedProductionScenarioAsync()
    {
        await using var context = _fixture.Database.CreateDbContext();
        return await ProductionScenarioSeeder.SeedDepositPaidOrderAsync(context);
    }

    private async Task<ProjectConsultationScenario> SeedProjectScenarioAsync()
    {
        await using var context = _fixture.Database.CreateDbContext();
        return await ProjectScenarioSeeder.SeedInConsultationAsync(context);
    }

    private async Task<FinalPaymentOrderScenario> SeedFinalPaymentScenarioAsync()
    {
        await using var context = _fixture.Database.CreateDbContext();
        return await FinalPaymentScenarioSeeder.SeedDeliveredOrderWithRemainingAsync(context);
    }

    private async Task<(DeliveryOrderScenario Delivery, Guid OtherCustomerId)> SeedDeliveryForWrongOwnerAsync()
    {
        await using var context = _fixture.Database.CreateDbContext();
        var delivery = await DeliveryScenarioSeeder.SeedReadyForDeliveryOrderAsync(context);
        var otherCustomer = await CoreAccountSeeder.SeedAccountAsync(
            context,
            CoreRoles.Customer,
            $"cross-owner-{Guid.NewGuid():N}@integration.test");

        var order = await context.OrderSet.SingleAsync(item => item.OrderId == delivery.OrderId);
        var project = await context.ProjectSet.SingleAsync(item => item.ProjectId == delivery.ProjectId);
        var orderItem = await context.OrderItemSet.SingleAsync(item => item.OrderItemId == delivery.FirstOrderItemId);
        order.Status = OrderStatus.DELIVERING;
        project.Status = ProjectStatus.DELIVERING;
        orderItem.Status = OrderItemStatus.DELIVERED;
        orderItem.DeliveredAt = CoreAccountSeeder.FixedTimestamp;
        orderItem.DeliveredBy = delivery.SalesAccountId;
        await context.SaveChangesAsync();

        return (delivery, otherCustomer.AccountId);
    }

    private static HttpRequestMessage BuildProjectFileUploadRequest(ProjectConsultationScenario scenario)
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes("fake floor plan"));
        file.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
        content.Add(file, "File", "floor-plan.png");
        content.Add(new StringContent(nameof(FileType.FLOOR_PLAN)), "FileType");
        content.Add(new StringContent(nameof(FileVisibility.CUSTOMER_VISIBLE)), "Visibility");
        content.Add(new StringContent("Initial measured floor plan"), "Note");

        return IntegrationHttp.Authenticated(
            HttpMethod.Post,
            $"/projects/{scenario.ProjectId}/files",
            scenario.CustomerAccountId,
            CoreRoles.Customer,
            content);
    }

    private async Task<OrderFinalPaymentPreparationDto> PrepareFinalPaymentAsync(
        FinalPaymentOrderScenario scenario)
    {
        using var request = IntegrationHttp.Authenticated(
            HttpMethod.Patch,
            $"/orders/{scenario.OrderId}/prepare-final-payment",
            scenario.SalesAccountId,
            CoreRoles.Sales);
        var response = await _fixture.Client.SendAsync(request);
        return await IntegrationHttp.ReadDataAsync<OrderFinalPaymentPreparationDto>(response, HttpStatusCode.OK);
    }

    private async Task<PaymentDetailDto> CreateRemainingPaymentAsync(
        FinalPaymentOrderScenario scenario,
        HttpStatusCode expectedStatus)
    {
        using var request = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Post,
            $"/orders/{scenario.OrderId}/payments/remaining",
            scenario.SalesAccountId,
            CoreRoles.Sales,
            new CreateOrderRemainingPaymentRequestDto { Note = "Cross-cutting remaining payment" });
        var response = await _fixture.Client.SendAsync(request);
        return await IntegrationHttp.ReadDataAsync<PaymentDetailDto>(response, expectedStatus);
    }

    private async Task CreatePayOsAttemptAsync(Guid customerId, Guid paymentId)
    {
        using var request = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Post,
            $"/api/payments/{paymentId}/transactions",
            customerId,
            CoreRoles.Customer,
            new CreatePaymentTransactionAttemptRequestDto
            {
                PaymentProvider = PaymentProvider.PAYOS,
                PaymentMethod = PaymentMethod.PAYMENT_LINK,
                ReturnUrl = ReturnUrl,
                CancelUrl = CancelUrl
            });
        var response = await _fixture.Client.SendAsync(request);
        _ = await IntegrationHttp.ReadDataAsync<PaymentTransactionAttemptResponseDto>(
            response,
            HttpStatusCode.OK);
    }

    private async Task ConfigureSuccessfulPayOsWebhookAsync(Guid paymentId, decimal amount)
    {
        await using var context = _fixture.Database.CreateDbContext();
        var transaction = await context.PaymentTransactionSet.SingleAsync(item => item.PaymentId == paymentId);
        var fakePayOs = _fixture.Factory.Services.GetRequiredService<IPayOsClient>() as FakePayOsClient
            ?? throw new InvalidOperationException("Integration tests require FakePayOsClient.");
        fakePayOs.VerifiedWebhook = new PayOsVerifiedWebhookData
        {
            OrderCode = long.Parse(transaction.ProviderReferenceCode!, CultureInfo.InvariantCulture),
            Amount = (long)amount,
            Reference = WebhookReference,
            PaymentLinkId = transaction.ProviderTransactionId,
            TransactionDateTime = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            Code = "00"
        };
    }

    private Task<HttpResponseMessage> PostPayOsWebhookAsync()
    {
        return _fixture.Client.PostAsync(
            "/api/webhooks/payos",
            JsonContent.Create(new { payload = WebhookPayload }));
    }

    private async Task<HttpResponseMessage> AcceptCustomizationVersionAsync(
        CustomizationRequestScenario scenario,
        Guid versionId)
    {
        using var request = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Post,
            $"/customization-requests/{scenario.CustomizationRequestId}/accept",
            scenario.Base.CustomerAccountId,
            CoreRoles.Customer,
            new AcceptCustomizationRequestDto { CustomizationRequestVersionId = versionId });

        return await _fixture.Client.SendAsync(request);
    }
}
