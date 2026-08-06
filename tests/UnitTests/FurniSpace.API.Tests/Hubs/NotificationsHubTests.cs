#nullable enable

using System.Linq;
using FurniSpace.API.Hubs;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace FurniSpace.API.Tests.Hubs;

public sealed class NotificationsHubTests
{
    [Fact]
    public void Hub_RequiresAuthentication()
    {
        var authorize = typeof(NotificationsHub)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .SingleOrDefault();

        Assert.NotNull(authorize);
    }
}
