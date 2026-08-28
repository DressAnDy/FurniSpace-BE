using System;
using FurniSpace.Application.DTOs.ProjectShowcases;
using FurniSpace.Application.Mappings;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.ReadModels.ProjectShowcases;
using Mapster;
using Xunit;

namespace FurniSpace.Application.Tests.ProjectShowcases;

public sealed class ProjectShowcaseMappingConfigTests
{
    public ProjectShowcaseMappingConfigTests()
    {
        var config = new TypeAdapterConfig();
        new ProjectShowcaseMappingConfig().Register(config);
        config.Compile();
        _config = config;
    }

    private readonly TypeAdapterConfig _config;

    [Fact]
    public void Adapt_PublicShowcaseListItemReadModel_MapsProjectFields()
    {
        var source = new PublicShowcaseListItemReadModel
        {
            ProjectShowcaseId = Guid.NewGuid(),
            Title = "Modern Cafe",
            Slug = "modern-cafe",
            Summary = "Summary",
            BusinessType = "Cafe",
            CompletedAt = new DateTime(2026, 8, 20, 15, 0, 0, DateTimeKind.Utc),
            TotalAreaSqm = 120.5m,
            CoverUrl = "https://cdn.example/cover.jpg",
            PublishedAt = new DateTime(2026, 8, 25, 8, 0, 0, DateTimeKind.Utc)
        };

        var result = source.Adapt<PublicShowcaseListItemDto>(_config);

        Assert.Equal(new DateOnly(2026, 8, 20), result.CompletedDate);
        Assert.Equal(120.5m, result.TotalAreaSqm);
        Assert.Equal("Cafe", result.BusinessType);
    }

    [Fact]
    public void Adapt_PublicShowcaseDetailReadModel_MapsDerivedFieldsAndCoverUrl()
    {
        var coverMediaId = Guid.NewGuid();
        var source = new PublicShowcaseDetailReadModel
        {
            ProjectShowcaseId = Guid.NewGuid(),
            Title = "Modern Cafe",
            Slug = "modern-cafe",
            Summary = "Summary",
            Description = "Description",
            ProjectName = "District 7 Cafe",
            BusinessType = "Cafe",
            CompletedAt = new DateTime(2026, 8, 20, 0, 0, 0, DateTimeKind.Utc),
            SubmittedAt = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            TotalAreaSqm = 120.5m,
            NumberOfFloors = 2,
            ProjectAddress = "District 7, Ho Chi Minh City",
            Media =
            [
                new ProjectShowcaseMediaReadModel
                {
                    ProjectShowcaseMediaId = Guid.NewGuid(),
                    FileId = Guid.NewGuid(),
                    MediaType = ProjectShowcaseMediaType.AFTER,
                    IsCover = false,
                    DisplayOrder = 2,
                    FileUrl = "https://cdn.example/gallery.jpg"
                },
                new ProjectShowcaseMediaReadModel
                {
                    ProjectShowcaseMediaId = coverMediaId,
                    FileId = Guid.NewGuid(),
                    MediaType = ProjectShowcaseMediaType.FINAL,
                    IsCover = true,
                    DisplayOrder = 1,
                    FileUrl = "https://cdn.example/cover.jpg"
                }
            ]
        };

        var result = source.Adapt<PublicShowcaseDetailDto>(_config);

        Assert.Equal(new DateOnly(2026, 8, 20), result.CompletedDate);
        Assert.Equal(2026, result.CompletionYear);
        Assert.Equal(80, result.ImplementationDurationDays);
        Assert.Equal("https://cdn.example/cover.jpg", result.CoverUrl);
        Assert.Equal(2, result.NumberOfFloors);
        Assert.Equal("District 7, Ho Chi Minh City", result.ProjectAddress);
    }

    [Fact]
    public void Adapt_PublicShowcaseDetailReadModel_WhenNoCoverMedia_CoverUrlIsNull()
    {
        var source = new PublicShowcaseDetailReadModel
        {
            ProjectShowcaseId = Guid.NewGuid(),
            Title = "No Cover",
            Slug = "no-cover",
            Media =
            [
                new ProjectShowcaseMediaReadModel
                {
                    ProjectShowcaseMediaId = Guid.NewGuid(),
                    FileId = Guid.NewGuid(),
                    MediaType = ProjectShowcaseMediaType.FINAL,
                    IsCover = false,
                    DisplayOrder = 1,
                    FileUrl = "https://cdn.example/gallery.jpg"
                }
            ]
        };

        var result = source.Adapt<PublicShowcaseDetailDto>(_config);

        Assert.Null(result.CoverUrl);
        Assert.Null(result.CompletedDate);
        Assert.Null(result.CompletionYear);
        Assert.Null(result.ImplementationDurationDays);
    }
}
