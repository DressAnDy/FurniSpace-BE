#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.Common.ProjectShowcases;
using FurniSpace.Application.DTOs.ProjectReviews;
using FurniSpace.Application.DTOs.ProjectShowcases;
using FurniSpace.Application.Services.ProjectReviews;
using FurniSpace.Application.Services.ProjectShowcases;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Common.Storage;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.Interfaces;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Infrastructure.Repositories.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using Xunit;

namespace FurniSpace.Application.Tests.ProjectShowcases;

public sealed class ProjectShowcaseServiceTests
{
    private static readonly Guid AdminId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid CustomerId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid SalesId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid DesignerId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

    static ProjectShowcaseServiceTests()
    {
        MapsterTestSetup.EnsureConfigured();
    }

    [Fact]
    public async Task CreateAsync_AssignedSalesCreatesDraftShowcase()
    {
        await using var context = CreateContext();
        var project = await SeedProjectAsync(context, ProjectStatus.COMPLETED);
        var service = CreateShowcaseService(context);

        var result = await service.CreateAsync(
            project.ProjectId,
            SalesId,
            new CreateProjectShowcaseRequestDto
            {
                Title = "Cafe Renovation",
                Summary = "Before and after transformation"
            });

        Assert.Equal(201, result.Status);
        Assert.Equal(ProjectShowcaseStatus.DRAFT, result.Data!.Status);
        Assert.Equal("cafe-renovation", result.Data.Slug);
    }

    [Fact]
    public async Task CreateAsync_ReturnsConflict_WhenShowcaseAlreadyExists()
    {
        await using var context = CreateContext();
        var project = await SeedProjectAsync(context, ProjectStatus.COMPLETED);
        var service = CreateShowcaseService(context);
        await service.CreateAsync(project.ProjectId, SalesId, null);

        var result = await service.CreateAsync(project.ProjectId, SalesId, null);

        Assert.Equal(409, result.Status);
        Assert.Equal(ProjectShowcaseErrorCodes.AlreadyExists, result.ErrorCode);
    }

    [Fact]
    public async Task PublishAsync_ReturnsForbidden_WhenDesignerAttemptsPublish()
    {
        await using var context = CreateContext();
        var project = await SeedProjectAsync(context, ProjectStatus.COMPLETED);
        var service = CreateShowcaseService(context);
        var showcase = await CreateReadyForPublishShowcaseAsync(context, service, project.ProjectId);
        showcase.Status = ProjectShowcaseStatus.PENDING_REVIEW;
        await context.SaveChangesAsync();

        var result = await service.PublishAsync(showcase.ProjectShowcaseId, DesignerId);

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task PublishAsync_ReturnsBadRequest_WhenProjectIsNotCompleted()
    {
        await using var context = CreateContext();
        var project = await SeedProjectAsync(context, ProjectStatus.DELIVERED);
        var service = CreateShowcaseService(context);
        var showcase = await CreateReadyForPublishShowcaseAsync(context, service, project.ProjectId);
        showcase.Status = ProjectShowcaseStatus.PENDING_REVIEW;
        await context.SaveChangesAsync();

        var result = await service.PublishAsync(showcase.ProjectShowcaseId, AdminId);

        Assert.Equal(400, result.Status);
        Assert.Equal(ProjectShowcaseErrorCodes.ProjectNotCompleted, result.ErrorCode);
    }

    [Fact]
    public async Task PublishAsync_AdminPublishesCompletedProjectShowcase()
    {
        await using var context = CreateContext();
        var project = await SeedProjectAsync(context, ProjectStatus.COMPLETED);
        var service = CreateShowcaseService(context);
        var showcase = await CreateReadyForPublishShowcaseAsync(context, service, project.ProjectId);
        showcase.Status = ProjectShowcaseStatus.PENDING_REVIEW;
        await context.SaveChangesAsync();

        var result = await service.PublishAsync(showcase.ProjectShowcaseId, AdminId);

        Assert.Equal(200, result.Status);
        Assert.Equal(ProjectShowcaseStatus.PUBLISHED, result.Data!.Status);
        Assert.NotNull(result.Data.PublishedAt);
    }

    [Fact]
    public async Task UpdateAsync_WhenArchived_ReturnsBadRequest()
    {
        await using var context = CreateContext();
        var project = await SeedProjectAsync(context, ProjectStatus.COMPLETED);
        var service = CreateShowcaseService(context);
        var createResult = await service.CreateAsync(project.ProjectId, SalesId, null);
        var showcase = await context.ProjectShowcaseSet.SingleAsync(
            entity => entity.ProjectShowcaseId == createResult.Data!.ProjectShowcaseId);
        showcase.Status = ProjectShowcaseStatus.ARCHIVED;
        await context.SaveChangesAsync();

        var result = await service.UpdateAsync(
            showcase.ProjectShowcaseId,
            SalesId,
            new UpdateProjectShowcaseRequestDto { Title = "Updated title" });

        Assert.Equal(400, result.Status);
        Assert.Equal(ProjectShowcaseErrorCodes.ArchivedReadOnly, result.ErrorCode);
    }

    [Fact]
    public async Task AddMediaAsync_DesignerCanAttachMediaToAssignedProject()
    {
        await using var context = CreateContext();
        var project = await SeedProjectAsync(context, ProjectStatus.COMPLETED);
        var file = await SeedProjectFileAsync(context, project.ProjectId, SalesId);
        var service = CreateShowcaseService(context);
        var createResult = await service.CreateAsync(project.ProjectId, SalesId, null);

        var result = await service.AddMediaAsync(
            createResult.Data!.ProjectShowcaseId,
            DesignerId,
            new AddProjectShowcaseMediaRequestDto
            {
                FileId = file.FileId,
                MediaType = ProjectShowcaseMediaType.FINAL,
                SetAsCover = true
            });

        Assert.Equal(201, result.Status);
        Assert.True(result.Data!.IsCover);
    }

    [Fact]
    public async Task SetCoverAsync_EnforcesSingleCover()
    {
        await using var context = CreateContext();
        var project = await SeedProjectAsync(context, ProjectStatus.COMPLETED);
        var firstFile = await SeedProjectFileAsync(context, project.ProjectId, SalesId);
        var secondFile = await SeedProjectFileAsync(context, project.ProjectId, SalesId);
        var service = CreateShowcaseService(context);
        var createResult = await service.CreateAsync(project.ProjectId, SalesId, null);
        var showcaseId = createResult.Data!.ProjectShowcaseId;

        await service.AddMediaAsync(
            showcaseId,
            SalesId,
            new AddProjectShowcaseMediaRequestDto
            {
                FileId = firstFile.FileId,
                MediaType = ProjectShowcaseMediaType.BEFORE,
                SetAsCover = true
            });
        await service.AddMediaAsync(
            showcaseId,
            SalesId,
            new AddProjectShowcaseMediaRequestDto
            {
                FileId = secondFile.FileId,
                MediaType = ProjectShowcaseMediaType.AFTER
            });
        var coverResult = await service.SetCoverAsync(
            showcaseId,
            (await context.ProjectShowcaseMediaSet.FirstAsync(item => item.FileId == secondFile.FileId)).ProjectShowcaseMediaId,
            SalesId);

        var covers = await context.ProjectShowcaseMediaSet.Where(item => item.IsCover).ToListAsync();

        Assert.Equal(200, coverResult.Status);
        Assert.Single(covers);
        Assert.Equal(secondFile.FileId, covers[0].FileId);
    }

    [Fact]
    public async Task RemoveMediaAsync_DoesNotDeleteSourceFile()
    {
        await using var context = CreateContext();
        var project = await SeedProjectAsync(context, ProjectStatus.COMPLETED);
        var file = await SeedProjectFileAsync(context, project.ProjectId, SalesId);
        var service = CreateShowcaseService(context);
        var createResult = await service.CreateAsync(project.ProjectId, SalesId, null);
        var mediaResult = await service.AddMediaAsync(
            createResult.Data!.ProjectShowcaseId,
            SalesId,
            new AddProjectShowcaseMediaRequestDto
            {
                FileId = file.FileId,
                MediaType = ProjectShowcaseMediaType.AFTER
            });

        var removeResult = await service.RemoveMediaAsync(
            createResult.Data.ProjectShowcaseId,
            mediaResult.Data!.ProjectShowcaseMediaId,
            SalesId);

        Assert.Equal(200, removeResult.Status);
        Assert.Empty(await context.ProjectShowcaseMediaSet.ToListAsync());
        Assert.NotNull(await context.StoredFileSet.FindAsync(file.FileId));
    }

    [Fact]
    public async Task UploadMediaAsync_CreatesStoredFileFileLinkAndShowcaseMedia()
    {
        await using var context = CreateContext();
        var project = await SeedProjectAsync(context, ProjectStatus.COMPLETED);
        var storage = new ShowcaseFileStorageFake();
        var service = CreateShowcaseService(context, storage);
        var createResult = await service.CreateAsync(project.ProjectId, SalesId, null);

        await using var content = new MemoryStream([0xFF, 0xD8, 0xFF]);
        var result = await service.UploadMediaAsync(
            createResult.Data!.ProjectShowcaseId,
            SalesId,
            new UploadProjectShowcaseMediaRequestDto
            {
                Content = content,
                OriginalFileName = "showcase.jpg",
                ContentType = "image/jpeg",
                FileSizeBytes = content.Length,
                MediaType = ProjectShowcaseMediaType.FINAL,
                SetAsCover = true
            });

        Assert.Equal(201, result.Status);
        Assert.True(result.Data!.IsCover);
        Assert.Single(await context.StoredFileSet.ToListAsync());
        Assert.Single(await context.FileLinkSet.Where(link => link.FileType == FileType.PORTFOLIO_IMAGE).ToListAsync());
        Assert.Single(await context.ProjectShowcaseMediaSet.ToListAsync());
        Assert.NotNull(storage.UploadRequest);
    }

    [Fact]
    public async Task UploadMediaAsync_InvalidMime_ReturnsBadRequest()
    {
        await using var context = CreateContext();
        var project = await SeedProjectAsync(context, ProjectStatus.COMPLETED);
        var service = CreateShowcaseService(context, new ShowcaseFileStorageFake());
        var createResult = await service.CreateAsync(project.ProjectId, SalesId, null);

        await using var content = new MemoryStream([0x25, 0x50, 0x44, 0x46]);
        var result = await service.UploadMediaAsync(
            createResult.Data!.ProjectShowcaseId,
            SalesId,
            new UploadProjectShowcaseMediaRequestDto
            {
                Content = content,
                OriginalFileName = "showcase.pdf",
                ContentType = "application/pdf",
                FileSizeBytes = content.Length
            });

        Assert.Equal(400, result.Status);
        Assert.Empty(await context.StoredFileSet.ToListAsync());
        Assert.Empty(await context.ProjectShowcaseMediaSet.ToListAsync());
    }

    [Fact]
    public async Task RemoveMediaAsync_DeleteCover_PromotesNextMediaByDisplayOrder()
    {
        await using var context = CreateContext();
        var project = await SeedProjectAsync(context, ProjectStatus.COMPLETED);
        var firstFile = await SeedProjectFileAsync(context, project.ProjectId, SalesId);
        var secondFile = await SeedProjectFileAsync(context, project.ProjectId, SalesId);
        var service = CreateShowcaseService(context);
        var createResult = await service.CreateAsync(project.ProjectId, SalesId, null);
        var showcaseId = createResult.Data!.ProjectShowcaseId;

        await service.AddMediaAsync(
            showcaseId,
            SalesId,
            new AddProjectShowcaseMediaRequestDto
            {
                FileId = firstFile.FileId,
                MediaType = ProjectShowcaseMediaType.BEFORE,
                SetAsCover = true
            });
        var secondMedia = await service.AddMediaAsync(
            showcaseId,
            SalesId,
            new AddProjectShowcaseMediaRequestDto
            {
                FileId = secondFile.FileId,
                MediaType = ProjectShowcaseMediaType.AFTER
            });

        var coverMediaId = (await context.ProjectShowcaseMediaSet.FirstAsync(item => item.IsCover)).ProjectShowcaseMediaId;
        var removeResult = await service.RemoveMediaAsync(showcaseId, coverMediaId, SalesId);

        Assert.Equal(200, removeResult.Status);
        var remainingCover = await context.ProjectShowcaseMediaSet.SingleAsync(item => item.IsCover);
        Assert.Equal(secondMedia.Data!.ProjectShowcaseMediaId, remainingCover.ProjectShowcaseMediaId);
    }

    [Fact]
    public async Task RemoveMediaAsync_DeleteLastMedia_LeavesNoCover()
    {
        await using var context = CreateContext();
        var project = await SeedProjectAsync(context, ProjectStatus.COMPLETED);
        var file = await SeedProjectFileAsync(context, project.ProjectId, SalesId);
        var service = CreateShowcaseService(context);
        var createResult = await service.CreateAsync(project.ProjectId, SalesId, null);
        var mediaResult = await service.AddMediaAsync(
            createResult.Data!.ProjectShowcaseId,
            SalesId,
            new AddProjectShowcaseMediaRequestDto
            {
                FileId = file.FileId,
                MediaType = ProjectShowcaseMediaType.FINAL,
                SetAsCover = true
            });

        var removeResult = await service.RemoveMediaAsync(
            createResult.Data.ProjectShowcaseId,
            mediaResult.Data!.ProjectShowcaseMediaId,
            SalesId);

        Assert.Equal(200, removeResult.Status);
        Assert.Empty(await context.ProjectShowcaseMediaSet.ToListAsync());
        Assert.False(await context.ProjectShowcaseMediaSet.AnyAsync(item => item.IsCover));
    }

    [Fact]
    public async Task UpdateAsync_RejectsFeaturedReviewFromDifferentProject()
    {
        await using var context = CreateContext();
        var project = await SeedProjectAsync(context, ProjectStatus.COMPLETED);
        var otherProject = await SeedProjectAsync(context, ProjectStatus.COMPLETED, "Other Project");
        var review = await SeedReviewAsync(context, otherProject.ProjectId, CustomerId);
        var service = CreateShowcaseService(context);
        var createResult = await service.CreateAsync(project.ProjectId, SalesId, null);

        var result = await service.UpdateAsync(
            createResult.Data!.ProjectShowcaseId,
            SalesId,
            new UpdateProjectShowcaseRequestDto { FeaturedReviewId = review.ReviewId });

        Assert.Equal(400, result.Status);
        Assert.Equal(ProjectShowcaseErrorCodes.FeaturedReviewInvalid, result.ErrorCode);
    }

    [Fact]
    public async Task GetPublicBySlugAsync_HidesReviewWithoutConsent()
    {
        await using var context = CreateContext();
        var project = await SeedProjectAsync(context, ProjectStatus.COMPLETED);
        var review = await SeedReviewAsync(context, project.ProjectId, CustomerId);
        var service = CreateShowcaseService(context);
        var showcase = await CreateReadyForPublishShowcaseAsync(context, service, project.ProjectId);
        showcase.FeaturedReviewId = review.ReviewId;
        showcase.Status = ProjectShowcaseStatus.PENDING_REVIEW;
        await context.SaveChangesAsync();
        await service.PublishAsync(showcase.ProjectShowcaseId, AdminId);

        var withoutConsent = await service.GetPublicBySlugAsync(showcase.Slug);
        review.AllowPublicDisplay = true;
        review.PublicDisplayConsentAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        var withConsent = await service.GetPublicBySlugAsync(showcase.Slug);

        Assert.Null(withoutConsent.Data!.Review);
        Assert.NotNull(withConsent.Data!.Review);
    }

    [Fact]
    public async Task GetPublicListAsync_ReturnsOnlyPublishedShowcases()
    {
        await using var context = CreateContext();
        var project = await SeedProjectAsync(context, ProjectStatus.COMPLETED);
        var service = CreateShowcaseService(context);
        var draftResult = await service.CreateAsync(
            project.ProjectId,
            SalesId,
            new CreateProjectShowcaseRequestDto { Title = "Draft Case Study" });
        var publishedProject = await SeedProjectAsync(context, ProjectStatus.COMPLETED, "Published Project");
        var published = await CreateReadyForPublishShowcaseAsync(
            context,
            service,
            publishedProject.ProjectId,
            "Published Case Study");
        published.Status = ProjectShowcaseStatus.PENDING_REVIEW;
        await context.SaveChangesAsync();
        await service.PublishAsync(published.ProjectShowcaseId, AdminId);

        var result = await service.GetPublicListAsync(new PublicShowcaseQueryDto());

        Assert.Equal(200, result.Status);
        Assert.Single(result.Data!.Items);
        Assert.Equal(published.ProjectShowcaseId, result.Data.Items[0].ProjectShowcaseId);
        Assert.DoesNotContain(result.Data.Items, item => item.ProjectShowcaseId == draftResult.Data!.ProjectShowcaseId);
    }

    [Fact]
    public async Task GetPublicListAsync_ProjectsCompletedDateAndTotalAreaSqm()
    {
        await using var context = CreateContext();
        var project = await SeedProjectAsync(context, ProjectStatus.COMPLETED);
        var service = CreateShowcaseService(context);
        var showcase = await CreateReadyForPublishShowcaseAsync(context, service, project.ProjectId);
        showcase.Status = ProjectShowcaseStatus.PENDING_REVIEW;
        await context.SaveChangesAsync();
        await service.PublishAsync(showcase.ProjectShowcaseId, AdminId);

        var result = await service.GetPublicListAsync(new PublicShowcaseQueryDto());

        var item = Assert.Single(result.Data!.Items);
        Assert.Equal(new DateOnly(2026, 8, 20), item.CompletedDate);
        Assert.Equal(120.5m, item.TotalAreaSqm);
        Assert.Equal("Cafe", item.BusinessType);
        Assert.NotNull(item.CoverUrl);
    }

    [Fact]
    public async Task GetPublicBySlugAsync_ProjectsProjectDetailsAndCoverUrl()
    {
        await using var context = CreateContext();
        var project = await SeedProjectAsync(context, ProjectStatus.COMPLETED);
        var service = CreateShowcaseService(context);
        var showcase = await CreateReadyForPublishShowcaseAsync(context, service, project.ProjectId);
        showcase.Description = "Full project overview.";
        showcase.Status = ProjectShowcaseStatus.PENDING_REVIEW;
        await context.SaveChangesAsync();
        await service.PublishAsync(showcase.ProjectShowcaseId, AdminId);

        var result = await service.GetPublicBySlugAsync(showcase.Slug);

        Assert.Equal(200, result.Status);
        var detail = result.Data!;
        Assert.Equal(new DateOnly(2026, 8, 20), detail.CompletedDate);
        Assert.Equal(120.5m, detail.TotalAreaSqm);
        Assert.Equal(2, detail.NumberOfFloors);
        Assert.Equal("District 7, Ho Chi Minh City", detail.ProjectAddress);
        Assert.Equal(2026, detail.CompletionYear);
        Assert.Equal(80, detail.ImplementationDurationDays);
        Assert.Equal("Full project overview.", detail.Description);
        Assert.NotNull(detail.CoverUrl);
        Assert.True(detail.Media.Any(item => item.IsCover));
    }

    [Fact]
    public async Task GetPublicBySlugAsync_WhenSubmittedAtNull_ImplementationDurationDaysIsNull()
    {
        await using var context = CreateContext();
        var project = await SeedProjectAsync(context, ProjectStatus.COMPLETED);
        project.SubmittedAt = null;
        await context.SaveChangesAsync();
        var service = CreateShowcaseService(context);
        var showcase = await CreateReadyForPublishShowcaseAsync(context, service, project.ProjectId);
        showcase.Status = ProjectShowcaseStatus.PENDING_REVIEW;
        await context.SaveChangesAsync();
        await service.PublishAsync(showcase.ProjectShowcaseId, AdminId);

        var result = await service.GetPublicBySlugAsync(showcase.Slug);

        Assert.Null(result.Data!.ImplementationDurationDays);
        Assert.Equal(new DateOnly(2026, 8, 20), result.Data.CompletedDate);
    }

    [Fact]
    public async Task UpdatePublicConsentAsync_CustomerCanGrantAndRevokeConsent()
    {
        await using var context = CreateContext();
        var project = await SeedProjectAsync(context, ProjectStatus.COMPLETED);
        var review = await SeedReviewAsync(context, project.ProjectId, CustomerId);
        var consentService = CreateConsentService(context);

        var grantResult = await consentService.UpdatePublicConsentAsync(
            review.ReviewId,
            CustomerId,
            new UpdateProjectReviewPublicConsentRequestDto { AllowPublicDisplay = true });
        var revokeResult = await consentService.UpdatePublicConsentAsync(
            review.ReviewId,
            CustomerId,
            new UpdateProjectReviewPublicConsentRequestDto { AllowPublicDisplay = false });

        Assert.True(grantResult.Data!.AllowPublicDisplay);
        Assert.NotNull(grantResult.Data.PublicDisplayConsentAt);
        Assert.False(revokeResult.Data!.AllowPublicDisplay);
        Assert.Null(revokeResult.Data.PublicDisplayConsentAt);
    }

    private static async Task<ProjectShowcase> CreateReadyForPublishShowcaseAsync(
        AppDbContext context,
        ProjectShowcaseService service,
        Guid projectId,
        string title = "Published Title")
    {
        var createResult = await service.CreateAsync(
            projectId,
            SalesId,
            new CreateProjectShowcaseRequestDto
            {
                Title = title,
                Summary = "Published summary"
            });
        var file = await SeedProjectFileAsync(context, projectId, SalesId);
        await service.AddMediaAsync(
            createResult.Data!.ProjectShowcaseId,
            SalesId,
            new AddProjectShowcaseMediaRequestDto
            {
                FileId = file.FileId,
                MediaType = ProjectShowcaseMediaType.FINAL,
                SetAsCover = true
            });

        return (await context.ProjectShowcaseSet.FirstAsync(item => item.ProjectShowcaseId == createResult.Data.ProjectShowcaseId))!;
    }

    private static ProjectShowcaseService CreateShowcaseService(
        AppDbContext context,
        IFileStorageService? storage = null)
    {
        var uploadSettings = Options.Create(new FileUploadSettings());
        var firebaseSettings = Options.Create(new FirebaseStorageSettings());
        var dependencies = new ProjectShowcaseServiceDependencies(
            storage ?? new ShowcaseFileStorageFake(),
            uploadSettings,
            firebaseSettings);

        return new ProjectShowcaseService(
            new ProjectRepository(context),
            new ProjectShowcaseRepository(context),
            new ProjectReviewRepository(context),
            new ProjectFileRepository(context),
            new UnitOfWork(context),
            dependencies);
    }

    private static ProjectReviewConsentService CreateConsentService(AppDbContext context)
    {
        return new ProjectReviewConsentService(
            new ProjectReviewRepository(context),
            new ProjectRepository(context),
            new UnitOfWork(context));
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<Project> SeedProjectAsync(
        AppDbContext context,
        ProjectStatus status,
        string projectName = "Showcase Project")
    {
        await EnsureAccountsAsync(context);

        var project = new Project
        {
            ProjectId = Guid.NewGuid(),
            CustomerId = CustomerId,
            AssignedSalesId = SalesId,
            AssignedDesignerId = DesignerId,
            ProjectName = projectName,
            BusinessType = "Cafe",
            Status = status
        };

        if (status == ProjectStatus.COMPLETED)
        {
            project.SubmittedAt = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
            project.CompletedAt = new DateTime(2026, 8, 20, 0, 0, 0, DateTimeKind.Utc);
            project.TotalAreaSqm = 120.5m;
            project.NumberOfFloors = 2;
            project.ProjectAddress = "District 7, Ho Chi Minh City";
        }

        context.ProjectSet.Add(project);
        await context.SaveChangesAsync();
        return project;
    }

    private static async Task EnsureAccountsAsync(AppDbContext context)
    {
        if (await context.AccountSet.AnyAsync(account => account.AccountId == AdminId))
        {
            return;
        }

        var roleIds = new Dictionary<string, Guid>
        {
            ["ADMIN"] = Guid.NewGuid(),
            ["CUSTOMER"] = Guid.NewGuid(),
            ["SALES"] = Guid.NewGuid(),
            ["DESIGNER"] = Guid.NewGuid()
        };
        context.RoleSet.AddRange(roleIds.Select(role => new Role { RoleId = role.Value, RoleName = role.Key }));
        context.AccountSet.AddRange(
            CreateAccount(AdminId, roleIds["ADMIN"], "admin@furnispace.local"),
            CreateAccount(CustomerId, roleIds["CUSTOMER"], "customer@furnispace.local"),
            CreateAccount(SalesId, roleIds["SALES"], "sales@furnispace.local"),
            CreateAccount(DesignerId, roleIds["DESIGNER"], "designer@furnispace.local"));
        await context.SaveChangesAsync();
    }

    private static Account CreateAccount(Guid accountId, Guid roleId, string email)
    {
        return new Account
        {
            AccountId = accountId,
            RoleId = roleId,
            Email = email,
            PasswordHash = "hash",
            FullName = email,
            Status = AccountStatus.ACTIVE
        };
    }

    private static async Task<StoredFile> SeedProjectFileAsync(AppDbContext context, Guid projectId, Guid uploadedBy)
    {
        var fileId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var file = new StoredFile
        {
            FileId = fileId,
            UploadedBy = uploadedBy,
            OriginalFileName = "final.jpg",
            StoredFileName = "final.jpg",
            FileUrl = "https://cdn.example/final.jpg",
            StoragePath = $"projects/{projectId}/files/{fileId}/final.jpg",
            MimeType = "image/jpeg",
            FileExtension = ".jpg",
            FileSizeBytes = 1024,
            Status = FileStatus.ACTIVE,
            UploadedAt = now
        };
        var link = new FileLink
        {
            FileLinkId = Guid.NewGuid(),
            FileId = fileId,
            ReferenceType = "PROJECT",
            ReferenceId = projectId,
            FileType = FileType.PORTFOLIO_IMAGE,
            Visibility = FileVisibility.STAFF_ONLY,
            CreatedBy = uploadedBy,
            CreatedAt = now
        };
        context.StoredFileSet.Add(file);
        context.FileLinkSet.Add(link);
        await context.SaveChangesAsync();
        return file;
    }

    private static async Task<ProjectReview> SeedReviewAsync(AppDbContext context, Guid projectId, Guid customerId)
    {
        var review = new ProjectReview
        {
            ReviewId = Guid.NewGuid(),
            ProjectId = projectId,
            CustomerId = customerId,
            Rating = 5,
            Comment = "Great service",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.ProjectReviewSet.Add(review);
        await context.SaveChangesAsync();
        return review;
    }
}

internal sealed class ShowcaseFileStorageFake : IFileStorageService
{
    public StorageUploadRequest? UploadRequest { get; private set; }

    public string? DeletedObjectName { get; private set; }

    public Task<StorageUploadResult> UploadAsync(
        StorageUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        UploadRequest = request;
        return Task.FromResult(new StorageUploadResult
        {
            ObjectName = request.ObjectName,
            PublicUrl = $"https://storage.example.com/{request.ObjectName}",
            Bucket = "test-bucket"
        });
    }

    public Task DeleteAsync(string objectName, CancellationToken cancellationToken = default)
    {
        DeletedObjectName = objectName;
        return Task.CompletedTask;
    }
}
