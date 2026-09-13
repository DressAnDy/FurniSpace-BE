using Microsoft.Extensions.Configuration;

namespace FurniSpace.Application.Common.Payments;

internal static class PayOsOptionsConfiguration
{
    public static void ApplyEnvironmentOverrides(PayOsOptions options, IConfiguration configuration)
    {
        if (bool.TryParse(configuration["PAYOS_ENABLED"], out var enabled))
        {
            options.Enabled = enabled;
        }

        options.Environment = configuration["PAYOS_ENVIRONMENT"] ?? options.Environment;
        options.ClientId = configuration["PAYOS_CLIENT_ID"] ?? options.ClientId;
        options.ApiKey = configuration["PAYOS_API_KEY"] ?? options.ApiKey;
        options.ChecksumKey = configuration["PAYOS_CHECKSUM_KEY"] ?? options.ChecksumKey;
        options.ApiBaseUrl = configuration["PAYOS_API_BASE_URL"] ?? options.ApiBaseUrl;
        options.ReturnUrl = configuration["PAYOS_RETURN_URL"] ?? options.ReturnUrl;
        options.CancelUrl = configuration["PAYOS_CANCEL_URL"] ?? options.CancelUrl;
        options.WebhookUrl = configuration["PAYOS_WEBHOOK_URL"] ?? options.WebhookUrl;
        options.DescriptionPrefix = configuration["PAYOS_DESCRIPTION_PREFIX"] ?? options.DescriptionPrefix;

        if (int.TryParse(configuration["PAYOS_MAX_DESCRIPTION_LENGTH"], out var maxDescriptionLength))
        {
            options.MaxDescriptionLength = maxDescriptionLength;
        }
    }
}
