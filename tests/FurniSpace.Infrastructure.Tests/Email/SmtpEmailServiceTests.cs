#nullable enable

using System;
using System.Reflection;
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

    [Fact]
    public void BuildResetInstructions_WithAndWithoutResetUrl_ReturnsExpectedText()
    {
        var withoutUrl = new SmtpEmailService(Options.Create(new SmtpSettings()));
        var withUrl = new SmtpEmailService(Options.Create(new SmtpSettings
        {
            ResetPasswordUrl = "https://example.com/reset"
        }));

        var tokenOnly = InvokeBuildResetInstructions(withoutUrl, "user@example.com", "token-1");
        var resetPage = InvokeBuildResetInstructions(withUrl, "user@example.com", "token-2");

        Assert.Contains("Token: token-1", tokenOnly);
        Assert.Contains("Email: user@example.com", tokenOnly);
        Assert.Contains("Reset page: https://example.com/reset", resetPage);
        Assert.Contains("Token: token-2", resetPage);
    }

    private static string InvokeBuildResetInstructions(
        SmtpEmailService service,
        string email,
        string token)
    {
        return (string)typeof(SmtpEmailService)
            .GetMethod("BuildResetInstructions", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(service, [email, token])!;
    }
}
