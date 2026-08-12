#nullable enable

using System;
using System.Linq;
using System.Threading.Tasks;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.ReadModels.ProjectFiles;
using FurniSpace.Infrastructure.Repositories.Repository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FurniSpace.Infrastructure.Tests.Repositories;

public sealed class ProjectFileRepositoryTests
{
    [Fact]
    public async Task AccessQueries_ReturnProjectAccessForSupportedReferenceTypes()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new ProjectFileRepository(context);

        var projectAccess = await repository.GetProjectAccessAsync(data.ProjectId);
        var scheduleAccess = await repository.GetReferenceProjectAccessAsync(" project_schedule ", data.ScheduleId);
        var proposalAccess = await repository.GetReferenceProjectAccessAsync("PROPOSAL", data.ProposalId);
        var quotationAccess = await repository.GetReferenceProjectAccessAsync("QUOTATION", data.QuotationId);
        var orderAccess = await repository.GetReferenceProjectAccessAsync("ORDER", data.OrderId);
        var unknownAccess = await repository.GetReferenceProjectAccessAsync("UNKNOWN", Guid.NewGuid());
        var role = await repository.GetAccountRoleNameAsync(data.CustomerId);

        Assert.Equal(data.ProjectId, projectAccess?.ProjectId);
        Assert.Equal(data.ProjectId, scheduleAccess?.ProjectId);
        Assert.Equal(data.ProjectId, proposalAccess?.ProjectId);
        Assert.Equal(data.ProjectId, quotationAccess?.ProjectId);
        Assert.Equal(data.ProjectId, orderAccess?.ProjectId);
        Assert.Null(unknownAccess);
        Assert.Equal("CUSTOMER", role);
    }

    [Fact]
    public async Task FileLinks_ReturnLinkedReadModelsAndCanRemoveLinks()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new ProjectFileRepository(context);

        var fileLink = await repository.GetFileLinkAsync(data.FileLinkId);
        var fileLinks = await repository.GetFileLinkEntitiesByFileIdAsync(data.FileId);

        Assert.NotNull(fileLink);
        Assert.Equal(data.ProjectId, fileLink.ProjectAccess?.ProjectId);
        Assert.Single(fileLinks);

        repository.RemoveFileLinks(fileLinks);
        await context.SaveChangesAsync();

        Assert.Empty(await repository.GetFileLinkEntitiesByFileIdAsync(data.FileId));
    }

    [Fact]
    public async Task ReferenceCatalogPreviewAndSearchQueries_ReturnExpectedFiles()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new ProjectFileRepository(context);

        var referencePage = await repository.GetFilesByReferenceAsync(new FileReferenceQueryReadModel
        {
            ReferenceType = " project ",
            ReferenceId = data.ProjectId,
            CustomerVisibleOnly = true,
            CustomerAccountId = data.CustomerId,
            Page = 1,
            Limit = 5
        });
        var catalogFiles = await repository.GetCatalogFilesByReferencesAsync(" project ", [data.ProjectId], customerVisibleOnly: true);
        var noCatalogFiles = await repository.GetCatalogFilesByReferencesAsync("PROJECT", [], customerVisibleOnly: false);
        var previewCount = await repository.CountProductPreviewFilesAsync(data.ProductId);
        var previews = await repository.GetProductPreviewFilesAsync(data.ProductId);
        var preview = await repository.GetProductPreviewFileAsync(data.ProductId, data.ProductPreviewFileId);
        var previewLinks = await repository.GetProductPreviewFileLinkEntitiesAsync(data.ProductId);
        var versionPreviewCount = await repository.CountProductVersionPreviewFilesAsync(data.ProductVersionId);
        var versionPreviewLinks = await repository.GetProductVersionPreviewFileLinkEntitiesAsync(data.ProductVersionId);
        var searchItem = await repository.GetSearchIndexItemAsync(data.FileId);
        var searchPage = await repository.GetSearchIndexPageAsync(page: 1, limit: 10);
        var hasMeasurements = await repository.HasProjectFileWithTypesAsync(data.ProjectId, [FileType.FLOOR_PLAN, FileType.MEASUREMENT_REPORT]);

        Assert.Equal(2, referencePage.Total);
        Assert.Equal(2, referencePage.Items.Count);
        Assert.Single(catalogFiles);
        Assert.Empty(noCatalogFiles);
        Assert.Equal(1, previewCount);
        Assert.Single(previews);
        Assert.NotNull(preview);
        Assert.Single(previewLinks);
        Assert.Equal(1, versionPreviewCount);
        Assert.Single(versionPreviewLinks);
        Assert.NotNull(searchItem);
        Assert.Equal(data.ProjectId, searchItem.ProjectId);
        Assert.Contains(searchPage, item => item.FileId == data.FileId);
        Assert.True(hasMeasurements);
    }

    [Fact]
    public async Task AddFileLinkAsync_AddsEntity()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new ProjectFileRepository(context);
        var link = new FileLink
        {
            FileLinkId = Guid.NewGuid(),
            FileId = data.UnlinkedFileId,
            ReferenceType = "PROJECT",
            ReferenceId = data.ProjectId,
            FileType = FileType.OTHER,
            Visibility = FileVisibility.PRIVATE
        };

        await repository.AddFileLinkAsync(link);
        await context.SaveChangesAsync();

        Assert.NotNull(await repository.GetFileLinkAsync(link.FileLinkId));
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<SeededData> SeedAsync(AppDbContext context)
    {
        var customerRole = new Role { RoleId = Guid.NewGuid(), RoleName = "CUSTOMER" };
        var customerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var scheduleId = Guid.NewGuid();
        var proposalId = Guid.NewGuid();
        var quotationId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var privateFileId = Guid.NewGuid();
        var unlinkedFileId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var productVersionId = Guid.NewGuid();
        var productPreviewFileId = Guid.NewGuid();
        var versionPreviewFileId = Guid.NewGuid();
        var fileLinkId = Guid.NewGuid();

        context.RoleSet.Add(customerRole);
        context.AccountSet.Add(new Account
        {
            AccountId = customerId,
            RoleId = customerRole.RoleId,
            Email = "customer@example.com",
            FullName = "Customer",
            PasswordHash = "hash",
            Status = AccountStatus.ACTIVE
        });
        context.ProjectSet.Add(new Project
        {
            ProjectId = projectId,
            CustomerId = customerId,
            ProjectName = "Kitchen",
            Status = ProjectStatus.SUBMITTED
        });
        context.ProjectScheduleSet.Add(new ProjectSchedule
        {
            ScheduleId = scheduleId,
            ProjectId = projectId,
            ScheduledStart = DateTime.UtcNow
        });
        context.ProposalSet.Add(new Proposal
        {
            ProposalId = proposalId,
            ProjectId = projectId,
            ProposalName = "Proposal"
        });
        context.QuotationSet.Add(new Quotation
        {
            QuotationId = quotationId,
            ProjectId = projectId,
            ProposalId = proposalId,
            QuotationCode = "Q-001",
            SubtotalAmount = 100m,
            TotalDiscountAmount = 0m,
            PreVatAmount = 100m,
            VatRate = 0.08m,
            VatAmount = 8m,
            TotalAmount = 108m,
            DepositAmount = 32m,
            Currency = "VND"
        });
        context.OrderSet.Add(new Order
        {
            OrderId = orderId,
            ProjectId = projectId,
            QuotationId = quotationId,
            OrderCode = "O-001",
            CustomerId = customerId,
            OriginalTotalAmount = 100,
            FinalTotalAmount = 100
        });
        context.StoredFileSet.AddRange(
            CreateFile(fileId, customerId, "floor-plan.pdf", FileStatus.ACTIVE, DateTime.UtcNow.AddMinutes(-5)),
            CreateFile(privateFileId, customerId, "private-note.pdf", FileStatus.ACTIVE, DateTime.UtcNow.AddMinutes(-4)),
            CreateFile(unlinkedFileId, customerId, "loose.pdf", FileStatus.ACTIVE, DateTime.UtcNow.AddMinutes(-3)),
            CreateFile(productPreviewFileId, customerId, "preview.jpg", FileStatus.ACTIVE, DateTime.UtcNow.AddMinutes(-2)),
            CreateFile(versionPreviewFileId, customerId, "version-preview.jpg", FileStatus.ACTIVE, DateTime.UtcNow.AddMinutes(-1)));
        context.FileLinkSet.AddRange(
            new FileLink
            {
                FileLinkId = fileLinkId,
                FileId = fileId,
                ReferenceType = "PROJECT",
                ReferenceId = projectId,
                FileType = FileType.FLOOR_PLAN,
                Visibility = FileVisibility.CUSTOMER_VISIBLE,
                CreatedBy = customerId,
                CreatedAt = DateTime.UtcNow
            },
            new FileLink
            {
                FileLinkId = Guid.NewGuid(),
                FileId = privateFileId,
                ReferenceType = "PROJECT",
                ReferenceId = projectId,
                FileType = FileType.MEASUREMENT_REPORT,
                Visibility = FileVisibility.PRIVATE,
                CreatedBy = customerId,
                CreatedAt = DateTime.UtcNow
            },
            new FileLink
            {
                FileLinkId = Guid.NewGuid(),
                FileId = productPreviewFileId,
                ReferenceType = "PRODUCT",
                ReferenceId = productId,
                FileType = FileType.PRODUCT_PREVIEW,
                Visibility = FileVisibility.CUSTOMER_VISIBLE,
                DisplayOrder = 1,
                CreatedAt = DateTime.UtcNow
            },
            new FileLink
            {
                FileLinkId = Guid.NewGuid(),
                FileId = versionPreviewFileId,
                ReferenceType = "PRODUCT_VERSION",
                ReferenceId = productVersionId,
                FileType = FileType.PRODUCT_PREVIEW,
                Visibility = FileVisibility.CUSTOMER_VISIBLE,
                DisplayOrder = 1,
                CreatedAt = DateTime.UtcNow
            });

        await context.SaveChangesAsync();
        return new SeededData(
            customerId,
            projectId,
            scheduleId,
            proposalId,
            quotationId,
            orderId,
            fileId,
            privateFileId,
            unlinkedFileId,
            productId,
            productVersionId,
            productPreviewFileId,
            versionPreviewFileId,
            fileLinkId);
    }

    private static StoredFile CreateFile(
        Guid fileId,
        Guid uploadedBy,
        string name,
        FileStatus status,
        DateTime uploadedAt)
    {
        return new StoredFile
        {
            FileId = fileId,
            UploadedBy = uploadedBy,
            OriginalFileName = name,
            StoredFileName = name,
            FileUrl = $"https://cdn.example.com/{name}",
            StoragePath = $"files/{name}",
            MimeType = "application/pdf",
            FileSizeBytes = 123,
            Status = status,
            UploadedAt = uploadedAt
        };
    }

    private sealed record SeededData(
        Guid CustomerId,
        Guid ProjectId,
        Guid ScheduleId,
        Guid ProposalId,
        Guid QuotationId,
        Guid OrderId,
        Guid FileId,
        Guid PrivateFileId,
        Guid UnlinkedFileId,
        Guid ProductId,
        Guid ProductVersionId,
        Guid ProductPreviewFileId,
        Guid VersionPreviewFileId,
        Guid FileLinkId);
}
