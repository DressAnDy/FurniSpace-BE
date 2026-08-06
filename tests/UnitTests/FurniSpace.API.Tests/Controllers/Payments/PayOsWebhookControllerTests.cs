#nullable enable

using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.API.Controllers.Payments;
using FurniSpace.Application.DTOs.Payments;
using FurniSpace.Application.Interfaces.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FurniSpace.API.Tests.Controllers.Payments;

public sealed class PayOsWebhookControllerTests
{
    [Fact]
    public void Controller_AllowsAnonymousAccess()
    {
        var authorize = typeof(PayOsWebhookController)
            .GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true);

        Assert.True(authorize.Length > 0);
    }

    [Fact]
    public async Task Receive_WithSuccessBody_ReturnsStatusCodeAndBody()
    {
        var controller = CreateController(new FakePayOsWebhookService(
            new PayOsWebhookProcessResult(200, new PayOsWebhookSuccessDto(), null)));

        var result = await controller.Receive();

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.IsType<PayOsWebhookSuccessDto>(objectResult.Value);
    }

    [Fact]
    public async Task Receive_WithErrorMessage_ReturnsStatusCodeAndMessagePayload()
    {
        var controller = CreateController(new FakePayOsWebhookService(
            new PayOsWebhookProcessResult(401, null, "PayOS webhook signature is invalid.")));

        var result = await controller.Receive();

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(401, objectResult.StatusCode);
    }

    private static PayOsWebhookController CreateController(IPayOsWebhookService service)
    {
        var controller = new PayOsWebhookController(service);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                Request =
                {
                    Body = new MemoryStream(Encoding.UTF8.GetBytes("{}"))
                }
            }
        };
        return controller;
    }

    private sealed class FakePayOsWebhookService(PayOsWebhookProcessResult result) : IPayOsWebhookService
    {
        public Task<PayOsWebhookProcessResult> ProcessAsync(
            string rawBody,
            CancellationToken cancellationToken = default)
            => Task.FromResult(result);
    }
}
