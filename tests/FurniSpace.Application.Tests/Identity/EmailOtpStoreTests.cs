#nullable enable

using System.Threading.Tasks;
using FurniSpace.Application.Common.Auth;
using FurniSpace.Application.Services.Identity;
using FurniSpace.Application.Tests.TestDoubles;
using Microsoft.Extensions.Options;
using Xunit;

namespace FurniSpace.Application.Tests.Identity;

public sealed class EmailOtpStoreTests
{
    [Fact]
    public async Task CreateAsync_CreatesHashedSingleUseOtp()
    {
        var cache = new InMemoryCacheService();
        var store = new EmailOtpStore(cache, Options.Create(new JwtSettings
        {
            SecretKey = "this-secret-has-at-least-32-bytes"
        }));

        var otp = await store.CreateAsync("USER@example.com");

        Assert.Matches("^\\d{6}$", otp);
        Assert.DoesNotContain(otp, cache.SerializedValues);
        Assert.False(await store.ConsumeAsync("user@example.com", "000000"));
        Assert.True(await store.ConsumeAsync("user@example.com", otp));
        Assert.False(await store.ConsumeAsync("user@example.com", otp));
    }
}
