using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;
using System.Text.Json;

namespace FurniSpace.Infrastructure.Common.Storage;

internal static class FirebaseStorageClientFactory
{
    internal static StorageClient Create(FirebaseStorageSettings settings)
    {
        var credential = CreateCredential(settings);
        return credential is null
            ? StorageClient.Create()
            : StorageClient.Create(credential);
    }

    internal static UrlSigner CreateUrlSigner(FirebaseStorageSettings settings)
    {
        var credential = CreateCredential(settings)
            ?? throw new InvalidOperationException(
                "Firebase credentials are required for signed upload URLs. Configure credentials path or FIREBASE_* environment variables.");

        return UrlSigner.FromCredential(credential);
    }

    internal static GoogleCredential? CreateCredential(FirebaseStorageSettings settings)
    {
        var credentialsPath = settings.CredentialsPath
            ?? Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS")
            ?? Environment.GetEnvironmentVariable("FIREBASE_CREDENTIALS_PATH");

        if (!string.IsNullOrWhiteSpace(credentialsPath))
        {
            return GoogleCredential.FromFile(credentialsPath);
        }

        return CreateCredentialFromEnvironment();
    }

    private static GoogleCredential? CreateCredentialFromEnvironment()
    {
        var projectId = GetEnvironmentValue("FIREBASE_PROJECT_ID");
        var privateKey = GetEnvironmentValue("FIREBASE_PRIVATE_KEY")?.Replace("\\n", "\n", StringComparison.Ordinal);
        var clientEmail = GetEnvironmentValue("FIREBASE_CLIENT_EMAIL");

        if (string.IsNullOrWhiteSpace(projectId) ||
            string.IsNullOrWhiteSpace(privateKey) ||
            string.IsNullOrWhiteSpace(clientEmail))
        {
            return null;
        }

        var serviceAccount = new Dictionary<string, string?>
        {
            ["type"] = GetEnvironmentValue("FIREBASE_TYPE") ?? "service_account",
            ["project_id"] = projectId,
            ["private_key_id"] = GetEnvironmentValue("FIREBASE_PRIVATE_KEY_ID"),
            ["private_key"] = privateKey,
            ["client_email"] = clientEmail,
            ["client_id"] = GetEnvironmentValue("FIREBASE_CLIENT_ID"),
            ["auth_uri"] = GetEnvironmentValue("FIREBASE_AUTH_URI") ?? "https://accounts.google.com/o/oauth2/auth",
            ["token_uri"] = GetEnvironmentValue("FIREBASE_TOKEN_URI") ?? "https://oauth2.googleapis.com/token",
            ["auth_provider_x509_cert_url"] = GetEnvironmentValue("FIREBASE_AUTH_PROVIDER_X509_CERT_URL") ?? "https://www.googleapis.com/oauth2/v1/certs",
            ["client_x509_cert_url"] = GetEnvironmentValue("FIREBASE_CLIENT_X509_CERT_URL")
        };

        var json = JsonSerializer.Serialize(serviceAccount);
        return GoogleCredential.FromJson(json);
    }

    private static string? GetEnvironmentValue(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        value = value.Trim();
        if (value.Length >= 2 &&
            ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
        {
            value = value[1..^1];
        }

        return value;
    }
}
