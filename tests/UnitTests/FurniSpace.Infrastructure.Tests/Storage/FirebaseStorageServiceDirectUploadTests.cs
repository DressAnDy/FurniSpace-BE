#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Infrastructure.Common.Storage;
using Google;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StorageObject = Google.Apis.Storage.v1.Data.Object;
using Xunit;

namespace FurniSpace.Infrastructure.Tests.Storage;

public sealed class FirebaseStorageServiceDirectUploadTests
{
    private static readonly FirebaseStorageSettings Settings = new()
    {
        Bucket = "test-bucket",
        UploadSignedUrlExpirationMinutes = 15
    };

    [Fact]
    public async Task CreateSignedUploadUrlAsync_WithValidRequest_ReturnsSignedUploadMetadata()
    {
        var urlSigner = new FakeSignedUploadUrlGenerator { SignedUrl = "https://signed.example/upload" };
        var service = CreateService(new FakeStorageObjectClient(), urlSigner);

        var result = await service.CreateSignedUploadUrlAsync(new StorageSignedUploadRequest
        {
            ObjectName = "projects/file.jpg",
            ContentType = "image/jpeg",
            Expiration = TimeSpan.FromMinutes(10)
        });

        Assert.Equal("https://signed.example/upload", result.UploadUrl);
        Assert.Equal("projects/file.jpg", result.ObjectName);
        Assert.Equal("test-bucket", result.Bucket);
        Assert.Equal("image/jpeg", result.ContentType);
        Assert.True(result.ExpiresAt > DateTime.UtcNow);
        Assert.NotNull(urlSigner.LastTemplate);
        Assert.NotNull(urlSigner.LastOptions);
    }

    [Fact]
    public async Task CreateSignedUploadUrlAsync_WithBlankContentType_UsesOctetStreamDefault()
    {
        var urlSigner = new FakeSignedUploadUrlGenerator { SignedUrl = "https://signed.example/upload" };
        var service = CreateService(new FakeStorageObjectClient(), urlSigner);

        var result = await service.CreateSignedUploadUrlAsync(new StorageSignedUploadRequest
        {
            ObjectName = "projects/file.bin",
            ContentType = "   ",
            Expiration = TimeSpan.Zero
        });

        Assert.Equal("application/octet-stream", result.ContentType);
    }

    [Fact]
    public async Task FinalizeDirectUploadAsync_WithMatchingObject_UpdatesMetadataAndReturnsPublicUrl()
    {
        var storage = new FakeStorageObjectClient
        {
            ObjectToReturn = new StorageObject
            {
                Bucket = Settings.Bucket,
                Name = "projects/file.jpg",
                ContentType = "image/jpeg",
                Size = 204800
            }
        };
        var service = CreateService(storage, new FakeSignedUploadUrlGenerator());

        var result = await service.FinalizeDirectUploadAsync(new StorageDirectUploadFinalizeRequest
        {
            ObjectName = "projects/file.jpg",
            ContentType = "image/jpeg",
            ExpectedSizeBytes = 204800
        });

        Assert.Equal("test-bucket", result.Bucket);
        Assert.Equal("projects/file.jpg", result.ObjectName);
        Assert.Contains("firebasestorage.googleapis.com", result.PublicUrl, StringComparison.Ordinal);
        Assert.NotNull(storage.UpdatedObject);
        Assert.NotNull(storage.UpdatedObject!.Metadata);
        Assert.True(storage.UpdatedObject.Metadata!.ContainsKey("firebaseStorageDownloadTokens"));
    }

    [Fact]
    public async Task FinalizeDirectUploadAsync_WhenObjectMissing_ThrowsInvalidOperationException()
    {
        var storage = new FakeStorageObjectClient { ThrowNotFoundOnGet = true };
        var service = CreateService(storage, new FakeSignedUploadUrlGenerator());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.FinalizeDirectUploadAsync(new StorageDirectUploadFinalizeRequest
            {
                ObjectName = "projects/missing.jpg",
                ContentType = "image/jpeg",
                ExpectedSizeBytes = 100
            }));

        Assert.Contains("was not found", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FinalizeDirectUploadAsync_WhenSizeMismatch_ThrowsInvalidOperationException()
    {
        var storage = new FakeStorageObjectClient
        {
            ObjectToReturn = new StorageObject
            {
                Name = "projects/file.jpg",
                ContentType = "image/jpeg",
                Size = 50
            }
        };
        var service = CreateService(storage, new FakeSignedUploadUrlGenerator());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.FinalizeDirectUploadAsync(new StorageDirectUploadFinalizeRequest
            {
                ObjectName = "projects/file.jpg",
                ContentType = "image/jpeg",
                ExpectedSizeBytes = 204800
            }));

        Assert.Contains("size mismatch", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FinalizeDirectUploadAsync_WhenContentTypeMismatch_ThrowsInvalidOperationException()
    {
        var storage = new FakeStorageObjectClient
        {
            ObjectToReturn = new StorageObject
            {
                Name = "projects/file.jpg",
                ContentType = "application/pdf",
                Size = 204800
            }
        };
        var service = CreateService(storage, new FakeSignedUploadUrlGenerator());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.FinalizeDirectUploadAsync(new StorageDirectUploadFinalizeRequest
            {
                ObjectName = "projects/file.jpg",
                ContentType = "image/jpeg",
                ExpectedSizeBytes = 204800
            }));

        Assert.Contains("content type mismatch", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UploadAsync_WithValidRequest_ReturnsPublicUrl()
    {
        var storage = new FakeStorageObjectClient();
        var service = CreateService(storage, new FakeSignedUploadUrlGenerator());

        await using var content = new MemoryStream([1, 2, 3]);
        var result = await service.UploadAsync(new StorageUploadRequest
        {
            ObjectName = "projects/upload.jpg",
            ContentType = "image/jpeg",
            Content = content
        });

        Assert.Equal("projects/upload.jpg", result.ObjectName);
        Assert.Contains("firebasestorage.googleapis.com", result.PublicUrl, StringComparison.Ordinal);
        Assert.NotNull(storage.UploadedObject);
    }

    [Fact]
    public async Task DeleteAsync_WhenObjectNameBlank_DoesNotCallStorageClient()
    {
        var storage = new FakeStorageObjectClient();
        var service = CreateService(storage, new FakeSignedUploadUrlGenerator());

        await service.DeleteAsync("   ");

        Assert.Equal(0, storage.DeleteCallCount);
    }

    [Fact]
    public async Task DeleteAsync_WhenObjectNotFound_DoesNotThrow()
    {
        var storage = new FakeStorageObjectClient { ThrowNotFoundOnDelete = true };
        var service = CreateService(storage, new FakeSignedUploadUrlGenerator());

        await service.DeleteAsync("projects/missing.jpg");

        Assert.Equal(1, storage.DeleteCallCount);
    }

    private static FirebaseStorageService CreateService(
        IStorageObjectClient storageClient,
        ISignedUploadUrlGenerator urlSigner) =>
        new FirebaseStorageService(
            storageClient,
            urlSigner,
            Options.Create(Settings),
            NullLogger<FirebaseStorageService>.Instance);

    private sealed class FakeStorageObjectClient : IStorageObjectClient
    {
        public StorageObject? ObjectToReturn { get; init; }
        public bool ThrowNotFoundOnGet { get; init; }
        public bool ThrowNotFoundOnDelete { get; init; }
        public StorageObject? UpdatedObject { get; private set; }
        public StorageObject? UploadedObject { get; private set; }
        public int DeleteCallCount { get; private set; }

        public Task<StorageObject> GetObjectAsync(
            string bucket,
            string objectName,
            CancellationToken cancellationToken = default)
        {
            if (ThrowNotFoundOnGet)
            {
                throw new GoogleApiException("storage", "Not Found")
                {
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }

            return Task.FromResult(ObjectToReturn ?? new StorageObject { Name = objectName, Bucket = bucket });
        }

        public Task UpdateObjectAsync(
            StorageObject storageObject,
            CancellationToken cancellationToken = default)
        {
            UpdatedObject = storageObject;
            return Task.CompletedTask;
        }

        public Task UploadObjectAsync(
            StorageObject storageObject,
            Stream source,
            CancellationToken cancellationToken = default)
        {
            UploadedObject = storageObject;
            return Task.CompletedTask;
        }

        public Task DeleteObjectAsync(
            string bucket,
            string objectName,
            CancellationToken cancellationToken = default)
        {
            DeleteCallCount++;
            if (ThrowNotFoundOnDelete)
            {
                throw new GoogleApiException("storage", "Not Found")
                {
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FakeSignedUploadUrlGenerator : ISignedUploadUrlGenerator
    {
        public string SignedUrl { get; init; } = "https://signed.example/default";
        public UrlSigner.RequestTemplate? LastTemplate { get; private set; }
        public UrlSigner.Options? LastOptions { get; private set; }

        public string Sign(UrlSigner.RequestTemplate template, UrlSigner.Options options)
        {
            LastTemplate = template;
            LastOptions = options;
            return SignedUrl;
        }
    }
}
