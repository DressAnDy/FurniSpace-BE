#nullable enable

using System;
using System.Collections.Generic;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Common.Caching;
using FurniSpace.Infrastructure.Common.Email;
using FurniSpace.Infrastructure.Common.Logging;
using FurniSpace.Infrastructure.Common.Search;
using FurniSpace.Infrastructure.ReadModels.ProjectFiles;
using Xunit;

namespace FurniSpace.Infrastructure.Tests.Common;

public sealed class InfrastructureModelCoverageTests
{
    [Fact]
    public void SearchModels_ExposeDefaultsAndAssignedValues()
    {
        var item = new BulkIndexItem<string>("id-1", "document");
        var facets = new Dictionary<string, IReadOnlyList<SearchFacetBucket>>
        {
            ["status"] = [new SearchFacetBucket { Key = "ACTIVE", Count = 2 }]
        };

        var aggregation = new SearchAggregationResult { Facets = facets };
        var search = new SearchResult<string>
        {
            Documents = ["one"],
            Total = 1,
            Page = 2,
            PageSize = 10,
            Facets = facets
        };
        var suggest = new SuggestRequest { Text = "so", Field = "name", Size = 5 };
        var suggestResult = new SuggestResult { Suggestions = ["sofa"] };

        Assert.Equal("id-1", item.Id);
        Assert.Equal("document", item.Document);
        Assert.Same(facets, aggregation.Facets);
        Assert.Equal("one", search.Documents[0]);
        Assert.Equal(1, search.Total);
        Assert.Equal(2, search.Page);
        Assert.Equal(10, search.PageSize);
        Assert.Same(facets, search.Facets);
        Assert.Equal("so", suggest.Text);
        Assert.Equal("name", suggest.Field);
        Assert.Equal(5, suggest.Size);
        Assert.Equal("sofa", suggestResult.Suggestions[0]);
    }

    [Fact]
    public void Settings_ExposeSectionNamesDefaultsAndAssignedValues()
    {
        var redis = new RedisSettings { ConnectionString = "localhost:6379" };
        var logging = new ElasticsearchLogSettings { Enabled = true, IndexFormat = "logs-{0:yyyy.MM.dd}" };
        var smtp = new SmtpSettings
        {
            Host = "smtp.example.com",
            Port = 2525,
            EnableSsl = false,
            Username = "user",
            Password = "secret",
            FromEmail = "noreply@example.com",
            FromName = "FurniSpace Test",
            ResetPasswordUrl = "https://example.com/reset"
        };

        Assert.Equal("Redis", RedisSettings.SectionName);
        Assert.Equal("localhost:6379", redis.ConnectionString);
        Assert.Equal("ElasticsearchLogging", ElasticsearchLogSettings.SectionName);
        Assert.True(logging.Enabled);
        Assert.Equal("logs-{0:yyyy.MM.dd}", logging.IndexFormat);
        Assert.Equal("Smtp", SmtpSettings.SectionName);
        Assert.Equal("smtp.example.com", smtp.Host);
        Assert.Equal(2525, smtp.Port);
        Assert.False(smtp.EnableSsl);
        Assert.Equal("user", smtp.Username);
        Assert.Equal("secret", smtp.Password);
        Assert.Equal("noreply@example.com", smtp.FromEmail);
        Assert.Equal("FurniSpace Test", smtp.FromName);
        Assert.Equal("https://example.com/reset", smtp.ResetPasswordUrl);
    }

    [Fact]
    public void FileLinkReadModel_StoresValues()
    {
        var model = new FileLinkReadModel
        {
            FileLinkId = Guid.NewGuid(),
            FileId = Guid.NewGuid(),
            ReferenceType = "PROJECT",
            ReferenceId = Guid.NewGuid(),
            FileType = FileType.MEASUREMENT_REPORT,
            Visibility = FileVisibility.CUSTOMER_VISIBLE,
            CreatedBy = Guid.NewGuid(),
            UploadedBy = Guid.NewGuid(),
            ProjectAccess = new ProjectFileAccessReadModel { ProjectId = Guid.NewGuid() }
        };

        Assert.Equal("PROJECT", model.ReferenceType);
        Assert.Equal(FileType.MEASUREMENT_REPORT, model.FileType);
        Assert.Equal(FileVisibility.CUSTOMER_VISIBLE, model.Visibility);
        Assert.NotEqual(Guid.Empty, model.FileLinkId);
        Assert.NotEqual(Guid.Empty, model.FileId);
        Assert.NotEqual(Guid.Empty, model.ReferenceId);
        Assert.NotNull(model.CreatedBy);
        Assert.NotEqual(Guid.Empty, model.UploadedBy);
        Assert.NotNull(model.ProjectAccess);
    }
}
