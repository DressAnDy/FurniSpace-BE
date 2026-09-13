using System.Text.Json;
using FurniSpace.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FurniSpace.Infrastructure.Common.Email;

public sealed class GmailAccessTokenProvider : IGmailAccessTokenProvider, IDisposable
{
    private static readonly TimeSpan RefreshBuffer = TimeSpan.FromMinutes(1);
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GmailApiSettings _settings;
    private readonly ILogger<GmailAccessTokenProvider> _logger;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private string? _accessToken;
    private DateTimeOffset _expiresAt;

    public GmailAccessTokenProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<GmailApiSettings> settings,
        ILogger<GmailAccessTokenProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        if (HasUsableToken())
        {
            return _accessToken!;
        }

        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            if (HasUsableToken())
            {
                return _accessToken!;
            }

            return await RefreshAccessTokenAsync(cancellationToken);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public void Invalidate()
    {
        _accessToken = null;
        _expiresAt = DateTimeOffset.MinValue;
    }

    public void Dispose()
    {
        _refreshLock.Dispose();
    }

    private bool HasUsableToken() =>
        !string.IsNullOrWhiteSpace(_accessToken) &&
        DateTimeOffset.UtcNow < _expiresAt - RefreshBuffer;

    private async Task<string> RefreshAccessTokenAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, _settings.TokenUrl)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = _settings.ClientId,
                ["client_secret"] = _settings.ClientSecret,
                ["refresh_token"] = _settings.RefreshToken,
                ["grant_type"] = "refresh_token"
            })
        };

        HttpResponseMessage response;
        try
        {
            response = await _httpClientFactory
                .CreateClient(GmailEmailClientNames.OAuth)
                .SendAsync(request, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError("Google OAuth token request timed out.");
            throw new EmailDeliveryException("Google OAuth token request timed out.");
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "Google OAuth token endpoint could not be reached.");
            throw new EmailDeliveryException(
                "Google OAuth token endpoint could not be reached.",
                innerException: exception);
        }

        using (response)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Google OAuth rejected a refresh-token request with status code {StatusCode}.",
                    (int)response.StatusCode);
                throw new EmailDeliveryException(
                    $"Google OAuth rejected the refresh-token request with status code {(int)response.StatusCode}.",
                    response.StatusCode,
                    Truncate(content));
            }

            using var document = JsonDocument.Parse(content);
            if (!document.RootElement.TryGetProperty("access_token", out var accessTokenElement) ||
                string.IsNullOrWhiteSpace(accessTokenElement.GetString()))
            {
                throw new EmailDeliveryException("Google OAuth response did not contain an access token.");
            }

            var expiresInSeconds = document.RootElement.TryGetProperty("expires_in", out var expiresInElement)
                ? expiresInElement.GetInt32()
                : 3_600;

            _accessToken = accessTokenElement.GetString();
            _expiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresInSeconds);
            return _accessToken!;
        }
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_settings.ClientId) ||
            string.IsNullOrWhiteSpace(_settings.ClientSecret) ||
            string.IsNullOrWhiteSpace(_settings.RefreshToken))
        {
            throw new EmailDeliveryException(
                "Gmail API OAuth is not configured. Set GmailApi__ClientId, GmailApi__ClientSecret, and GmailApi__RefreshToken.");
        }
    }

    private static string? Truncate(string content)
    {
        const int maxLength = 1_000;
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        return content.Length <= maxLength ? content : content[..maxLength];
    }
}

public static class GmailEmailClientNames
{
    public const string OAuth = "GmailOAuth";
}
