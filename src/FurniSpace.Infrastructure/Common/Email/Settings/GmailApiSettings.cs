namespace FurniSpace.Infrastructure.Common.Email;

public sealed class GmailApiSettings
{
    public const string SectionName = "GmailApi";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string SenderEmail { get; set; } = string.Empty;
    public string SenderName { get; set; } = "FurniSpace";
    public string ResetPasswordUrl { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://gmail.googleapis.com/gmail/v1/";
    public string TokenUrl { get; set; } = "https://oauth2.googleapis.com/token";
    public int TimeoutSeconds { get; set; } = 10;
}
