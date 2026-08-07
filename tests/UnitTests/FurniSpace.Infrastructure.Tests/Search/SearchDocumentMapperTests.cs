#nullable enable

using System;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Common.Search;
using FurniSpace.Infrastructure.Common.Search.Documents;
using FurniSpace.Infrastructure.ReadModels.ProjectChatMessages;
using FurniSpace.Infrastructure.ReadModels.ProjectFiles;
using FurniSpace.Infrastructure.ReadModels.Projects;
using Xunit;

namespace FurniSpace.Infrastructure.Tests.Search;

public sealed class SearchDocumentMapperTests
{
    [Fact]
    public void ChatMessageMapper_MapsIndexableMessageAndTrimsContent()
    {
        var item = new ChatMessageSearchIndexItemReadModel
        {
            MessageId = Guid.NewGuid(),
            ChatId = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            SenderId = Guid.NewGuid(),
            SenderName = "Sarah",
            MessageType = ProjectChatMessageType.TEXT,
            Content = "  hello project  ",
            CreatedAt = DateTime.UtcNow
        };

        var document = ChatMessageSearchDocumentMapper.ToDocument(item);

        Assert.True(ChatMessageSearchDocumentMapper.IsIndexable(item));
        Assert.Equal(item.MessageId, document.MessageId);
        Assert.Equal(item.ChatId, document.ChatId);
        Assert.Equal(item.ProjectId, document.ProjectId);
        Assert.Equal(item.SenderId, document.SenderId);
        Assert.Equal("Sarah", document.SenderName);
        Assert.Equal("TEXT", document.MessageType);
        Assert.Equal("hello project", document.Content);
        Assert.Equal(item.CreatedAt, document.CreatedAt);
    }

    [Fact]
    public void ChatMessageMapper_RejectsDeletedOrBlankMessages()
    {
        Assert.False(ChatMessageSearchDocumentMapper.IsIndexable(new ChatMessageSearchIndexItemReadModel
        {
            Content = "valid",
            DeletedAt = DateTime.UtcNow
        }));
        Assert.False(ChatMessageSearchDocumentMapper.IsIndexable(new ChatMessageSearchIndexItemReadModel
        {
            Content = " "
        }));
    }

    [Fact]
    public void ProjectFileMapper_MapsActiveFileAndRejectsArchivedOrUnnamedFile()
    {
        var item = new ProjectFileSearchIndexItemReadModel
        {
            FileId = Guid.NewGuid(),
            FileLinkId = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            ReferenceType = "PROJECT",
            ReferenceId = Guid.NewGuid(),
            OriginalFileName = "floor-plan.pdf",
            FileType = FileType.FLOOR_PLAN,
            Visibility = FileVisibility.CUSTOMER_VISIBLE,
            MimeType = "application/pdf",
            UploadedAt = DateTime.UtcNow,
            UploadedBy = Guid.NewGuid(),
            Status = FileStatus.ACTIVE
        };

        var document = ProjectFileSearchDocumentMapper.ToDocument(item);

        Assert.True(ProjectFileSearchDocumentMapper.IsIndexable(item));
        Assert.Equal(item.FileId, document.FileId);
        Assert.Equal(item.FileLinkId, document.FileLinkId);
        Assert.Equal(item.ProjectId, document.ProjectId);
        Assert.Equal("PROJECT", document.ReferenceType);
        Assert.Equal(item.ReferenceId, document.ReferenceId);
        Assert.Equal("floor-plan.pdf", document.OriginalFileName);
        Assert.Equal("FLOOR_PLAN", document.FileType);
        Assert.Equal("CUSTOMER_VISIBLE", document.Visibility);
        Assert.Equal("application/pdf", document.MimeType);
        Assert.Equal(item.UploadedAt, document.UploadedAt);
        Assert.Equal(item.UploadedBy, document.UploadedBy);
        Assert.False(ProjectFileSearchDocumentMapper.IsIndexable(CreateFile(FileStatus.ARCHIVED, "floor-plan.pdf")));
        Assert.False(ProjectFileSearchDocumentMapper.IsIndexable(CreateFile(FileStatus.ACTIVE, " ")));

        static ProjectFileSearchIndexItemReadModel CreateFile(FileStatus status, string name)
        {
            return new ProjectFileSearchIndexItemReadModel
            {
                OriginalFileName = name,
                Status = status
            };
        }
    }

    [Fact]
    public void ProjectMapper_RoundTripsSearchDocument()
    {
        var item = new ProjectSearchIndexItemReadModel
        {
            ProjectId = Guid.NewGuid(),
            ProjectCode = "PRJ-2026-0001",
            ProjectName = "Cafe Interior",
            BusinessType = "Cafe",
            Status = ProjectStatus.IN_CONSULTATION,
            CustomerId = Guid.NewGuid(),
            CustomerName = "Michael",
            CustomerEmail = "michael@example.com",
            CustomerPhone = "0900000001",
            AssignedSalesId = Guid.NewGuid(),
            AssignedDesignerId = Guid.NewGuid(),
            SubmittedAt = DateTime.UtcNow
        };

        var document = ProjectSearchDocumentMapper.ToDocument(item);
        var listItem = ProjectSearchDocumentMapper.ToListItem(document);

        Assert.Equal(item.ProjectId, document.ProjectId);
        Assert.Equal("IN_CONSULTATION", document.Status);
        Assert.Equal(item.CustomerEmail, document.CustomerEmail);
        Assert.Equal(item.ProjectId, listItem.ProjectId);
        Assert.Equal(ProjectStatus.IN_CONSULTATION, listItem.Status);
        Assert.Equal(item.AssignedSalesId, listItem.AssignedSalesId);
        Assert.Equal(item.AssignedDesignerId, listItem.AssignedDesignerId);
    }

    [Fact]
    public void ProjectMapper_WithUnknownStatus_ReturnsDefaultEnumValue()
    {
        var listItem = ProjectSearchDocumentMapper.ToListItem(new ProjectSearchDocument
        {
            ProjectId = Guid.NewGuid(),
            ProjectName = "Cafe Interior",
            CustomerId = Guid.NewGuid(),
            CustomerName = "Michael",
            CustomerEmail = "michael@example.com",
            Status = "NOT_A_STATUS"
        });

        Assert.Equal(ProjectStatus.SUBMITTED, listItem.Status);
    }
}
