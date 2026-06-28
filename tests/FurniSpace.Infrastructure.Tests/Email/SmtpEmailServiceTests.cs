#nullable enable

using System;
using System.Threading.Tasks;
using FurniSpace.Infrastructure.Common.Email;
using Microsoft.Extensions.Options;
using Xunit;

namespace FurniSpace.Infrastructure.Tests.Email;

public sealed class SmtpEmailServiceTests
{
    [Fact]
    public async Task SendPasswordResetAsync_WhenSmtpNotConfigured_Throws()
    {
        var service = new SmtpEmailService(Options.Create(new SmtpSettings()));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SendPasswordResetAsync("user@example.com", "User", "reset-token"));

        Assert.Contains("SMTP is not configured", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendEmailVerificationOtpAsync_WhenSmtpNotConfigured_Throws()
    {
        var service = new SmtpEmailService(Options.Create(new SmtpSettings { FromEmail = "noreply@example.com" }));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SendEmailVerificationOtpAsync("user@example.com", "User", "123456"));

        Assert.Contains("SMTP is not configured", exception.Message, StringComparison.Ordinal);
    }
}
