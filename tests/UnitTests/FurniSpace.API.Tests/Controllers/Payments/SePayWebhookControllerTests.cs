#nullable enable

using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.API.Controllers.Payments;
using FurniSpace.Application.Common.Payments;
using FurniSpace.Application.DTOs.Payments;
using FurniSpace.Application.Interfaces.Payments;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Xunit;

namespace FurniSpace.API.Tests.Controllers.Payments;

public sealed class SePayWebhookControllerTests
{
    [Fact]
    public async Task Receive_WithSuccessBody_ReturnsStatusCodeAndBody()
    {
        var controller = CreateController(new FakeSePayWebhookService(
            new SePayWebhookProcessResult(200, new SePayWebhookSuccessDto(), null)));

        var result = await controller.Receive();

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.IsType<SePayWebhookSuccessDto>(objectResult.Value);
    }

    [Fact]
    public async Task Receive_WithErrorMessage_ReturnsStatusCodeAndMessagePayload()
    {
        var controller = CreateController(new FakeSePayWebhookService(
            new SePayWebhookProcessResult(401, null, "Webhook signature is invalid.")));

        var result = await controller.Receive();

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(401, objectResult.StatusCode);
    }

    private static SePayWebhookController CreateController(ISePayWebhookService service)
    {
        var controller = new SePayWebhookController(
            service,
            Options.Create(new SePayOptions
            {
                WebhookSignatureHeader = "X-Signature",
                WebhookTimestampHeader = "X-Timestamp"
            }));
        var body = "{\"id\":1}";
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                Request =
                {
                    Body = new MemoryStream(Encoding.UTF8.GetBytes(body)),
                    Headers =
                    {
                        ["X-Signature"] = "sha256=abc",
                        ["X-Timestamp"] = "1234567890"
                    }
                }
            }
        };
        return controller;
    }

    private sealed class FakeSePayWebhookService(SePayWebhookProcessResult result) : ISePayWebhookService
    {
        public Task<SePayWebhookProcessResult> ProcessAsync(
            string rawBody,
            string? signature,
            string? timestampHeader,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(result);
        }
    }
}
