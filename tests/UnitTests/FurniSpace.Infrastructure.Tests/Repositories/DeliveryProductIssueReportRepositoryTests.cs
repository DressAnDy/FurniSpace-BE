#nullable enable

using System;
using System.Linq;
using System.Threading.Tasks;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.Repositories.Repository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FurniSpace.Infrastructure.Tests.Repositories;

public sealed class DeliveryProductIssueReportRepositoryTests
{
    [Fact]
    public async Task GetByOrderAsync_ReturnsIssuesOrderedByReportedAtDesc()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new DeliveryProductIssueReportRepository(context);

        var issues = await repository.GetByOrderAsync(data.OrderId);

        Assert.Equal(2, issues.Count);
        Assert.Equal(data.NewerIssueId, issues[0].DeliveryProductIssueReportId);
        Assert.Equal("Customer Two", issues[0].ReporterName);
    }

    [Fact]
    public async Task GetDetailAsync_ReturnsProductSnapshotAndEvidenceFiles()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new DeliveryProductIssueReportRepository(context);

        var detail = await repository.GetDetailAsync(data.NewerIssueId);

        Assert.NotNull(detail);
        Assert.Equal("Issue Project", detail!.ProjectName);
        Assert.Equal("Oak Chair", detail.ProductNameSnapshot);
        Assert.Single(detail.EvidenceFiles);
        Assert.Equal("damage.jpg", detail.EvidenceFiles[0].OriginalFileName);
    }

    [Fact]
    public async Task GetByProjectAsync_ReturnsProjectIssues()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new DeliveryProductIssueReportRepository(context);

        var issues = await repository.GetByProjectAsync(data.ProjectId);

        Assert.Equal(2, issues.Count);
        Assert.All(issues, issue => Assert.Equal(data.ProjectId, issue.ProjectId));
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
        var projectId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var orderItemId = Guid.NewGuid();
        var customerOneId = Guid.NewGuid();
        var customerTwoId = Guid.NewGuid();
        var olderIssueId = Guid.NewGuid();
        var newerIssueId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var fileLinkId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        context.ProjectSet.Add(new Project
        {
            ProjectId = projectId,
            CustomerId = customerOneId,
            ProjectName = "Issue Project",
            Status = ProjectStatus.DELIVERING
        });
        context.OrderSet.Add(new Order
        {
            OrderId = orderId,
            ProjectId = projectId,
            CustomerId = customerOneId,
            QuotationId = Guid.NewGuid(),
            OrderCode = "ORD-ISSUE",
            Status = OrderStatus.DELIVERING,
            VatRate = 0.08m,
            VatAmount = 0m,
            FinalTotalAmount = 100m,
            CreatedAt = now
        });
        context.OrderItemSet.Add(new OrderItem
        {
            OrderItemId = orderItemId,
            OrderId = orderId,
            ProductNameSnapshot = "Oak Chair",
            Quantity = 2,
            DeliveredQuantity = 2,
            Status = OrderItemStatus.DELIVERED,
            UnitPrice = 50m,
            DiscountAmount = 0m,
            SubtotalAmount = 100m
        });
        context.AccountSet.AddRange(
            new Account
            {
                AccountId = customerOneId,
                RoleId = Guid.NewGuid(),
                Email = "customer1@example.com",
                PasswordHash = "hash",
                FullName = "Customer One",
                Status = AccountStatus.ACTIVE
            },
            new Account
            {
                AccountId = customerTwoId,
                RoleId = Guid.NewGuid(),
                Email = "customer2@example.com",
                PasswordHash = "hash",
                FullName = "Customer Two",
                Status = AccountStatus.ACTIVE
            });
        context.DeliveryProductIssueReportSet.AddRange(
            new DeliveryProductIssueReport
            {
                DeliveryProductIssueReportId = olderIssueId,
                ProjectId = projectId,
                OrderId = orderId,
                OrderItemId = orderItemId,
                IssueType = DeliveryProductIssueType.DAMAGED,
                Description = "Older issue",
                ReportedBy = customerOneId,
                ReportedAt = now.AddHours(-2),
                CreatedAt = now.AddHours(-2)
            },
            new DeliveryProductIssueReport
            {
                DeliveryProductIssueReportId = newerIssueId,
                ProjectId = projectId,
                OrderId = orderId,
                OrderItemId = orderItemId,
                IssueType = DeliveryProductIssueType.QUALITY_DEFECT,
                Description = "Newer issue",
                ReportedBy = customerTwoId,
                ReportedAt = now.AddHours(-1),
                CreatedAt = now.AddHours(-1)
            });
        context.StoredFileSet.Add(new StoredFile
        {
            FileId = fileId,
            UploadedBy = customerTwoId,
            OriginalFileName = "damage.jpg",
            StoredFileName = $"{fileId:D}.jpg",
            FileUrl = "https://files.example/damage.jpg",
            StoragePath = $"projects/{projectId:D}/{fileId:D}.jpg",
            MimeType = "image/jpeg",
            FileExtension = "jpg",
            FileSizeBytes = 100,
            Status = FileStatus.ACTIVE,
            UploadedAt = now
        });
        context.FileLinkSet.Add(new FileLink
        {
            FileLinkId = fileLinkId,
            FileId = fileId,
            ReferenceType = "DELIVERY_PRODUCT_ISSUE_REPORT",
            ReferenceId = newerIssueId,
            FileType = FileType.PRODUCT_ISSUE_EVIDENCE,
            Visibility = FileVisibility.CUSTOMER_VISIBLE,
            CreatedBy = customerTwoId,
            CreatedAt = now
        });

        await context.SaveChangesAsync();
        return new SeededData(projectId, orderId, olderIssueId, newerIssueId);
    }

    private sealed record SeededData(Guid ProjectId, Guid OrderId, Guid OlderIssueId, Guid NewerIssueId);
}
