using System.Text.Json.Serialization;

namespace FurniSpace.Application.DTOs;

public sealed class AuthResponseDto
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("refresh_token")]
    [JsonIgnore]
    public string RefreshToken { get; set; } = string.Empty;

    [JsonPropertyName("access_token_expires_at")]
    public DateTimeOffset AccessTokenExpiresAt { get; set; }

    [JsonPropertyName("refresh_token_expires_at")]
    [JsonIgnore]
    public DateTimeOffset RefreshTokenExpiresAt { get; set; }

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = "Bearer";

    [JsonPropertyName("expires_in")]
    public long ExpiresIn => Math.Max(0, (long)(AccessTokenExpiresAt - DateTimeOffset.UtcNow).TotalSeconds);
}
