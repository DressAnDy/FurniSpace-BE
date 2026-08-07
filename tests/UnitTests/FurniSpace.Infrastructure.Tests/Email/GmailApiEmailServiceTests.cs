#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Infrastructure.Common.Email;
using FurniSpace.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace FurniSpace.Infrastructure.Tests.Email;

public sealed class GmailApiEmailServiceTests
{
    [Fact]
    public async Task SendEmailVerificationOtpAsync_WhenConfigured_SendsMimeMessage()
    {
        var handler = new RecordingHandler((_, _) => new HttpResponseMessage(HttpStatusCode.OK));
        var tokenProvider = new FakeTokenProvider();
        var service = CreateService(handler, tokenProvider);

        await service.SendEmailVerificationOtpAsync(
            "user@example.com",
            "Nguyễn User",
            "123456");

        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal(
            "https://gmail.googleapis.com/gmail/v1/users/me/messages/send",
            handler.RequestUri?.ToString());
        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Equal("access-token", handler.AuthorizationParameter);

        var mimeMessage = DecodeRawMessage(handler.Body!);
        Assert.Contains("From: =?UTF-8?B?", mimeMessage, StringComparison.Ordinal);
        Assert.Contains("<sender@gmail.com>", mimeMessage, StringComparison.Ordinal);
        Assert.Contains("<user@example.com>", mimeMessage, StringComparison.Ordinal);
        Assert.Contains("Content-Type: text/plain; charset=UTF-8", mimeMessage, StringComparison.Ordinal);
        Assert.Contains("Content-Type: text/html; charset=UTF-8", mimeMessage, StringComparison.Ordinal);
        Assert.Contains("123456", DecodeFirstMimePart(mimeMessage), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendPasswordResetAsync_WhenConfigured_IncludesResetUrl()
    {
        var handler = new RecordingHandler((_, _) => new HttpResponseMessage(HttpStatusCode.OK));
        var service = CreateService(handler, new FakeTokenProvider());

        await service.SendPasswordResetAsync("user@example.com", "User", "reset-token");

        var mimeMessage = DecodeRawMessage(handler.Body!);
        var textContent = DecodeFirstMimePart(mimeMessage);
        Assert.Contains(
            "Reset page: https://app.example.com/reset-password",
            textContent,
            StringComparison.Ordinal);
        Assert.Contains("Token: reset-token", textContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendEmailVerificationOtpAsync_WhenGmailRejectsRequest_ThrowsDeliveryException()
    {
        var handler = new RecordingHandler((_, _) => new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent("""{"error":{"message":"Insufficient Permission"}}""")
        });
        var service = CreateService(handler, new FakeTokenProvider());

        var exception = await Assert.ThrowsAsync<EmailDeliveryException>(() =>
            service.SendEmailVerificationOtpAsync("user@example.com", "User", "123456"));

        Assert.Equal(HttpStatusCode.Forbidden, exception.StatusCode);
        Assert.Contains("Insufficient Permission", exception.ProviderMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendEmailVerificationOtpAsync_WhenAccessTokenRejected_RefreshesAndRetries()
    {
        var handler = new RecordingHandler((_, callCount) =>
            new HttpResponseMessage(callCount == 1 ? HttpStatusCode.Unauthorized : HttpStatusCode.OK));
        var tokenProvider = new FakeTokenProvider();
        var service = CreateService(handler, tokenProvider);

        await service.SendEmailVerificationOtpAsync("user@example.com", "User", "123456");

        Assert.Equal(2, handler.CallCount);
        Assert.Equal(2, tokenProvider.GetTokenCallCount);
        Assert.Equal(1, tokenProvider.InvalidateCallCount);
    }

    [Fact]
    public async Task SendEmailVerificationOtpAsync_WhenSenderMissing_ThrowsDeliveryException()
    {
        var settings = CreateSettings();
        settings.SenderEmail = string.Empty;
        var service = CreateService(
            new RecordingHandler((_, _) => new HttpResponseMessage(HttpStatusCode.OK)),
            new FakeTokenProvider(),
            settings);

        var exception = await Assert.ThrowsAsync<EmailDeliveryException>(() =>
            service.SendEmailVerificationOtpAsync("user@example.com", "User", "123456"));

        Assert.Contains("Gmail API is not configured", exception.Message, StringComparison.Ordinal);
    }

    private static GmailApiEmailService CreateService(
        HttpMessageHandler handler,
        IGmailAccessTokenProvider tokenProvider,
        GmailApiSettings? settings = null)
    {
        settings ??= CreateSettings();
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri(settings.BaseUrl)
        };

        return new GmailApiEmailService(
            client,
            tokenProvider,
            Options.Create(settings),
            NullLogger<GmailApiEmailService>.Instance);
    }

    private static GmailApiSettings CreateSettings() => new()
    {
        ClientId = "client-id",
        ClientSecret = "client-secret",
        RefreshToken = "refresh-token",
        SenderEmail = "sender@gmail.com",
        SenderName = "FurniSpace Test",
        ResetPasswordUrl = "https://app.example.com/reset-password"
    };

    private static string DecodeRawMessage(string requestBody)
    {
        using var payload = JsonDocument.Parse(requestBody);
        var raw = payload.RootElement.GetProperty("raw").GetString()!;
        var padded = raw.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
        return Encoding.UTF8.GetString(Convert.FromBase64String(padded));
    }

    private static string DecodeFirstMimePart(string mimeMessage)
    {
        const string transferHeader = "Content-Transfer-Encoding: base64\r\n\r\n";
        var contentStart = mimeMessage.IndexOf(transferHeader, StringComparison.Ordinal) + transferHeader.Length;
        var contentEnd = mimeMessage.IndexOf("\r\n--furnispace_", contentStart, StringComparison.Ordinal);
        var encoded = mimeMessage[contentStart..contentEnd].Replace("\r\n", string.Empty);
        return Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
    }

    private sealed class FakeTokenProvider : IGmailAccessTokenProvider
    {
        public int GetTokenCallCount { get; private set; }
        public int InvalidateCallCount { get; private set; }

        public Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
        {
            GetTokenCallCount++;
            return Task.FromResult("access-token");
        }

        public void Invalidate()
        {
            InvalidateCallCount++;
        }
    }

    private sealed class RecordingHandler(
        Func<HttpRequestMessage, int, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public int CallCount { get; private set; }
        public HttpMethod? Method { get; private set; }
        public Uri? RequestUri { get; private set; }
        public string? AuthorizationScheme { get; private set; }
        public string? AuthorizationParameter { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            Method = request.Method;
            RequestUri = request.RequestUri;
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            AuthorizationParameter = request.Headers.Authorization?.Parameter;
            Body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return responseFactory(request, CallCount);
        }
    }
}

public sealed class GmailAccessTokenProviderTests
{
    [Fact]
    public async Task GetAccessTokenAsync_WhenRefreshSucceeds_CachesToken()
    {
        var handler = new TokenHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"access_token":"new-access-token","expires_in":3600,"token_type":"Bearer"}""")
        });
        using var provider = CreateProvider(handler);

        var first = await provider.GetAccessTokenAsync();
        var second = await provider.GetAccessTokenAsync();

        Assert.Equal("new-access-token", first);
        Assert.Equal(first, second);
        Assert.Equal(1, handler.CallCount);
        Assert.Contains("grant_type=refresh_token", handler.Body, StringComparison.Ordinal);
        Assert.Contains("refresh_token=refresh-token", handler.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetAccessTokenAsync_WhenGoogleRejectsRefresh_ThrowsDeliveryException()
    {
        var handler = new TokenHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("""{"error":"invalid_grant"}""")
        });
        using var provider = CreateProvider(handler);

        var exception = await Assert.ThrowsAsync<EmailDeliveryException>(() =>
            provider.GetAccessTokenAsync());

        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Contains("invalid_grant", exception.ProviderMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetAccessTokenAsync_WhenConfigurationMissing_ThrowsDeliveryException()
    {
        var provider = CreateProvider(
            new TokenHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)),
            new GmailApiSettings());

        var exception = await Assert.ThrowsAsync<EmailDeliveryException>(() =>
            provider.GetAccessTokenAsync());

        Assert.Contains("Gmail API OAuth is not configured", exception.Message, StringComparison.Ordinal);
        provider.Dispose();
    }

    private static GmailAccessTokenProvider CreateProvider(
        HttpMessageHandler handler,
        GmailApiSettings? settings = null)
    {
        settings ??= new GmailApiSettings
        {
            ClientId = "client-id",
            ClientSecret = "client-secret",
            RefreshToken = "refresh-token",
            SenderEmail = "sender@gmail.com"
        };

        var client = new HttpClient(handler);
        return new GmailAccessTokenProvider(
            new FakeHttpClientFactory(client),
            Options.Create(settings),
            NullLogger<GmailAccessTokenProvider>.Instance);
    }

    private sealed class FakeHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class TokenHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public int CallCount { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            Body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return responseFactory(request);
        }
    }
}
