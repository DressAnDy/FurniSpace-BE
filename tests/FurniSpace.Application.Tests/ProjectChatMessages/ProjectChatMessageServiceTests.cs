#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.DTOs.ProjectChatMessages;
using FurniSpace.Application.Interfaces.ProjectChatMessages;
using FurniSpace.Application.Common.Storage;
using FurniSpace.Application.Mappings;
using FurniSpace.Application.Services.ProjectChatMessages;
using FurniSpace.Application.Tests.TestDoubles;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Common.Storage;
using FurniSpace.Infrastructure.DTOs.ProjectChatMessages;
using FurniSpace.Infrastructure.Interfaces;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Infrastructure.Repositories.IRepository;
using FurniSpace.Infrastructure.Storage;
using Mapster;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace FurniSpace.Application.Tests.ProjectChatMessages;

public sealed class ProjectChatMessageServiceTests
{
    static ProjectChatMessageServiceTests()
    {
        TypeAdapterConfig.GlobalSettings.Scan(typeof(ProjectChatMessageMappingConfig).Assembly);
    }
    [Fact]
    public async Task SendTextMessageAsync_WithOpenChat_SavesBeforeRealtimeEvent()
    {
        var chatId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var salesId = Guid.NewGuid();
        var access = CreateAccess(chatId, salesId, "SALES", ProjectChatType.SALES);
        access = CopyAccess(access, projectId: projectId, currentUserName: "Nguyen Van A");
        var repository = new FakeProjectChatMessageRepository(access);
        var saveChangesCallCount = 0;
        var realtime = new FakeProjectChatRealtimeService((sentProjectId, sentChatId, message) =>
        {
            Assert.Equal(1, saveChangesCallCount);
            Assert.Equal(1, repository.AddCallCount);
            Assert.Equal(projectId, sentProjectId);
            Assert.Equal(chatId, sentChatId);
            Assert.Same(repository.AddedMessage, repository.LastAddedEntity);
            Assert.Equal(repository.AddedMessage!.MessageId, message.MessageId);
            return Task.CompletedTask;
        });
        var unitOfWork = TestUnitOfWork.ForSaveChanges(_ =>
        {
            saveChangesCallCount++;
            return Task.FromResult(1);
        });
        var service = CreateService(repository, realtime, unitOfWork);

        var result = await service.SendTextMessageAsync(
            chatId,
            salesId,
            new SendTextChatMessageRequestDto
            {
                MessageType = ProjectChatMessageType.TEXT,
                Content = "  Please send the floor plan.  "
            });

        Assert.Equal(201, result.Status);
        Assert.Equal("Message sent successfully.", result.Message);
        Assert.NotNull(result.Data);
        Assert.NotEqual(Guid.Empty, result.Data.MessageId);
        Assert.Equal(chatId, result.Data.ChatId);
        Assert.Equal(salesId, result.Data.SenderId);
        Assert.Equal("Nguyen Van A", result.Data.SenderName);
        Assert.Equal("SALES", result.Data.SenderRole);
        Assert.Equal(ProjectChatMessageType.TEXT.ToString(), result.Data.MessageType);
        Assert.Equal("Please send the floor plan.", result.Data.Content);
        Assert.Null(result.Data.Attachment);
        Assert.NotNull(result.Data.CreatedAt);
        Assert.Equal(1, saveChangesCallCount);
        Assert.Equal(1, realtime.CallCount);
    }

    [Fact]
    public async Task SendTextMessageAsync_WhenRealtimeFails_StillReturnsCreated()
    {
        var chatId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var access = CreateAccess(chatId, adminId, "ADMIN", ProjectChatType.INTERNAL);
        access = CopyAccess(access, chatStatus: null);
        var repository = new FakeProjectChatMessageRepository(access);
        var saveChangesCallCount = 0;
        var realtime = new FakeProjectChatRealtimeService((_, _, _) =>
            Task.FromException(new InvalidOperationException("SignalR unavailable")));
        var service = CreateService(
            repository,
            realtime,
            TestUnitOfWork.ForSaveChanges(_ =>
            {
                saveChangesCallCount++;
                return Task.FromResult(1);
            }));

        var result = await service.SendTextMessageAsync(
            chatId,
            adminId,
            ValidSendRequest());

        Assert.Equal(201, result.Status);
        Assert.Equal(1, repository.AddCallCount);
        Assert.Equal(1, saveChangesCallCount);
        Assert.Equal(1, realtime.CallCount);
    }

    [Theory]
    [InlineData(ProjectChatStatus.CLOSED)]
    [InlineData(ProjectChatStatus.ARCHIVED)]
    public async Task SendTextMessageAsync_WhenChatIsNotOpen_ReturnsConflict(
        ProjectChatStatus status)
    {
        var chatId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var access = CreateAccess(chatId, adminId, "ADMIN", ProjectChatType.INTERNAL);
        access = CopyAccess(access, chatStatus: status);
        var repository = new FakeProjectChatMessageRepository(access);
        var service = CreateService(repository);

        var result = await service.SendTextMessageAsync(chatId, adminId, ValidSendRequest());

        Assert.Equal(409, result.Status);
        Assert.Equal("Messages can only be sent to an open project chat.", result.Message);
        Assert.Equal(0, repository.AddCallCount);
    }

    [Fact]
    public async Task SendTextMessageAsync_WithoutAccess_ReturnsForbidden()
    {
        var chatId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var access = CreateAccess(chatId, customerId, "CUSTOMER", ProjectChatType.INTERNAL);
        var repository = new FakeProjectChatMessageRepository(access);
        var service = CreateService(repository);

        var result = await service.SendTextMessageAsync(chatId, customerId, ValidSendRequest());

        Assert.Equal(403, result.Status);
        Assert.Equal("You do not have access to this project chat.", result.Message);
        Assert.Equal(0, repository.AddCallCount);
    }

    [Fact]
    public async Task SendTextMessageAsync_WhenChatDoesNotExist_ReturnsNotFound()
    {
        var repository = new FakeProjectChatMessageRepository();
        var service = CreateService(repository);

        var result = await service.SendTextMessageAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            ValidSendRequest());

        Assert.Equal(404, result.Status);
        Assert.Equal("Project chat not found.", result.Message);
        Assert.Equal(0, repository.AddCallCount);
    }

    [Fact]
    public async Task SendTextMessageAsync_WithEmptyChatId_ReturnsBadRequest()
    {
        var repository = new FakeProjectChatMessageRepository();
        var service = CreateService(repository);

        var result = await service.SendTextMessageAsync(
            Guid.Empty,
            Guid.NewGuid(),
            ValidSendRequest());

        Assert.Equal(400, result.Status);
        Assert.Equal("Chat id is required.", result.Message);
        Assert.Equal(0, repository.GetAccessCallCount);
    }

    [Fact]
    public async Task SendTextMessageAsync_WithEmptyCurrentUser_ReturnsUnauthorized()
    {
        var repository = new FakeProjectChatMessageRepository();
        var service = CreateService(repository);

        var result = await service.SendTextMessageAsync(
            Guid.NewGuid(),
            Guid.Empty,
            ValidSendRequest());

        Assert.Equal(401, result.Status);
        Assert.Equal("Authenticated account id is required.", result.Message);
        Assert.Equal(0, repository.GetAccessCallCount);
    }

    [Theory]
    [InlineData(ProjectChatMessageType.FILE)]
    [InlineData(ProjectChatMessageType.SYSTEM)]
    [InlineData((ProjectChatMessageType)999)]
    public async Task SendTextMessageAsync_WithNonTextMessageType_ReturnsBadRequest(
        ProjectChatMessageType messageType)
    {
        var request = ValidSendRequest();
        request.MessageType = messageType;
        var service = CreateService(new FakeProjectChatMessageRepository());

        var result = await service.SendTextMessageAsync(Guid.NewGuid(), Guid.NewGuid(), request);

        Assert.Equal(400, result.Status);
        Assert.Equal("Message type must be TEXT.", result.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task SendTextMessageAsync_WithMissingContent_ReturnsBadRequest(string? content)
    {
        var request = ValidSendRequest();
        request.Content = content!;
        var service = CreateService(new FakeProjectChatMessageRepository());

        var result = await service.SendTextMessageAsync(Guid.NewGuid(), Guid.NewGuid(), request);

        Assert.Equal(400, result.Status);
        Assert.Equal("Message content is required.", result.Message);
    }

    [Fact]
    public async Task SendTextMessageAsync_WithTooLongContent_ReturnsBadRequest()
    {
        var request = ValidSendRequest();
        request.Content = new string('M', 4001);
        var service = CreateService(new FakeProjectChatMessageRepository());

        var result = await service.SendTextMessageAsync(Guid.NewGuid(), Guid.NewGuid(), request);

        Assert.Equal(400, result.Status);
        Assert.Equal("Message content must not exceed 4000 characters.", result.Message);
    }

    [Fact]
    public async Task SendFileMessageAsync_WithOpenChat_StoresFileAndCreatesFileMessage()
    {
        var chatId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var salesId = Guid.NewGuid();
        var access = CreateAccess(chatId, salesId, "SALES", ProjectChatType.SALES);
        access = CopyAccess(access, projectId: projectId, currentUserName: "Nguyen Van A");
        var repository = new FakeProjectChatMessageRepository(access);
        var projectFiles = new FakeCatalogProjectFileRepository();
        var storage = new FakeFileStorageService();
        var saveChangesCallCount = 0;
        var unitOfWork = TestUnitOfWork.ForTransaction(
            _ => Task.CompletedTask,
            _ =>
            {
                saveChangesCallCount++;
                return Task.FromResult(1);
            },
            _ => Task.CompletedTask,
            _ => Task.CompletedTask);
        var realtime = new FakeProjectChatRealtimeService();
        var service = CreateService(repository, realtime, unitOfWork, projectFiles, storage);

        await using var stream = new MemoryStream("floor-plan"u8.ToArray());
        var result = await service.SendFileMessageAsync(
            chatId,
            salesId,
            new SendFileChatMessageRequestDto
            {
                FileContent = stream,
                OriginalFileName = "floor-plan.pdf",
                ContentType = "application/pdf",
                FileSizeBytes = stream.Length,
                FileType = FileType.FLOOR_PLAN,
                Visibility = FileVisibility.CUSTOMER_VISIBLE,
                Content = "  Em gửi file mặt bằng.  "
            });

        Assert.Equal(201, result.Status);
        Assert.Equal("File message sent successfully.", result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal(ProjectChatMessageType.FILE.ToString(), result.Data.MessageType);
        Assert.Equal("Em gửi file mặt bằng.", result.Data.Content);
        Assert.NotNull(result.Data.Attachment);
        Assert.Equal("floor-plan.pdf", result.Data.Attachment.OriginalFileName);
        Assert.Equal("application/pdf", result.Data.Attachment.MimeType);
        Assert.Equal(1, repository.AddCallCount);
        Assert.Single(projectFiles.StoredFiles);
        Assert.Single(projectFiles.FileLinks);
        Assert.Equal(projectId, projectFiles.FileLinks[0].ReferenceId);
        Assert.Equal("PROJECT", projectFiles.FileLinks[0].ReferenceType);
        Assert.Equal(FileType.FLOOR_PLAN, projectFiles.FileLinks[0].FileType);
        Assert.Equal(FileVisibility.CUSTOMER_VISIBLE, projectFiles.FileLinks[0].Visibility);
        Assert.Equal(repository.AddedMessage!.AttachmentFileId, projectFiles.StoredFiles[0].FileId);
        Assert.Equal(1, saveChangesCallCount);
        Assert.Equal(1, realtime.CallCount);
        Assert.NotNull(storage.UploadRequest);
    }

    [Fact]
    public async Task SendFileMessageAsync_WithInvalidMimeType_ReturnsUnsupportedMediaType()
    {
        var service = CreateService(new FakeProjectChatMessageRepository());

        await using var stream = new MemoryStream([1, 2, 3]);
        var result = await service.SendFileMessageAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new SendFileChatMessageRequestDto
            {
                FileContent = stream,
                OriginalFileName = "malware.exe",
                ContentType = "application/x-msdownload",
                FileSizeBytes = stream.Length,
                FileType = FileType.OTHER
            });

        Assert.Equal(415, result.Status);
        Assert.Equal("File extension is not allowed.", result.Message);
    }

    [Fact]
    public async Task SendFileMessageAsync_WithFileTooLarge_ReturnsPayloadTooLarge()
    {
        var service = CreateService(
            new FakeProjectChatMessageRepository(),
            uploadSettings: new FileUploadSettings { MaxFileSizeBytes = 1024 });

        await using var stream = new MemoryStream(new byte[2048]);
        var result = await service.SendFileMessageAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new SendFileChatMessageRequestDto
            {
                FileContent = stream,
                OriginalFileName = "large.pdf",
                ContentType = "application/pdf",
                FileSizeBytes = stream.Length,
                FileType = FileType.FLOOR_PLAN
            });

        Assert.Equal(413, result.Status);
        Assert.Contains("File size must not exceed 1024 bytes.", result.Message);
    }

    [Fact]
    public async Task CanAccessChatAsync_WithValidParticipant_ReturnsTrue()
    {
        var chatId = Guid.NewGuid();
        var designerId = Guid.NewGuid();
        var repository = new FakeProjectChatMessageRepository(
            CreateAccess(chatId, designerId, "DESIGNER", ProjectChatType.DESIGNER));
        var service = CreateService(repository);

        var canAccess = await service.CanAccessChatAsync(chatId, designerId);

        Assert.True(canAccess);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task CanAccessChatAsync_WithEmptyId_ReturnsFalse(
        bool emptyChatId,
        bool emptyCurrentUserId)
    {
        var repository = new FakeProjectChatMessageRepository();
        var service = CreateService(repository);

        var canAccess = await service.CanAccessChatAsync(
            emptyChatId ? Guid.Empty : Guid.NewGuid(),
            emptyCurrentUserId ? Guid.Empty : Guid.NewGuid());

        Assert.False(canAccess);
        Assert.Equal(0, repository.GetAccessCallCount);
    }

    [Fact]
    public async Task CanAccessChatAsync_WhenChatMissingOrForbidden_ReturnsFalse()
    {
        var chatId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var missingRepository = new FakeProjectChatMessageRepository();
        var forbiddenRepository = new FakeProjectChatMessageRepository(
            CreateAccess(chatId, currentUserId, "CUSTOMER", ProjectChatType.INTERNAL));

        Assert.False(await CreateService(missingRepository).CanAccessChatAsync(chatId, currentUserId));
        Assert.False(await CreateService(forbiddenRepository).CanAccessChatAsync(chatId, currentUserId));
    }

    [Fact]
    public async Task GetMessagesAsync_WithAdmin_ReturnsMessagesAndAttachment()
    {
        var chatId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var attachment = new ProjectChatMessageAttachmentReadModel
        {
            FileId = Guid.NewGuid(),
            OriginalFileName = "floor-plan.pdf",
            MimeType = "application/pdf",
            FileSizeBytes = 2048,
            FileUrl = "https://files.example/floor-plan.pdf"
        };
        var activeMessage = CreateMessage(chatId);
        activeMessage = CopyMessage(activeMessage, attachment: attachment);
        var deletedMessage = CopyMessage(
            CreateMessage(chatId),
            content: "Hidden content",
            attachment: attachment,
            deletedAt: DateTime.UtcNow,
            messageType: null);
        var repository = new FakeProjectChatMessageRepository(
            CreateAccess(chatId, adminId, "ADMIN", ProjectChatType.INTERNAL),
            [activeMessage, deletedMessage]);
        var service = CreateService(repository);

        var result = await service.GetMessagesAsync(
            chatId,
            adminId,
            new ProjectChatMessageQueryDto());

        Assert.Equal(200, result.Status);
        Assert.Equal("Chat messages retrieved successfully.", result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal(1, result.Data.Page);
        Assert.Equal(30, result.Data.Limit);
        Assert.Equal(2, result.Data.Total);
        Assert.Equal(2, result.Data.Items.Count);
        var first = result.Data.Items[0];
        Assert.Equal(ProjectChatMessageType.TEXT.ToString(), first.MessageType);
        Assert.Equal(activeMessage.Content, first.Content);
        Assert.NotNull(first.Attachment);
        Assert.Equal(attachment.FileId, first.Attachment.FileId);
        Assert.Equal(attachment.OriginalFileName, first.Attachment.OriginalFileName);
        Assert.Equal(attachment.MimeType, first.Attachment.MimeType);
        Assert.Equal(attachment.FileSizeBytes, first.Attachment.FileSizeBytes);
        Assert.Equal(attachment.FileUrl, first.Attachment.FileUrl);
        var deleted = result.Data.Items[1];
        Assert.Equal(ProjectChatMessageType.TEXT.ToString(), deleted.MessageType);
        Assert.Null(deleted.Content);
        Assert.Null(deleted.Attachment);
        Assert.False(repository.LastQuery!.SortDescending);
        Assert.Equal(1, repository.GetAccessCallCount);
        Assert.Equal(1, repository.GetMessagesCallCount);
    }

    [Fact]
    public async Task GetMessagesAsync_WithDescendingSort_NormalizesAndPassesPagination()
    {
        var chatId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var repository = new FakeProjectChatMessageRepository(
            CreateAccess(chatId, adminId, "admin", ProjectChatType.GENERAL));
        var service = CreateService(repository);

        var result = await service.GetMessagesAsync(
            chatId,
            adminId,
            new ProjectChatMessageQueryDto { Page = 2, Limit = 10, Sort = " desc " });

        Assert.Equal(200, result.Status);
        Assert.Equal(chatId, repository.LastChatId);
        Assert.NotNull(repository.LastQuery);
        Assert.Equal(2, repository.LastQuery.Page);
        Assert.Equal(10, repository.LastQuery.Limit);
        Assert.True(repository.LastQuery.SortDescending);
    }

    [Theory]
    [InlineData("CUSTOMER", ProjectChatType.SALES)]
    [InlineData("CUSTOMER", ProjectChatType.DESIGNER)]
    [InlineData("SALES", ProjectChatType.SALES)]
    [InlineData("SALES", ProjectChatType.DESIGNER)]
    [InlineData("DESIGNER", ProjectChatType.DESIGNER)]
    public async Task GetMessagesAsync_WithAuthorizedParticipant_ReturnsSuccess(
        string roleName,
        ProjectChatType chatType)
    {
        var chatId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var repository = new FakeProjectChatMessageRepository(
            CreateAccess(chatId, currentUserId, roleName, chatType));
        var service = CreateService(repository);

        var result = await service.GetMessagesAsync(
            chatId,
            currentUserId,
            new ProjectChatMessageQueryDto());

        Assert.Equal(200, result.Status);
        Assert.Equal(1, repository.GetMessagesCallCount);
    }

    [Theory]
    [InlineData("CUSTOMER", ProjectChatType.INTERNAL)]
    [InlineData("CUSTOMER", ProjectChatType.SALES)]
    [InlineData("SALES", ProjectChatType.GENERAL)]
    [InlineData("SALES", ProjectChatType.SALES)]
    [InlineData("DESIGNER", ProjectChatType.SALES)]
    [InlineData("DESIGNER", ProjectChatType.DESIGNER)]
    [InlineData("UNKNOWN", ProjectChatType.DESIGNER)]
    [InlineData(null, ProjectChatType.DESIGNER)]
    public async Task GetMessagesAsync_WithoutChatAccess_ReturnsForbidden(
        string? roleName,
        ProjectChatType chatType)
    {
        var chatId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var access = CreateAccess(chatId, currentUserId, roleName, chatType);
        access = new ProjectChatMessageAccessReadModel
        {
            ChatId = access.ChatId,
            ProjectId = access.ProjectId,
            ChatType = access.ChatType,
            CustomerId = roleName == "CUSTOMER" && chatType == ProjectChatType.INTERNAL
                ? currentUserId
                : Guid.NewGuid(),
            AssignedSalesId = roleName == "SALES" && chatType == ProjectChatType.GENERAL
                ? currentUserId
                : Guid.NewGuid(),
            AssignedDesignerId = roleName == "DESIGNER" && chatType == ProjectChatType.SALES
                ? currentUserId
                : Guid.NewGuid(),
            RoleName = roleName
        };
        var repository = new FakeProjectChatMessageRepository(access);
        var service = CreateService(repository);

        var result = await service.GetMessagesAsync(
            chatId,
            currentUserId,
            new ProjectChatMessageQueryDto());

        Assert.Equal(403, result.Status);
        Assert.Equal("You do not have access to this project chat.", result.Message);
        Assert.Equal(0, repository.GetMessagesCallCount);
    }

    [Fact]
    public async Task GetMessagesAsync_WhenChatDoesNotExist_ReturnsNotFound()
    {
        var repository = new FakeProjectChatMessageRepository();
        var service = CreateService(repository);

        var result = await service.GetMessagesAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new ProjectChatMessageQueryDto());

        Assert.Equal(404, result.Status);
        Assert.Equal("Project chat not found.", result.Message);
        Assert.Equal(0, repository.GetMessagesCallCount);
    }

    [Fact]
    public async Task GetMessagesAsync_WithEmptyChatId_ReturnsBadRequest()
    {
        var repository = new FakeProjectChatMessageRepository();
        var service = CreateService(repository);

        var result = await service.GetMessagesAsync(
            Guid.Empty,
            Guid.NewGuid(),
            new ProjectChatMessageQueryDto());

        Assert.Equal(400, result.Status);
        Assert.Equal("Chat id is required.", result.Message);
        Assert.Equal(0, repository.GetAccessCallCount);
    }

    [Fact]
    public async Task GetMessagesAsync_WithEmptyCurrentUser_ReturnsUnauthorized()
    {
        var repository = new FakeProjectChatMessageRepository();
        var service = CreateService(repository);

        var result = await service.GetMessagesAsync(
            Guid.NewGuid(),
            Guid.Empty,
            new ProjectChatMessageQueryDto());

        Assert.Equal(401, result.Status);
        Assert.Equal("Authenticated account id is required.", result.Message);
        Assert.Equal(0, repository.GetAccessCallCount);
    }

    [Theory]
    [InlineData(0, 30, "Page must be greater than zero.")]
    [InlineData(1, 0, "Limit must be between 1 and 100.")]
    [InlineData(1, 101, "Limit must be between 1 and 100.")]
    public async Task GetMessagesAsync_WithInvalidPagination_ReturnsBadRequest(
        int page,
        int limit,
        string expectedMessage)
    {
        var repository = new FakeProjectChatMessageRepository();
        var service = CreateService(repository);

        var result = await service.GetMessagesAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new ProjectChatMessageQueryDto { Page = page, Limit = limit });

        Assert.Equal(400, result.Status);
        Assert.Equal(expectedMessage, result.Message);
        Assert.Equal(0, repository.GetAccessCallCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("newest")]
    public async Task GetMessagesAsync_WithInvalidSort_ReturnsBadRequest(string? sort)
    {
        var repository = new FakeProjectChatMessageRepository();
        var service = CreateService(repository);

        var result = await service.GetMessagesAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new ProjectChatMessageQueryDto { Sort = sort! });

        Assert.Equal(400, result.Status);
        Assert.Equal("Sort must be ASC or DESC.", result.Message);
        Assert.Equal(0, repository.GetAccessCallCount);
    }

    private static ProjectChatMessageService CreateService(
        IProjectChatMessageRepository repository,
        IProjectChatRealtimeService? realtime = null,
        IUnitOfWork? unitOfWork = null,
        IProjectFileRepository? projectFiles = null,
        IFileStorageService? storage = null,
        FileUploadSettings? uploadSettings = null)
    {
        return new ProjectChatMessageService(
            repository,
            projectFiles ?? new FakeCatalogProjectFileRepository(),
            realtime ?? new FakeProjectChatRealtimeService(),
            unitOfWork ?? TestUnitOfWork.Instance,
            new ProjectChatFileUploadDependencies(
                storage ?? new FakeFileStorageService(),
                new FileUploadValidator(
                    Options.Create(uploadSettings ?? new FileUploadSettings()),
                    Options.Create(new FirebaseStorageSettings())),
                new FirebaseStorageSettings()),
            NullLogger<ProjectChatMessageService>.Instance);
    }

    private static SendTextChatMessageRequestDto ValidSendRequest()
    {
        return new SendTextChatMessageRequestDto
        {
            MessageType = ProjectChatMessageType.TEXT,
            Content = "Project chat message"
        };
    }

    private static ProjectChatMessageAccessReadModel CreateAccess(
        Guid chatId,
        Guid currentUserId,
        string? roleName,
        ProjectChatType chatType)
    {
        return new ProjectChatMessageAccessReadModel
        {
            ChatId = chatId,
            ProjectId = Guid.NewGuid(),
            ChatType = chatType,
            ChatStatus = ProjectChatStatus.OPEN,
            CustomerId = roleName == "CUSTOMER" ? currentUserId : Guid.NewGuid(),
            AssignedSalesId = roleName == "SALES" ? currentUserId : Guid.NewGuid(),
            AssignedDesignerId = roleName == "DESIGNER" ? currentUserId : Guid.NewGuid(),
            CurrentUserName = "Project participant",
            RoleName = roleName
        };
    }

    private static ProjectChatMessageAccessReadModel CopyAccess(
        ProjectChatMessageAccessReadModel source,
        Guid? projectId = null,
        ProjectChatStatus? chatStatus = ProjectChatStatus.OPEN,
        string? currentUserName = null)
    {
        return new ProjectChatMessageAccessReadModel
        {
            ChatId = source.ChatId,
            ProjectId = projectId ?? source.ProjectId,
            ChatType = source.ChatType,
            ChatStatus = chatStatus,
            CustomerId = source.CustomerId,
            AssignedSalesId = source.AssignedSalesId,
            AssignedDesignerId = source.AssignedDesignerId,
            CurrentUserName = currentUserName ?? source.CurrentUserName,
            RoleName = source.RoleName
        };
    }

    private static ProjectChatMessageReadModel CreateMessage(Guid chatId)
    {
        return new ProjectChatMessageReadModel
        {
            MessageId = Guid.NewGuid(),
            ChatId = chatId,
            SenderId = Guid.NewGuid(),
            SenderName = "Nguyen Van A",
            SenderRole = "SALES",
            MessageType = ProjectChatMessageType.TEXT,
            Content = "Please provide the floor plan.",
            CreatedAt = DateTime.UtcNow
        };
    }

    private static ProjectChatMessageReadModel CopyMessage(
        ProjectChatMessageReadModel source,
        string? content = null,
        ProjectChatMessageAttachmentReadModel? attachment = null,
        DateTime? deletedAt = null,
        ProjectChatMessageType? messageType = ProjectChatMessageType.TEXT)
    {
        return new ProjectChatMessageReadModel
        {
            MessageId = source.MessageId,
            ChatId = source.ChatId,
            SenderId = source.SenderId,
            SenderName = source.SenderName,
            SenderRole = source.SenderRole,
            MessageType = messageType,
            Content = content ?? source.Content,
            Attachment = attachment,
            CreatedAt = source.CreatedAt,
            EditedAt = source.EditedAt,
            DeletedAt = deletedAt,
            ReadAt = source.ReadAt
        };
    }

    private sealed class FakeProjectChatMessageRepository : IProjectChatMessageRepository
    {
        private readonly ProjectChatMessageAccessReadModel? _access;
        private readonly IReadOnlyList<ProjectChatMessageReadModel> _items;

        public FakeProjectChatMessageRepository(
            ProjectChatMessageAccessReadModel? access = null,
            IReadOnlyList<ProjectChatMessageReadModel>? items = null)
        {
            _access = access;
            _items = items ?? [];
        }

        public int GetAccessCallCount { get; private set; }
        public int GetMessagesCallCount { get; private set; }
        public int AddCallCount { get; private set; }
        public Guid LastChatId { get; private set; }
        public ProjectChatMessageQueryReadModel? LastQuery { get; private set; }
        public ProjectChatMessage? AddedMessage { get; private set; }
        public ProjectChatMessage? LastAddedEntity { get; private set; }

        public Task<ProjectChatMessageAccessReadModel?> GetAccessAsync(
            Guid chatId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            GetAccessCallCount++;
            return Task.FromResult(_access?.ChatId == chatId ? _access : null);
        }

        public Task<(IReadOnlyList<ProjectChatMessageReadModel> Items, int Total)> GetMessagesAsync(
            Guid chatId,
            ProjectChatMessageQueryReadModel query,
            CancellationToken cancellationToken = default)
        {
            GetMessagesCallCount++;
            LastChatId = chatId;
            LastQuery = query;
            var ordered = query.SortDescending
                ? _items.OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.MessageId)
                : _items.OrderBy(item => item.CreatedAt).ThenBy(item => item.MessageId);
            var page = ordered.Skip((query.Page - 1) * query.Limit).Take(query.Limit).ToList();
            return Task.FromResult<(IReadOnlyList<ProjectChatMessageReadModel>, int)>((page, _items.Count));
        }

        public IQueryable<ProjectChatMessage> Query() => Array.Empty<ProjectChatMessage>().AsQueryable();
        public Task<ProjectChatMessage?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<ProjectChatMessage?>(null);
        public Task<IReadOnlyList<ProjectChatMessage>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ProjectChatMessage>>([]);
        public Task AddAsync(ProjectChatMessage entity, CancellationToken cancellationToken = default)
        {
            AddCallCount++;
            AddedMessage = entity;
            LastAddedEntity = entity;
            return Task.CompletedTask;
        }
        public Task AddRangeAsync(IEnumerable<ProjectChatMessage> entities, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
        public void Update(ProjectChatMessage entity) { }
        public void Remove(ProjectChatMessage entity) { }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
    }

    private sealed class FakeProjectChatRealtimeService : IProjectChatRealtimeService
    {
        private readonly Func<Guid, Guid, ProjectChatMessageDto, Task>? _send;

        public FakeProjectChatRealtimeService(
            Func<Guid, Guid, ProjectChatMessageDto, Task>? send = null)
        {
            _send = send;
        }

        public int CallCount { get; private set; }

        public Task SendMessageSentAsync(
            Guid projectId,
            Guid chatId,
            ProjectChatMessageDto message,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return _send?.Invoke(projectId, chatId, message) ?? Task.CompletedTask;
        }
    }

    private sealed class FakeFileStorageService : IFileStorageService
    {
        public StorageUploadRequest? UploadRequest { get; private set; }

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

        public Task DeleteAsync(string objectName, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
