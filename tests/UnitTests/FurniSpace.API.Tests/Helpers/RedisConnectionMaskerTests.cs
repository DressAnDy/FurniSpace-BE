#nullable enable

using System;
using FurniSpace.API.Helpers;
using Xunit;

namespace FurniSpace.API.Tests.Helpers;

public sealed class RedisConnectionMaskerTests
{
    [Fact]
    public void WithEmptyValue_ReturnsInput()
    {
        Assert.Equal(string.Empty, RedisConnectionMasker.Mask(string.Empty));
        Assert.Null(RedisConnectionMasker.Mask(null!));
    }

    [Fact]
    public void WithPasswordSegment_MasksCredential()
    {
        var masked = RedisConnectionMasker.Mask("host=localhost:6379,password=secret123,ssl=true");

        Assert.Contains("password=***", masked, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret123", masked, StringComparison.Ordinal);
        Assert.Contains("host=localhost:6379", masked, StringComparison.Ordinal);
    }

    [Fact]
    public void WithUpperCasePasswordSegment_MasksCredential()
    {
        var masked = RedisConnectionMasker.Mask("PASSWORD=abc123,host=localhost");

        Assert.Contains("PASSWORD=***", masked, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("abc123", masked, StringComparison.Ordinal);
    }
}
