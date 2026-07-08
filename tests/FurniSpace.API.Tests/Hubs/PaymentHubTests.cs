using System.Linq;
using FurniSpace.API.Hubs;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace FurniSpace.API.Tests.Hubs;

public sealed class PaymentHubTests
{
    [Fact]
    public void Hub_RequiresAuthenticationAndAllowedRoles()
    {
        var authorize = typeof(PaymentHub)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        Assert.Equal("CUSTOMER,SALES,DESIGNER,ADMIN", authorize.Roles);
    }
}
