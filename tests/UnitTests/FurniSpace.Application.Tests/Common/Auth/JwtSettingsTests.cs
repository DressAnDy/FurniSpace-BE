#nullable enable

using System;
using FurniSpace.Application.Common.Auth;
using Xunit;

namespace FurniSpace.Application.Tests.Common.Auth;

public sealed class JwtSettingsTests
{
    [Fact]
    public void GetSecretKeyBytes_RejectsWeakSecret()
    {
        var settings = new JwtSettings { SecretKey = "short-secret" };

        Assert.Throws<InvalidOperationException>(() => settings.GetSecretKeyBytes());
    }

    [Fact]
    public void GetSecretKeyBytes_AcceptsStrongUtf8Secret()
    {
        var settings = new JwtSettings { SecretKey = "this-secret-has-at-least-32-bytes" };

        var keyBytes = settings.GetSecretKeyBytes();

        Assert.True(keyBytes.Length >= JwtSettings.MinimumSecretKeyBytes);
    }
}
