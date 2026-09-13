using Google.Cloud.Storage.V1;
using FurniSpace.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using Google;
using StorageObject = Google.Apis.Storage.v1.Data.Object;

namespace FurniSpace.Infrastructure.Common.Storage;

public sealed class FirebaseStorageService : IFileStorageService, IDirectFileUploadStorageService
{
    private const string DownloadTokenMetadataKey = "firebaseStorageDownloadTokens";
    private readonly FirebaseStorageSettings _settings;
    private readonly ILogger<FirebaseStorageService> _logger;
    private IStorageObjectClient? _storageClient;
    private ISignedUploadUrlGenerator? _urlSigner;

    public FirebaseStorageService(
        IOptions<FirebaseStorageSettings> settings,
        ILogger<FirebaseStorageService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    internal FirebaseStorageService(
        IStorageObjectClient storageClient,
        ISignedUploadUrlGenerator urlSigner,
        IOptions<FirebaseStorageSettings> settings,
        ILogger<FirebaseStorageService> logger)
    {
        _storageClient = storageClient;
        _urlSigner = urlSigner;
        _settings = settings.Value;
        _logger = logger;
    }

    public Task<StorageSignedUploadResult> CreateSignedUploadUrlAsync(
        StorageSignedUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureBucketConfigured();

        var expiration = request.Expiration > TimeSpan.Zero
            ? request.Expiration
            : TimeSpan.FromMinutes(Math.Clamp(_settings.UploadSignedUrlExpirationMinutes, 1, 60));
        var expiresAt = DateTime.UtcNow.Add(expiration);
        var contentType = string.IsNullOrWhiteSpace(request.ContentType)
            ? "application/octet-stream"
            : request.ContentType.Trim();

        var template = UrlSigner.RequestTemplate
            .FromBucket(_settings.Bucket)
            .WithObjectName(request.ObjectName)
            .WithHttpMethod(HttpMethod.Put)
            .WithContentHeaders(new Dictionary<string, IEnumerable<string>>
            {
                ["Content-Type"] = [contentType]
            });

        var options = UrlSigner.Options.FromExpiration(expiresAt);
        var signedUrl = ResolveUrlSigner().Sign(template, options);

        return Task.FromResult(new StorageSignedUploadResult
        {
            UploadUrl = signedUrl,
            ObjectName = request.ObjectName,
            Bucket = _settings.Bucket,
            ContentType = contentType,
            ExpiresAt = expiresAt
        });
    }

    public async Task<StorageUploadResult> FinalizeDirectUploadAsync(
        StorageDirectUploadFinalizeRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureBucketConfigured();

        StorageObject storageObject;
        try
        {
            storageObject = await ResolveStorageClient().GetObjectAsync(
                _settings.Bucket,
                request.ObjectName,
                cancellationToken: cancellationToken);
        }
        catch (GoogleApiException exception) when (exception.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException(
                $"Storage object '{request.ObjectName}' was not found. Upload the file to the signed URL before completing.",
                exception);
        }

        var actualSize = storageObject.Size is null ? 0L : (long)storageObject.Size.Value;
        if (request.ExpectedSizeBytes > 0 && actualSize != request.ExpectedSizeBytes)
        {
            throw new InvalidOperationException(
                $"Storage object size mismatch. Expected {request.ExpectedSizeBytes} bytes but found {actualSize} bytes.");
        }

        var normalizedContentType = string.IsNullOrWhiteSpace(request.ContentType)
            ? "application/octet-stream"
            : request.ContentType.Trim();
        if (!string.IsNullOrWhiteSpace(storageObject.ContentType) &&
            !string.Equals(storageObject.ContentType, normalizedContentType, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Storage object content type mismatch. Expected '{normalizedContentType}' but found '{storageObject.ContentType}'.");
        }

        var downloadToken = Guid.NewGuid().ToString("N");
        storageObject.Metadata ??= new Dictionary<string, string>();
        storageObject.Metadata[DownloadTokenMetadataKey] = downloadToken;
        storageObject.ContentType = normalizedContentType;

        await ResolveStorageClient().UpdateObjectAsync(storageObject, cancellationToken: cancellationToken);

        return new StorageUploadResult
        {
            Bucket = _settings.Bucket,
            ObjectName = request.ObjectName,
            PublicUrl = BuildFirebaseDownloadUrl(_settings.Bucket, request.ObjectName, downloadToken)
        };
    }

    public async Task<StorageUploadResult> UploadAsync(
        StorageUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureBucketConfigured();

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

        var stopwatch = Stopwatch.StartNew();
        await ResolveStorageClient().UploadObjectAsync(
            storageObject,
            request.Content,
            cancellationToken: cancellationToken);
        stopwatch.Stop();

        if (stopwatch.ElapsedMilliseconds >= 1_000)
        {
            _logger.LogWarning(
                "Firebase upload completed in {ElapsedMs} ms for object {ObjectName} ({ContentType})",
                stopwatch.ElapsedMilliseconds,
                request.ObjectName,
                request.ContentType);
        }
        else
        {
            _logger.LogDebug(
                "Firebase upload completed in {ElapsedMs} ms for object {ObjectName}",
                stopwatch.ElapsedMilliseconds,
                request.ObjectName);
        }

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
        EnsureBucketConfigured();

        if (string.IsNullOrWhiteSpace(objectName))
        {
            return;
        }

        try
        {
            await ResolveStorageClient().DeleteObjectAsync(
                _settings.Bucket,
                objectName,
                cancellationToken: cancellationToken);
        }
        catch (GoogleApiException exception) when (exception.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Storage object is already gone; DB hard delete can continue.
        }
    }

    private void EnsureBucketConfigured()
    {
        if (string.IsNullOrWhiteSpace(_settings.Bucket))
        {
            throw new InvalidOperationException("Firebase storage bucket is missing. Set FirebaseStorage__Bucket or FIREBASE_STORAGE_BUCKET.");
        }
    }

    private IStorageObjectClient ResolveStorageClient() =>
        _storageClient ??= new GoogleStorageObjectClient(FirebaseStorageClientFactory.Create(_settings));

    private ISignedUploadUrlGenerator ResolveUrlSigner() =>
        _urlSigner ??= new GoogleSignedUploadUrlGenerator(FirebaseStorageClientFactory.CreateUrlSigner(_settings));

    internal static string BuildFirebaseDownloadUrl(string bucket, string objectName, string downloadToken)
    {
        var encodedObjectName = Uri.EscapeDataString(objectName);
        return $"https://firebasestorage.googleapis.com/v0/b/{bucket}/o/{encodedObjectName}?alt=media&token={downloadToken}";
    }
}
