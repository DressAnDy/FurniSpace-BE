using FurniSpace.API.Controllers.Payments;
using Microsoft.AspNetCore.Authorization;
using System.Linq;
using Xunit;

namespace FurniSpace.API.Tests.Controllers.Payments;

public sealed class PayOsWebhookControllerTests
{
    [Fact]
    public void Controller_AllowsAnonymousAccess()
    {
        var authorize = typeof(PayOsWebhookController)
            .GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true)
            .Any();

        Assert.True(authorize);
    }
}
