using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;
using FurniSpace.Infrastructure.Common.Storage;
using FurniSpace.Infrastructure.Interfaces;
using Google;
using Microsoft.Extensions.Options;
using System.Text.Json;
using StorageObject = Google.Apis.Storage.v1.Data.Object;

namespace FurniSpace.Infrastructure.Common.Storage;

public sealed class FirebaseStorageService : IFileStorageService
{
    private const string DownloadTokenMetadataKey = "firebaseStorageDownloadTokens";
    private readonly FirebaseStorageSettings _settings;
    private readonly StorageClient _storageClient;

    public FirebaseStorageService(IOptions<FirebaseStorageSettings> settings)
    {
        _settings = settings.Value;
        _storageClient = CreateStorageClient(_settings);
    }

    public async Task<StorageUploadResult> UploadAsync(
        StorageUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.Bucket))
        {
            throw new InvalidOperationException("Firebase storage bucket is missing. Set FirebaseStorage__Bucket or FIREBASE_STORAGE_BUCKET.");
        }

        var downloadToken = Guid.NewGuid().ToString("N");
        var storageObject = new StorageObject
        {
            Bucket = _settings.Bucket,
            Name = request.ObjectName,
            ContentType = request.ContentType,
            Metadata = new Dictionary<string, string>
            {
                [DownloadTokenMetadataKey] = downloadToken
            }
        };

        await _storageClient.UploadObjectAsync(
            storageObject,
            request.Content,
            cancellationToken: cancellationToken);

        return new StorageUploadResult
        {
            Bucket = _settings.Bucket,
            ObjectName = request.ObjectName,
            PublicUrl = BuildFirebaseDownloadUrl(_settings.Bucket, request.ObjectName, downloadToken)
        };
    }

    public async Task DeleteAsync(
        string objectName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.Bucket))
        {
            throw new InvalidOperationException("Firebase storage bucket is missing. Set FirebaseStorage__Bucket or FIREBASE_STORAGE_BUCKET.");
        }

        if (string.IsNullOrWhiteSpace(objectName))
        {
            return;
        }

        try
        {
            await _storageClient.DeleteObjectAsync(
                _settings.Bucket,
                objectName,
                cancellationToken: cancellationToken);
        }
        catch (GoogleApiException exception) when (exception.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Storage object is already gone; DB hard delete can continue.
        }
    }

    private static StorageClient CreateStorageClient(FirebaseStorageSettings settings)
    {
        var credentialsPath = settings.CredentialsPath
            ?? Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS")
            ?? Environment.GetEnvironmentVariable("FIREBASE_CREDENTIALS_PATH");

        if (string.IsNullOrWhiteSpace(credentialsPath))
        {
            var credential = CreateCredentialFromEnvironment();
            return credential is null
                ? StorageClient.Create()
                : StorageClient.Create(credential);
        }

        return StorageClient.Create(GoogleCredential.FromFile(credentialsPath));
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

    private static string BuildFirebaseDownloadUrl(string bucket, string objectName, string downloadToken)
    {
        var encodedObjectName = Uri.EscapeDataString(objectName);
        return $"https://firebasestorage.googleapis.com/v0/b/{bucket}/o/{encodedObjectName}?alt=media&token={downloadToken}";
    }
}
