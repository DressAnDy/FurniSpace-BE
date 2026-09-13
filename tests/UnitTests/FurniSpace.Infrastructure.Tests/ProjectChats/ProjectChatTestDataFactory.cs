#nullable enable

using System;
using System.Threading.Tasks;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.Repositories.IRepository;
using FurniSpace.Infrastructure.Repositories.Repository;
using Microsoft.EntityFrameworkCore;

namespace FurniSpace.Infrastructure.Tests.ProjectChats;

internal static class ProjectChatTestDataFactory
{
    internal sealed class SeededData
    {
        public Guid ProjectId { get; init; }
        public Guid CustomerAccountId { get; init; }
        public Guid SalesAccountId { get; init; }
        public Guid DesignerAccountId { get; init; }
        public Guid SalesChatId { get; init; }
        public Guid DesignerChatId { get; init; }
        public Guid ArchivedChatId { get; init; }
        public Guid FileMessageId { get; init; }
        public Guid AttachmentFileId { get; init; }
    }

    internal static async Task<(AppDbContext Context, SeededData Data)> CreateSeededContextAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new AppDbContext(options);

        var salesRoleId = Guid.NewGuid();
        var designerRoleId = Guid.NewGuid();
        var customerRoleId = Guid.NewGuid();
        var adminRoleId = Guid.NewGuid();

        var customerAccountId = Guid.NewGuid();
        var salesAccountId = Guid.NewGuid();
        var designerAccountId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var salesChatId = Guid.NewGuid();
        var designerChatId = Guid.NewGuid();
        var archivedChatId = Guid.NewGuid();
        var fileMessageId = Guid.NewGuid();
        var attachmentFileId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        context.RoleSet.AddRange(
            new Role { RoleId = salesRoleId, RoleName = "SALES", CreatedAt = now },
            new Role { RoleId = designerRoleId, RoleName = "DESIGNER", CreatedAt = now },
            new Role { RoleId = customerRoleId, RoleName = "CUSTOMER", CreatedAt = now },
            new Role { RoleId = adminRoleId, RoleName = "ADMIN", CreatedAt = now });

        context.AccountSet.AddRange(
            new Account
            {
                AccountId = customerAccountId,
                RoleId = customerRoleId,
                Email = "customer@example.com",
                PasswordHash = "hash",
                FullName = "Customer User",
                Status = AccountStatus.ACTIVE,
                CreatedAt = now
            },
            new Account
            {
                AccountId = salesAccountId,
                RoleId = salesRoleId,
                Email = "sales@example.com",
                PasswordHash = "hash",
                FullName = "Sales User",
                Status = AccountStatus.ACTIVE,
                CreatedAt = now
            },
            new Account
            {
                AccountId = designerAccountId,
                RoleId = designerRoleId,
                Email = "designer@example.com",
                PasswordHash = "hash",
                FullName = "Designer User",
                Status = AccountStatus.ACTIVE,
                CreatedAt = now
            });

        context.ProjectSet.Add(new Project
        {
            ProjectId = projectId,
            CustomerId = customerAccountId,
            AssignedSalesId = salesAccountId,
            AssignedDesignerId = designerAccountId,
            ProjectName = "Test Project",
            Status = ProjectStatus.IN_CONSULTATION,
            CreatedAt = now
        });

        context.ProjectChatSet.AddRange(
            new ProjectChat
            {
                ChatId = salesChatId,
                ProjectId = projectId,
                ChatType = ProjectChatType.SALES,
                StaffId = salesAccountId,
                Title = "Sales chat",
                Status = ProjectChatStatus.OPEN,
                CreatedAt = now.AddMinutes(-10)
            },
            new ProjectChat
            {
                ChatId = designerChatId,
                ProjectId = projectId,
                ChatType = ProjectChatType.DESIGNER,
                StaffId = designerAccountId,
                Title = "Designer chat",
                Status = ProjectChatStatus.OPEN,
                CreatedAt = now.AddMinutes(-5)
            },
            new ProjectChat
            {
                ChatId = archivedChatId,
                ProjectId = projectId,
                ChatType = ProjectChatType.GENERAL,
                StaffId = salesAccountId,
                Title = "Archived chat",
                Status = ProjectChatStatus.ARCHIVED,
                CreatedAt = now.AddMinutes(-20)
            });

        context.StoredFileSet.Add(new StoredFile
        {
            FileId = attachmentFileId,
            UploadedBy = salesAccountId,
            OriginalFileName = "floor-plan.pdf",
            StoredFileName = $"{attachmentFileId:N}.pdf",
            FileUrl = "https://files.example/floor-plan.pdf",
            StoragePath = $"projects/{projectId:D}/{attachmentFileId:N}.pdf",
            MimeType = "application/pdf",
            FileExtension = "pdf",
            FileSizeBytes = 2048,
            Status = FileStatus.ACTIVE,
            UploadedAt = now
        });

        context.ProjectChatMessageSet.AddRange(
            new ProjectChatMessage
            {
                MessageId = Guid.NewGuid(),
                ChatId = salesChatId,
                SenderId = salesAccountId,
                MessageType = ProjectChatMessageType.TEXT,
                Content = new string('A', 250),
                CreatedAt = now.AddMinutes(-2)
            },
            new ProjectChatMessage
            {
                MessageId = fileMessageId,
                ChatId = salesChatId,
                SenderId = salesAccountId,
                MessageType = ProjectChatMessageType.FILE,
                Content = "Attached file",
                AttachmentFileId = attachmentFileId,
                CreatedAt = now.AddMinutes(-1)
            },
            new ProjectChatMessage
            {
                MessageId = Guid.NewGuid(),
                ChatId = salesChatId,
                SenderId = salesAccountId,
                MessageType = ProjectChatMessageType.TEXT,
                Content = "Deleted message",
                CreatedAt = now,
                DeletedAt = now
            });

        await context.SaveChangesAsync();

        return (context, new SeededData
        {
            ProjectId = projectId,
            CustomerAccountId = customerAccountId,
            SalesAccountId = salesAccountId,
            DesignerAccountId = designerAccountId,
            SalesChatId = salesChatId,
            DesignerChatId = designerChatId,
            ArchivedChatId = archivedChatId,
            FileMessageId = fileMessageId,
            AttachmentFileId = attachmentFileId
        });
    }

    internal static ProjectChatRepository CreateRepository(AppDbContext context) => new(context);

    internal static ProjectChatMessageRepository CreateMessageRepository(AppDbContext context) => new(context);
}
