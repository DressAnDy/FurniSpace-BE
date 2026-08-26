#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.Common.MeasurementImages;
using FurniSpace.Application.DTOs.MeasurementImages;
using FurniSpace.Application.Services.MeasurementImages;
using FurniSpace.Application.Tests;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Common.Storage;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Infrastructure.ReadModels.Products;
using FurniSpace.Infrastructure.ReadModels.ProjectFiles;
using FurniSpace.Infrastructure.ReadModels.ProjectSchedules;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Microsoft.Extensions.Options;
using Xunit;

namespace FurniSpace.Application.Tests.MeasurementImages;

public sealed class MeasurementImageServiceTests
{
    public MeasurementImageServiceTests()
    {
        MapsterTestSetup.EnsureConfigured();
    }

    [Fact]
    public async Task RegisterMeasurementImageAsync_AssignedDesignerDuringConfirmedSchedule_CreatesScheduleLink()
    {
        var designerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var scheduleId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var scheduleRepo = new MeasurementScheduleRepositoryFake
        {
            Detail = CreateMeasurementSchedule(scheduleId, projectId, designerId, ProjectScheduleStatus.CONFIRMED)
        };
        var fileRepo = new MeasurementProjectFileRepositoryFake
        {
            RoleName = "DESIGNER",
            ProjectAccess = CreateProjectAccess(projectId, assignedDesignerId: designerId)
        };
        var unitOfWork = new MeasurementUnitOfWorkFake();
        var service = CreateService(scheduleRepo, fileRepo, unitOfWork);
        var request = CreateRegisterRequest(projectId, fileId);

        var result = await service.RegisterMeasurementImageAsync(scheduleId, designerId, request);

        Assert.Equal(201, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(fileId, result.Data.FileId);
        Assert.Equal(FileType.SPACE_IMAGE, result.Data.FileType);
        Assert.Equal("PROJECT_SCHEDULE", result.Data.ReferenceType);
        Assert.Equal(scheduleId, result.Data.ReferenceId);
        Assert.Single(fileRepo.StoredFiles);
        Assert.Single(fileRepo.FileLinks);
        Assert.Equal(FileVisibility.STAFF_ONLY, fileRepo.FileLinks[0].Visibility);
        Assert.Equal(1, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task RegisterMeasurementImageAsync_BeforeScheduledStart_AllowsEarlyCapture()
    {
        var designerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var scheduleId = Guid.NewGuid();
        var schedule = CreateMeasurementSchedule(scheduleId, projectId, designerId, ProjectScheduleStatus.CONFIRMED);
        schedule.ScheduledStart = DateTime.UtcNow.AddHours(2);
        var service = CreateService(
            new MeasurementScheduleRepositoryFake { Detail = schedule },
            new MeasurementProjectFileRepositoryFake { RoleName = "DESIGNER" },
            new MeasurementUnitOfWorkFake());

        var result = await service.RegisterMeasurementImageAsync(
            scheduleId,
            designerId,
            CreateRegisterRequest(projectId, Guid.NewGuid()));

        Assert.Equal(201, result.Status);
    }

    [Fact]
    public async Task RegisterMeasurementImageAsync_WhenScheduleCompleted_ReturnsBadRequest()
    {
        var designerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var scheduleId = Guid.NewGuid();
        var schedule = CreateMeasurementSchedule(scheduleId, projectId, designerId, ProjectScheduleStatus.COMPLETED);
        var service = CreateService(
            new MeasurementScheduleRepositoryFake { Detail = schedule },
            new MeasurementProjectFileRepositoryFake { RoleName = "DESIGNER" },
            new MeasurementUnitOfWorkFake());

        var result = await service.RegisterMeasurementImageAsync(
            scheduleId,
            designerId,
            CreateRegisterRequest(projectId, Guid.NewGuid()));

        Assert.Equal(400, result.Status);
        Assert.Equal(MeasurementImageErrorCodes.ScheduleNotEligible, result.ErrorCode);
    }

    [Fact]
    public async Task RegisterMeasurementImageAsync_UnassignedDesigner_ReturnsForbidden()
    {
        var designerId = Guid.NewGuid();
        var otherDesignerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var scheduleId = Guid.NewGuid();
        var service = CreateService(
            new MeasurementScheduleRepositoryFake
            {
                Detail = CreateMeasurementSchedule(scheduleId, projectId, designerId, ProjectScheduleStatus.CONFIRMED)
            },
            new MeasurementProjectFileRepositoryFake { RoleName = "DESIGNER" },
            new MeasurementUnitOfWorkFake());

        var result = await service.RegisterMeasurementImageAsync(
            scheduleId,
            otherDesignerId,
            CreateRegisterRequest(projectId, Guid.NewGuid()));

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task LinkMeasurementImageToAreaAsync_AssignedDesigner_CreatesAreaLink()
    {
        var designerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var projectAreaId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var fileRepo = new MeasurementProjectFileRepositoryFake
        {
            RoleName = "DESIGNER",
            ProjectAccess = CreateProjectAccess(projectId, assignedDesignerId: designerId),
            HasMeasurementScheduleLink = true
        };
        var unitOfWork = new MeasurementUnitOfWorkFake();
        var service = CreateService(new MeasurementScheduleRepositoryFake(), fileRepo, unitOfWork);

        var result = await service.LinkMeasurementImageToAreaAsync(projectAreaId, fileId, designerId);

        Assert.Equal(201, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(projectAreaId, result.Data.ProjectAreaId);
        Assert.Equal(fileId, result.Data.FileId);
        Assert.Single(fileRepo.FileLinks);
        Assert.Equal("PROJECT_AREA", fileRepo.FileLinks[0].ReferenceType);
        Assert.Equal(FileType.SPACE_IMAGE, fileRepo.FileLinks[0].FileType);
    }

    [Fact]
    public async Task UnlinkMeasurementImageFromAreaAsync_RemovesOnlyAreaLink()
    {
        var designerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var projectAreaId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var areaLink = new FileLink
        {
            FileLinkId = Guid.NewGuid(),
            FileId = fileId,
            ReferenceType = "PROJECT_AREA",
            ReferenceId = projectAreaId,
            FileType = FileType.SPACE_IMAGE
        };
        var fileRepo = new MeasurementProjectFileRepositoryFake
        {
            RoleName = "DESIGNER",
            ProjectAccess = CreateProjectAccess(projectId, assignedDesignerId: designerId),
            ExistingAreaLink = areaLink
        };
        var unitOfWork = new MeasurementUnitOfWorkFake();
        var service = CreateService(new MeasurementScheduleRepositoryFake(), fileRepo, unitOfWork);

        var result = await service.UnlinkMeasurementImageFromAreaAsync(projectAreaId, fileId, designerId);

        Assert.Equal(200, result.Status);
        Assert.Empty(fileRepo.FileLinks);
        Assert.Equal(1, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task GetProjectMeasurementImagesAsync_UnassignedFilter_DelegatesToRepository()
    {
        var salesId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var fileRepo = new MeasurementProjectFileRepositoryFake
        {
            RoleName = "SALES",
            ProjectAccess = CreateProjectAccess(projectId, assignedSalesId: salesId),
            GalleryPage = new MeasurementImageGalleryPageReadModel
            {
                Total = 1,
                Items =
                [
                    new MeasurementImageGalleryItemReadModel
                    {
                        FileId = Guid.NewGuid(),
                        FileUrl = "https://example.com/photo.jpg",
                        UploadedAt = DateTime.UtcNow,
                        ScheduleId = Guid.NewGuid(),
                        ScheduledStart = DateTime.UtcNow.AddDays(-1)
                    }
                ]
            }
        };
        var service = CreateService(new MeasurementScheduleRepositoryFake(), fileRepo, new MeasurementUnitOfWorkFake());

        var result = await service.GetProjectMeasurementImagesAsync(
            projectId,
            salesId,
            new MeasurementImageGalleryQueryDto
            {
                Assigned = false,
                Page = 1,
                Limit = 20
            });

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data.Items);
        Assert.False(fileRepo.LastGalleryQuery?.Assigned);
        Assert.Equal(projectId, fileRepo.LastGalleryQuery?.ProjectId);
    }

    [Fact]
    public async Task GetProjectAreaMeasurementImagesAsync_ReturnsGalleryForAuthorizedUser()
    {
        var designerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var projectAreaId = Guid.NewGuid();
        var fileRepo = new MeasurementProjectFileRepositoryFake
        {
            RoleName = "DESIGNER",
            ProjectAccess = CreateProjectAccess(projectId, assignedDesignerId: designerId),
            GalleryPage = new MeasurementImageGalleryPageReadModel
            {
                Total = 1,
                Items =
                [
                    new MeasurementImageGalleryItemReadModel
                    {
                        FileId = Guid.NewGuid(),
                        FileUrl = "https://example.com/area.jpg",
                        UploadedAt = DateTime.UtcNow,
                        ScheduleId = Guid.NewGuid(),
                        ScheduledStart = DateTime.UtcNow.AddDays(-1)
                    }
                ]
            }
        };
        var service = CreateService(new MeasurementScheduleRepositoryFake(), fileRepo, new MeasurementUnitOfWorkFake());

        var result = await service.GetProjectAreaMeasurementImagesAsync(
            projectAreaId,
            designerId,
            new MeasurementImageGalleryQueryDto { Page = 1, Limit = 10 });

        Assert.Equal(200, result.Status);
        Assert.Single(result.Data!.Items);
        Assert.Equal(projectAreaId, fileRepo.LastGalleryQuery?.ProjectAreaId);
    }

    [Fact]
    public async Task GetScheduleMeasurementImagesAsync_ReturnsGalleryForAuthorizedUser()
    {
        var designerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var scheduleId = Guid.NewGuid();
        var fileRepo = new MeasurementProjectFileRepositoryFake
        {
            RoleName = "DESIGNER",
            ProjectAccess = CreateProjectAccess(projectId, assignedDesignerId: designerId),
            GalleryPage = new MeasurementImageGalleryPageReadModel { Total = 0, Items = [] }
        };
        var scheduleRepo = new MeasurementScheduleRepositoryFake
        {
            Detail = CreateMeasurementSchedule(scheduleId, projectId, designerId, ProjectScheduleStatus.CONFIRMED)
        };
        var service = CreateService(scheduleRepo, fileRepo, new MeasurementUnitOfWorkFake());

        var result = await service.GetScheduleMeasurementImagesAsync(
            scheduleId,
            designerId,
            new MeasurementImageGalleryQueryDto { Assigned = true });

        Assert.Equal(200, result.Status);
        Assert.Equal(scheduleId, fileRepo.LastGalleryQuery?.ScheduleId);
        Assert.True(fileRepo.LastGalleryQuery?.Assigned);
    }

    private static MeasurementImageService CreateService(
        MeasurementScheduleRepositoryFake scheduleRepo,
        MeasurementProjectFileRepositoryFake fileRepo,
        MeasurementUnitOfWorkFake unitOfWork)
    {
        return new MeasurementImageService(
            scheduleRepo,
            fileRepo,
            new MeasurementImageServiceDependencies(
                unitOfWork,
                Options.Create(new FileUploadSettings()),
                Options.Create(new FirebaseStorageSettings { ProjectFilesPrefix = "projects" })));
    }

    private static ProjectScheduleDetailReadModel CreateMeasurementSchedule(
        Guid scheduleId,
        Guid projectId,
        Guid assignedStaffId,
        ProjectScheduleStatus status)
    {
        return new ProjectScheduleDetailReadModel
        {
            ScheduleId = scheduleId,
            ProjectId = projectId,
            ScheduleType = ProjectScheduleType.MEASUREMENT,
            Status = status,
            AssignedStaffId = assignedStaffId,
            ScheduledStart = DateTime.UtcNow.AddHours(-1)
        };
    }

    private static ProjectFileAccessReadModel CreateProjectAccess(
        Guid projectId,
        Guid? assignedSalesId = null,
        Guid? assignedDesignerId = null)
    {
        return new ProjectFileAccessReadModel
        {
            ProjectId = projectId,
            CustomerId = Guid.NewGuid(),
            AssignedSalesId = assignedSalesId,
            AssignedDesignerId = assignedDesignerId
        };
    }

    private static RegisterMeasurementImageRequestDto CreateRegisterRequest(Guid projectId, Guid fileId)
    {
        return new RegisterMeasurementImageRequestDto
        {
            StoragePath = $"projects/{projectId:D}/{fileId:N}.jpg",
            PublicUrl = "https://firebasestorage.googleapis.com/v0/b/demo/o/photo.jpg",
            OriginalFileName = "kitchen.jpg",
            ContentType = "image/jpeg",
            FileSizeBytes = 1024
        };
    }
}

internal sealed class MeasurementScheduleRepositoryFake : IProjectScheduleRepository
{
    public ProjectScheduleDetailReadModel? Detail { get; init; }

    public Task<ProjectScheduleDetailReadModel?> GetDetailAsync(
        Guid scheduleId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Detail);

    public Task<(IReadOnlyList<ProjectScheduleListItemReadModel> Items, int Total)> GetListByProjectAsync(
        Guid projectId,
        ProjectScheduleListQueryReadModel query,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<(IReadOnlyList<ProjectScheduleListItemReadModel>, int)>(([], 0));

    public Task<(IReadOnlyList<ProjectScheduleListItemReadModel> Items, int Total)> GetMyAssignedAsync(
        Guid? staffId,
        ProjectScheduleListQueryReadModel query,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<(IReadOnlyList<ProjectScheduleListItemReadModel>, int)>(([], 0));

    public Task<bool> HasCompletedMeasurementScheduleAsync(
        Guid projectId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public Task<bool> ExistsMeasurementScheduleAsync(
        Guid projectId,
        ProjectScheduleStatus? status,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public Task<bool> HasAssignedScheduleAsync(
        Guid projectId,
        Guid staffId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public IQueryable<ProjectSchedule> Query() => Enumerable.Empty<ProjectSchedule>().AsQueryable();
    public Task<ProjectSchedule?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult<ProjectSchedule?>(null);
    public Task<IReadOnlyList<ProjectSchedule>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ProjectSchedule>>([]);
    public Task AddAsync(ProjectSchedule entity, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
    public Task AddRangeAsync(IEnumerable<ProjectSchedule> entities, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
    public void Update(ProjectSchedule entity) { }
    public void Remove(ProjectSchedule entity) { }
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
}

internal sealed class MeasurementProjectFileRepositoryFake : IProjectFileRepository
{
    public string? RoleName { get; init; }
    public ProjectFileAccessReadModel? ProjectAccess { get; init; }
    public bool HasMeasurementScheduleLink { get; init; }
    public FileLink? ExistingAreaLink { get; init; }
    public MeasurementImageGalleryPageReadModel GalleryPage { get; init; } = new();
    public MeasurementImageGalleryQueryReadModel? LastGalleryQuery { get; private set; }

    public List<StoredFile> StoredFiles { get; } = [];
    public List<FileLink> FileLinks { get; } = [];

    public Task<ProjectFileAccessReadModel?> GetProjectAccessAsync(
        Guid projectId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(ProjectAccess);

    public Task<ProjectFileAccessReadModel?> GetReferenceProjectAccessAsync(
        string referenceType,
        Guid referenceId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(ProjectAccess);

    public Task<string?> GetAccountRoleNameAsync(
        Guid accountId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(RoleName);

    public Task AddFileLinkAsync(FileLink fileLink, CancellationToken cancellationToken = default)
    {
        FileLinks.Add(fileLink);
        return Task.CompletedTask;
    }

    public Task<FileMetadataReadModel?> GetFileMetadataAsync(
        Guid fileId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<FileMetadataReadModel?>(null);

    public Task<FileReferencePageReadModel> GetFilesByReferenceAsync(
        FileReferenceQueryReadModel query,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new FileReferencePageReadModel());

    public Task<FileLinkReadModel?> GetFileLinkAsync(
        Guid fileLinkId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<FileLinkReadModel?>(null);

    public Task<IReadOnlyList<FileLink>> GetFileLinkEntitiesByFileIdAsync(
        Guid fileId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<FileLink>>([]);

    public Task<FileLink?> GetFileLinkEntityAsync(
        string referenceType,
        Guid referenceId,
        Guid fileId,
        CancellationToken cancellationToken = default)
    {
        if (ExistingAreaLink is not null &&
            ExistingAreaLink.ReferenceType == referenceType &&
            ExistingAreaLink.ReferenceId == referenceId &&
            ExistingAreaLink.FileId == fileId)
        {
            return Task.FromResult<FileLink?>(ExistingAreaLink);
        }

        return Task.FromResult<FileLink?>(FileLinks.FirstOrDefault(link =>
            link.ReferenceType == referenceType &&
            link.ReferenceId == referenceId &&
            link.FileId == fileId));
    }

    public Task<IReadOnlyList<FileLink>> GetFileLinkEntitiesByReferenceAsync(
        string referenceType,
        Guid referenceId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<FileLink>>([]);

    public void RemoveFileLinks(IEnumerable<FileLink> fileLinks)
    {
        foreach (var link in fileLinks.ToList())
        {
            FileLinks.Remove(link);
        }
    }

    public Task<IReadOnlyList<CatalogFileReadModel>> GetCatalogFilesByReferencesAsync(
        string referenceType,
        IReadOnlyList<Guid> referenceIds,
        bool customerVisibleOnly,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<CatalogFileReadModel>>([]);

    public Task<int> CountProductPreviewFilesAsync(
        Guid productId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(0);

    public Task<IReadOnlyList<ProductPreviewImageReadModel>> GetProductPreviewFilesAsync(
        Guid productId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ProductPreviewImageReadModel>>([]);

    public Task<ProductPreviewImageReadModel?> GetProductPreviewFileAsync(
        Guid productId,
        Guid fileId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<ProductPreviewImageReadModel?>(null);

    public Task<IReadOnlyList<FileLink>> GetProductPreviewFileLinkEntitiesAsync(
        Guid productId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<FileLink>>([]);

    public Task<int> CountProductVersionPreviewFilesAsync(
        Guid productVersionId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(0);

    public Task<IReadOnlyList<FileLink>> GetProductVersionPreviewFileLinkEntitiesAsync(
        Guid productVersionId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<FileLink>>([]);

    public Task<ProjectFileSearchIndexItemReadModel?> GetSearchIndexItemAsync(
        Guid fileId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<ProjectFileSearchIndexItemReadModel?>(null);

    public Task<IReadOnlyList<ProjectFileSearchIndexItemReadModel>> GetSearchIndexPageAsync(
        int page,
        int limit,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ProjectFileSearchIndexItemReadModel>>([]);

    public Task<IReadOnlyList<ProjectFileSearchIndexItemReadModel>> SearchByProjectAsync(
        Guid projectId,
        string query,
        int page,
        int limit,
        bool customerVisibleOnly,
        Guid? customerAccountId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ProjectFileSearchIndexItemReadModel>>([]);

    public Task<int> CountSearchByProjectAsync(
        Guid projectId,
        string query,
        bool customerVisibleOnly,
        Guid? customerAccountId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(0);

    public Task<bool> HasProjectFileWithTypesAsync(
        Guid projectId,
        IReadOnlyCollection<FileType> fileTypes,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public Task<ProjectLinkedFileReadModel?> GetProjectLinkedActiveFileAsync(
        Guid projectId,
        Guid fileId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<ProjectLinkedFileReadModel?>(null);

    public Task<bool> ExistsByStoragePathAsync(
        string storagePath,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(StoredFiles.Any(file => file.StoragePath == storagePath));

    public Task<MeasurementImageGalleryPageReadModel> GetMeasurementImageGalleryAsync(
        MeasurementImageGalleryQueryReadModel query,
        CancellationToken cancellationToken = default)
    {
        LastGalleryQuery = query;
        return Task.FromResult(GalleryPage);
    }

    public Task<bool> HasMeasurementScheduleLinkInProjectAsync(
        Guid fileId,
        Guid projectId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(HasMeasurementScheduleLink);

    public IQueryable<StoredFile> Query() => Enumerable.Empty<StoredFile>().AsQueryable();
    public Task<StoredFile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult<StoredFile?>(null);
    public Task<IReadOnlyList<StoredFile>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<StoredFile>>([]);
    public Task AddAsync(StoredFile entity, CancellationToken cancellationToken = default)
    {
        StoredFiles.Add(entity);
        return Task.CompletedTask;
    }
    public Task AddRangeAsync(IEnumerable<StoredFile> entities, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
    public void Update(StoredFile entity) { }
    public void Remove(StoredFile entity) { }
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
}

internal sealed class MeasurementUnitOfWorkFake : IUnitOfWork
{
    public int SaveChangesCallCount { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCallCount++;
        return Task.FromResult(1);
    }

    public Task BeginTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task CommitTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RollbackTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
