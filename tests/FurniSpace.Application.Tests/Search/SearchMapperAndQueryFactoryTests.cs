#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using FurniSpace.Application.DTOs.Products;
using FurniSpace.Application.Services.Search;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Common.Search;
using FurniSpace.Infrastructure.Common.Search.Documents;
using FurniSpace.Infrastructure.ReadModels.Projects;
using Xunit;

namespace FurniSpace.Application.Tests.Search;

public sealed class SearchMapperAndQueryFactoryTests
{
    [Fact]
    public void ChatMessageQueryFactory_BuildsProjectScopedEscapedQuery()
    {
        var projectId = Guid.NewGuid();

        var request = ChatMessageElasticsearchQueryFactory.BuildProjectSearch(
            projectId,
            " hello/world ",
            page: 2,
            limit: 15);

        Assert.Equal(2, request.Page);
        Assert.Equal(15, request.PageSize);
        Assert.True(request.TrackTotalHits);
        Assert.Contains("content:*hello\\/world*", request.Query);
        Assert.Contains("senderName:*hello\\/world*", request.Query);
        Assert.Equal(new SearchFilter("projectId", SearchFilterOperator.Term, projectId.ToString()), request.Filters.Single());
        Assert.Equal(["createdAt", "messageId"], request.Sort.Select(sort => sort.Field).ToArray());
        Assert.All(request.Sort, sort => Assert.Equal(SortDirection.Desc, sort.Direction));
    }

    [Fact]
    public void ProjectFileQueryFactory_AddsCustomerVisibilityShouldGroup()
    {
        var projectId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        var request = ProjectFileElasticsearchQueryFactory.BuildProjectSearch(
            projectId,
            "floor plan",
            page: 1,
            limit: 20,
            customerVisibleOnly: true,
            customerAccountId: customerId);

        Assert.Equal("originalFileName:*floor\\ plan*", request.Query);
        Assert.Equal(new SearchFilter("projectId", SearchFilterOperator.Term, projectId.ToString()), request.Filters.Single());
        var group = Assert.Single(request.FilterShouldMatchOne);
        Assert.Contains(group.AnyOf, filter => filter.Field == "visibility" && Equals(filter.Value, nameof(FileVisibility.CUSTOMER_VISIBLE)));
        Assert.Contains(group.AnyOf, filter => filter.Field == "uploadedBy" && Equals(filter.Value, customerId.ToString()));
    }

    [Fact]
    public void ProjectQueryFactory_MapsFiltersAndBlankSearch()
    {
        var salesId = Guid.NewGuid();
        var query = new ProjectListQueryReadModel
        {
            Search = " ",
            Status = ProjectStatus.SUBMITTED,
            AssignedSalesId = salesId,
            Page = 3,
            Limit = 25
        };

        var request = ProjectElasticsearchQueryFactory.Build(query);

        Assert.Null(request.Query);
        Assert.Equal(3, request.Page);
        Assert.Equal(25, request.PageSize);
        Assert.Contains(request.Filters, filter => filter.Field == "status" && Equals(filter.Value, nameof(ProjectStatus.SUBMITTED)));
        Assert.Contains(request.Filters, filter => filter.Field == "assignedSalesId" && Equals(filter.Value, salesId.ToString()));
        Assert.Equal("submittedAt", request.Sort[0].Field);
    }

    [Fact]
    public void ProductQueryFactory_BuildsSearchSuggestSimilarAndRepositoryQueries()
    {
        var categoryId = Guid.NewGuid();
        var request = new ProductSearchRequestDto
        {
            Query = " oak table ",
            CategoryId = categoryId,
            Material = " oak ",
            Color = " brown ",
            MinPrice = 100,
            MaxPrice = 500,
            Sort = "price_desc",
            Page = 2,
            Limit = 12
        };

        var search = ProductElasticsearchQueryFactory.Build(request);
        var repositoryQuery = ProductElasticsearchQueryFactory.ToRepositoryQuery(request);
        var suggest = ProductElasticsearchQueryFactory.BuildSuggest(" chair ", 5);
        var similar = ProductElasticsearchQueryFactory.BuildSimilar(4);
        var fallback = ProductElasticsearchQueryFactory.BuildSuggestFallbackQuery("desk", 6);

        Assert.Contains("productName:*oak\\ table*", search.Query);
        Assert.Contains(search.Filters, filter => filter.Field == "categoryId" && Equals(filter.Value, categoryId.ToString()));
        Assert.Contains(search.Filters, filter => filter.Field == "material" && Equals(filter.Value, "oak"));
        Assert.Contains(search.Filters, filter => filter.Field == "estimatedPrice" && filter.Operator == SearchFilterOperator.RangeGte);
        Assert.Equal(["estimatedPrice", "productName.keyword"], search.Sort.Select(sort => sort.Field).ToArray());
        Assert.Equal(SortDirection.Desc, search.Sort[0].Direction);
        Assert.Equal(ProductElasticsearchQueryFactory.CategoryFacetField, search.FacetFields[0]);
        Assert.Equal("oak table", repositoryQuery.Query);
        Assert.Equal("oak", repositoryQuery.Material);
        Assert.Equal("brown", repositoryQuery.Color);
        Assert.Equal("chair", suggest.AutocompleteText);
        Assert.Equal(["productName.sayt"], suggest.AutocompleteFields.ToArray());
        Assert.Equal(4, similar.Size);
        Assert.Equal("desk", fallback.Query);
        Assert.Equal(6, fallback.Limit);
    }

    [Fact]
    public void AccountQueryFactory_BuildsSearchSuggestStatsAndEscapedFilters()
    {
        var search = AccountElasticsearchQueryFactory.BuildSearch(
            page: 2,
            pageSize: 25,
            search: " a+b@example.com ",
            status: "ACTIVE",
            includeDeleted: false);
        var suggest = AccountElasticsearchQueryFactory.BuildSuggest(" Nguyen Van A ", limit: 7);
        var stats = AccountElasticsearchQueryFactory.BuildStatsAggregation(includeDeleted: true);
        var deletedFilters = AccountElasticsearchQueryFactory.CreateAccountFilters(status: null, includeDeleted: false);

        Assert.Equal(2, search.Page);
        Assert.Equal(25, search.PageSize);
        Assert.Contains("email:*a\\+b@example.com*", search.Query);
        Assert.Contains(search.Filters, filter => filter.Field == "deletedAt" && filter.Operator == SearchFilterOperator.NotExists);
        Assert.Contains(search.Filters, filter => filter.Field == "status" && Equals(filter.Value, "ACTIVE"));
        Assert.Equal(["createdAt", "email.keyword"], search.Sort.Select(sort => sort.Field).ToArray());
        Assert.True(search.TrackTotalHits);

        Assert.Contains("fullName:*Nguyen\\ Van\\ A*", suggest.Query);
        Assert.Equal(7, suggest.PageSize);
        Assert.False(suggest.TrackTotalHits);
        Assert.Equal(["fullName.keyword", "email.keyword"], suggest.Sort.Select(sort => sort.Field).ToArray());

        Assert.Empty(stats.Filters);
        Assert.Equal([AccountElasticsearchQueryFactory.StatusField, AccountElasticsearchQueryFactory.RoleIdField], stats.TermsFields);
        Assert.Equal(20, stats.TermsSize);
        Assert.Single(deletedFilters);
    }

    [Fact]
    public void ResponseMappers_MapSearchDocumentsToDtos()
    {
        var message = new ChatMessageSearchDocument
        {
            MessageId = Guid.NewGuid(),
            ChatId = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            SenderId = Guid.NewGuid(),
            SenderName = "Designer",
            MessageType = "TEXT",
            Content = "Hello",
            CreatedAt = DateTime.UtcNow
        };
        var file = new ProjectFileSearchDocument
        {
            FileId = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            ReferenceType = "PROJECT",
            ReferenceId = Guid.NewGuid(),
            OriginalFileName = "floor.pdf",
            FileType = "FLOOR_PLAN",
            Visibility = "CUSTOMER_VISIBLE",
            MimeType = "application/pdf",
            UploadedAt = DateTime.UtcNow
        };
        var product = new ProductSearchDocument
        {
            ProductId = Guid.NewGuid(),
            CategoryId = Guid.NewGuid(),
            CategoryName = "Tables",
            ProductCode = "TBL-01",
            ProductName = "Oak Table",
            Description = "Solid",
            Status = "ACTIVE",
            Material = "Oak",
            Color = "Brown",
            Width = 120,
            Height = 75,
            Depth = 60,
            EstimatedPrice = 250,
            IsPublic = true
        };

        var messageDto = ChatMessageSearchResponseMapper.ToItem(message);
        var fileDto = ProjectFileSearchResponseMapper.ToItem(file);
        var productDto = ProductSearchResponseMapper.ToListItem(product);

        Assert.Equal(message.MessageId, messageDto.MessageId);
        Assert.Equal("Hello", messageDto.Content);
        Assert.Equal(file.FileId, fileDto.FileId);
        Assert.Equal("floor.pdf", fileDto.OriginalFileName);
        Assert.Equal(product.ProductId, productDto.ProductId);
        Assert.Equal(ProductStatus.ACTIVE, productDto.Status);
        Assert.NotNull(productDto.DefaultVersion);
        Assert.Equal("Oak", productDto.DefaultVersion.Material);
        Assert.True(productDto.DefaultVersion.IsPublic);
    }

    [Fact]
    public void SearchFacetMapper_MapsKnownAndMissingFacetBuckets()
    {
        var facets = new Dictionary<string, IReadOnlyList<SearchFacetBucket>>
        {
            [ProductElasticsearchQueryFactory.CategoryFacetField] =
            [
                new SearchFacetBucket { Key = "Tables", Count = 3 }
            ],
            [ProductElasticsearchQueryFactory.MaterialFacetField] =
            [
                new SearchFacetBucket { Key = "Oak", Count = 2 }
            ]
        };

        var result = SearchFacetMapper.ToProductFacets(facets);

        Assert.Single(result.Categories);
        Assert.Equal("Tables", result.Categories[0].Key);
        Assert.Equal(3, result.Categories[0].Count);
        Assert.Single(result.Materials);
        Assert.Empty(result.Colors);
    }
}
