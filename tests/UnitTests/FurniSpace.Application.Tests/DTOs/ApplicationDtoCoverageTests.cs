#nullable enable

using System;
using FurniSpace.Application.DTOs.Accounts;
using FurniSpace.Application.DTOs.Products;
using FurniSpace.Application.DTOs.ProjectChatMessages;
using FurniSpace.Application.DTOs.ProjectFiles;
using Xunit;

namespace FurniSpace.Application.Tests.DTOs;

public sealed class ApplicationDtoCoverageTests
{
    [Fact]
    public void AccountSuggestResponseDto_StoresItems()
    {
        var accountId = Guid.NewGuid();
        var response = new AccountSuggestResponseDto
        {
            Items =
            [
                new AccountSuggestItemDto
                {
                    AccountId = accountId,
                    FullName = "Nguyen Van A",
                    Email = "a@example.com"
                }
            ]
        };

        Assert.Single(response.Items);
        Assert.Equal(accountId, response.Items[0].AccountId);
        Assert.Equal("Nguyen Van A", response.Items[0].FullName);
        Assert.Equal("a@example.com", response.Items[0].Email);
    }

    [Fact]
    public void ProductListResponseDto_StoresPagingAndDefaultsItems()
    {
        var response = new ProductListResponseDto
        {
            Page = 2,
            Limit = 20,
            Total = 41,
            Facets = new ProductSearchFacetsDto()
        };

        Assert.Empty(response.Items);
        Assert.Equal(2, response.Page);
        Assert.Equal(20, response.Limit);
        Assert.Equal(41, response.Total);
        Assert.NotNull(response.Facets);
    }

    [Fact]
    public void SearchResponseDtos_StoreItemsAndPaging()
    {
        var messageId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var chatResponse = new ProjectChatMessageSearchResponseDto
        {
            Items =
            [
                new ProjectChatMessageSearchItemDto
                {
                    MessageId = messageId,
                    ChatId = Guid.NewGuid(),
                    ProjectId = Guid.NewGuid(),
                    SenderId = Guid.NewGuid(),
                    SenderName = "Designer",
                    MessageType = "TEXT",
                    Content = "Hello",
                    CreatedAt = DateTime.UtcNow
                }
            ],
            Page = 1,
            Limit = 10,
            Total = 1
        };
        var fileResponse = new ProjectFileSearchResponseDto
        {
            Items =
            [
                new ProjectFileSearchItemDto
                {
                    FileId = fileId,
                    ProjectId = Guid.NewGuid(),
                    ReferenceType = "PROJECT",
                    ReferenceId = Guid.NewGuid(),
                    OriginalFileName = "floor.pdf",
                    FileType = "FLOOR_PLAN",
                    Visibility = "CUSTOMER_VISIBLE",
                    MimeType = "application/pdf",
                    UploadedAt = DateTime.UtcNow
                }
            ],
            Page = 2,
            Limit = 5,
            Total = 11
        };

        Assert.Equal(messageId, chatResponse.Items[0].MessageId);
        Assert.Equal("Hello", chatResponse.Items[0].Content);
        Assert.Equal(1, chatResponse.Page);
        Assert.Equal(10, chatResponse.Limit);
        Assert.Equal(1, chatResponse.Total);
        Assert.Equal(fileId, fileResponse.Items[0].FileId);
        Assert.Equal("floor.pdf", fileResponse.Items[0].OriginalFileName);
        Assert.Equal(2, fileResponse.Page);
        Assert.Equal(5, fileResponse.Limit);
        Assert.Equal(11, fileResponse.Total);
    }
}
