#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.Common.Storage;
using FurniSpace.Application.DTOs.Products;
using FurniSpace.Application.Tests.TestDoubles;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Common.Storage;
using FurniSpace.Infrastructure.Interfaces;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Xunit;

namespace FurniSpace.Application.Tests.Common.Storage;

public sealed class CatalogDirectFileUploadServiceTests
{
    [Fact]
    public async Task PrepareAsync_CreatesPendingRecordAndSignedUrl()
    {
        var repository = new FakeCatalogProjectFileRepository();
        var storage = new CatalogDirectUploadStorage();
        var service = CreateService(repository, storage);

        var result = await service.PrepareAsync(
            new CatalogDirectUploadPrepareRequest(
                Guid.NewGuid(),
                new UploadCatalogFileRequestDto
                {
                    OriginalFileName = "preview.jpg",
                    ContentType = "image/jpeg",
                    FileSizeBytes = 1024
                },
                CatalogFileReferenceTypes.Product,
                Guid.NewGuid(),
                (_, generated) => $"products/{generated}",
                FileType.PRODUCT_PREVIEW,
                FileVisibility.CUSTOMER_VISIBLE,
                1),
            CancellationToken.None);

        Assert.Equal(201, result.Status);
        Assert.NotNull(result.Data);
        Assert.NotEmpty(result.Data.UploadUrl);
        Assert.Single(repository.StoredFiles);
        Assert.Equal(FileStatus.PENDING, repository.StoredFiles[0].Status);
        Assert.Single(repository.FileLinks);
        Assert.NotNull(storage.SignedUploadRequest);
    }

    [Fact]
    public async Task CompleteAsync_ActivatesPendingFile()
    {
        var userId = Guid.NewGuid();
        var referenceId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var repository = new FakeCatalogProjectFileRepository();
        SeedPending(repository, fileId, userId, referenceId);
        var storage = new CatalogDirectUploadStorage();
        var service = CreateService(repository, storage);

        var result = await service.CompleteAsync(
            new CatalogDirectUploadCompleteRequest(userId, fileId, CatalogFileReferenceTypes.Product, referenceId),
            postCompleteAsync: null,
            CancellationToken.None);

        Assert.Equal(201, result.Status);
        Assert.Equal(FileStatus.ACTIVE, repository.StoredFiles[0].Status);
        Assert.NotEmpty(repository.StoredFiles[0].FileUrl);
        Assert.NotNull(storage.FinalizeRequest);
    }

    [Fact]
    public async Task CompleteAsync_WhenFileNotFound_ReturnsNotFound()
    {
        var repository = new FakeCatalogProjectFileRepository();
        var service = CreateService(repository, new CatalogDirectUploadStorage());

        var result = await service.CompleteAsync(
            new CatalogDirectUploadCompleteRequest(Guid.NewGuid(), Guid.NewGuid(), CatalogFileReferenceTypes.Product, Guid.NewGuid()),
            postCompleteAsync: null,
            CancellationToken.None);

        Assert.Equal(404, result.Status);
        Assert.Equal(DirectFileUploadErrorCodes.UploadNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task CompleteAsync_WhenWrongUser_ReturnsForbidden()
    {
        var userId = Guid.NewGuid();
        var referenceId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var repository = new FakeCatalogProjectFileRepository();
        SeedPending(repository, fileId, userId, referenceId);
        var service = CreateService(repository, new CatalogDirectUploadStorage());

        var result = await service.CompleteAsync(
            new CatalogDirectUploadCompleteRequest(Guid.NewGuid(), fileId, CatalogFileReferenceTypes.Product, referenceId),
            postCompleteAsync: null,
            CancellationToken.None);

        Assert.Equal(403, result.Status);
        Assert.Equal(DirectFileUploadErrorCodes.UploadForbidden, result.ErrorCode);
    }

    [Fact]
    public async Task CompleteAsync_WhenAlreadyActive_ReturnsSuccessWithoutFinalizing()
    {
        var userId = Guid.NewGuid();
        var referenceId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var repository = new FakeCatalogProjectFileRepository();
        SeedPending(repository, fileId, userId, referenceId);
        repository.StoredFiles[0].Status = FileStatus.ACTIVE;
        repository.StoredFiles[0].FileUrl = "https://storage.example.com/existing.jpg";
        var storage = new CatalogDirectUploadStorage();
        var service = CreateService(repository, storage);

        var result = await service.CompleteAsync(
            new CatalogDirectUploadCompleteRequest(userId, fileId, CatalogFileReferenceTypes.Product, referenceId),
            postCompleteAsync: null,
            CancellationToken.None);

        Assert.Equal(200, result.Status);
        Assert.Null(storage.FinalizeRequest);
    }

    [Fact]
    public async Task CompleteAsync_WhenSaveFails_DeletesUploadedObject()
    {
        var userId = Guid.NewGuid();
        var referenceId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var repository = new FakeCatalogProjectFileRepository();
        SeedPending(repository, fileId, userId, referenceId);
        var storage = new CatalogDirectUploadStorage();
        var unitOfWork = TestUnitOfWork.ForFailingSaveChanges();
        var service = CreateService(repository, storage, unitOfWork);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CompleteAsync(
                new CatalogDirectUploadCompleteRequest(userId, fileId, CatalogFileReferenceTypes.Product, referenceId),
                postCompleteAsync: null,
                CancellationToken.None));

        Assert.NotNull(storage.DeletedObjectName);
    }

    private static void SeedPending(
        FakeCatalogProjectFileRepository repository,
        Guid fileId,
        Guid uploadedBy,
        Guid referenceId)
    {
        repository.StoredFiles.Add(new StoredFile
        {
            FileId = fileId,
            UploadedBy = uploadedBy,
            OriginalFileName = "preview.jpg",
            StoredFileName = $"{fileId:N}.jpg",
            FileUrl = string.Empty,
            StoragePath = $"products/{referenceId:D}/{fileId:N}.jpg",
            MimeType = "image/jpeg",
            FileExtension = "jpg",
            FileSizeBytes = 1024,
            Status = FileStatus.PENDING,
            UploadedAt = DateTime.UtcNow
        });
        repository.FileLinks.Add(new FileLink
        {
            FileLinkId = Guid.NewGuid(),
            FileId = fileId,
            ReferenceType = CatalogFileReferenceTypes.Product,
            ReferenceId = referenceId,
            FileType = FileType.PRODUCT_PREVIEW,
            Visibility = FileVisibility.CUSTOMER_VISIBLE,
            CreatedBy = uploadedBy,
            CreatedAt = DateTime.UtcNow
        });
    }

    private static CatalogDirectFileUploadService CreateService(
        IProjectFileRepository repository,
        CatalogDirectUploadStorage storage,
        IUnitOfWork? unitOfWork = null)
    {
        var firebaseSettings = CatalogServiceTestHelper.DefaultFirebaseSettings();
        return new CatalogDirectFileUploadService(
            repository,
            unitOfWork ?? TestUnitOfWork.ForSaveChanges(_ => Task.FromResult(1)),
            DirectUploadTestDoubles.CreateCoordinator(storage, firebaseSettings),
            firebaseSettings);
    }

    private sealed class CatalogDirectUploadStorage : IFileStorageService, IDirectFileUploadStorageService
    {
        public StorageSignedUploadRequest? SignedUploadRequest { get; private set; }
        public StorageDirectUploadFinalizeRequest? FinalizeRequest { get; private set; }
        public string? DeletedObjectName { get; private set; }

        public Task<StorageSignedUploadResult> CreateSignedUploadUrlAsync(
            StorageSignedUploadRequest request,
            CancellationToken cancellationToken = default)
        {
            SignedUploadRequest = request;
            return Task.FromResult(new StorageSignedUploadResult
            {
                UploadUrl = $"https://storage.example.com/upload/{request.ObjectName}",
                ContentType = request.ContentType,
                ExpiresAt = DateTime.UtcNow.AddMinutes(15)
            });
        }

        public Task<StorageUploadResult> FinalizeDirectUploadAsync(
            StorageDirectUploadFinalizeRequest request,
            CancellationToken cancellationToken = default)
        {
            FinalizeRequest = request;
            return Task.FromResult(new StorageUploadResult
            {
                ObjectName = request.ObjectName,
                PublicUrl = $"https://storage.example.com/{request.ObjectName}",
                Bucket = "test-bucket"
            });
        }

        public Task<StorageUploadResult> UploadAsync(StorageUploadRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new StorageUploadResult
            {
                ObjectName = request.ObjectName,
                PublicUrl = $"https://storage.example.com/{request.ObjectName}",
                Bucket = "test-bucket"
            });

        public Task DeleteAsync(string objectName, CancellationToken cancellationToken = default)
        {
            DeletedObjectName = objectName;
            return Task.CompletedTask;
        }
    }
}
