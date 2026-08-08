#nullable enable

using System;
using FurniSpace.Application.Common.Catalog;
using FurniSpace.Application.Common.ProductVersions;
using FurniSpace.Application.Common.Products;
using FurniSpace.Domain.Enums;
using Xunit;

namespace FurniSpace.Application.Tests.Catalog;

public sealed class CatalogValidatorTests
{
    [Fact]
    public void ProductVersionLifecycleTransitionValidator_IsActive_TreatsNullAsActive()
    {
        Assert.True(ProductVersionLifecycleTransitionValidator.IsActive(null));
        Assert.True(ProductVersionLifecycleTransitionValidator.IsActive(ProductStatus.ACTIVE));
        Assert.False(ProductVersionLifecycleTransitionValidator.IsActive(ProductStatus.INACTIVE));
    }

    [Theory]
    [InlineData(ProductStatus.INACTIVE, true)]
    [InlineData(ProductStatus.ACTIVE, false)]
    [InlineData(ProductStatus.ARCHIVED, false)]
    public void ProductLifecycleTransitionValidator_CanActivate_OnlyFromInactive(
        ProductStatus status,
        bool expected)
    {
        Assert.Equal(expected, ProductLifecycleTransitionValidator.CanActivate(status));
    }

    [Theory]
    [InlineData(ProductStatus.ACTIVE, true)]
    [InlineData(ProductStatus.INACTIVE, false)]
    public void ProductLifecycleTransitionValidator_CanDeactivate_OnlyFromActive(
        ProductStatus status,
        bool expected)
    {
        Assert.Equal(expected, ProductLifecycleTransitionValidator.CanDeactivate(status));
    }

    [Theory]
    [InlineData(ProductStatus.ACTIVE, true)]
    [InlineData(ProductStatus.INACTIVE, true)]
    [InlineData(ProductStatus.ARCHIVED, false)]
    public void ProductLifecycleTransitionValidator_CanArchive_FromActiveOrInactive(
        ProductStatus status,
        bool expected)
    {
        Assert.Equal(expected, ProductLifecycleTransitionValidator.CanArchive(status));
    }

    [Fact]
    public void ProductLifecycleTransitionValidator_CanRestore_OnlyFromArchived()
    {
        Assert.True(ProductLifecycleTransitionValidator.CanRestore(ProductStatus.ARCHIVED));
        Assert.False(ProductLifecycleTransitionValidator.CanRestore(ProductStatus.ACTIVE));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(0, true)]
    [InlineData(10, true)]
    [InlineData(100, true)]
    [InlineData(-1, false)]
    [InlineData(101, false)]
    public void ProductVersionTaxRateValidator_IsValid_EnforcesRange(int? taxRate, bool expected)
    {
        decimal? rate = taxRate.HasValue ? taxRate.Value : null;
        Assert.Equal(expected, ProductVersionTaxRateValidator.IsValid(rate));
    }

    [Fact]
    public void ProjectCatalogEligibility_AllowsPublicActiveVersion()
    {
        var projectId = Guid.NewGuid();

        Assert.True(ProjectCatalogEligibility.IsEligibleVersion(
            ProductStatus.ACTIVE,
            ProductStatus.ACTIVE,
            isPublic: true,
            isProjectSpecific: false,
            versionProjectId: null,
            projectId));
    }

    [Fact]
    public void ProjectCatalogEligibility_AllowsProjectSpecificVersionForMatchingProject()
    {
        var projectId = Guid.NewGuid();

        Assert.True(ProjectCatalogEligibility.IsEligibleVersion(
            ProductStatus.ACTIVE,
            ProductStatus.ACTIVE,
            isPublic: false,
            isProjectSpecific: true,
            versionProjectId: projectId,
            projectId));
    }

    [Fact]
    public void ProjectCatalogEligibility_RejectsInactiveProductOrVersion()
    {
        var projectId = Guid.NewGuid();

        Assert.False(ProjectCatalogEligibility.IsEligibleVersion(
            ProductStatus.INACTIVE,
            ProductStatus.ACTIVE,
            isPublic: true,
            isProjectSpecific: false,
            versionProjectId: null,
            projectId));

        Assert.False(ProjectCatalogEligibility.IsEligibleVersion(
            ProductStatus.ACTIVE,
            ProductStatus.INACTIVE,
            isPublic: true,
            isProjectSpecific: false,
            versionProjectId: null,
            projectId));
    }

    [Fact]
    public void ProjectCatalogEligibility_RejectsProjectSpecificVersionForDifferentProject()
    {
        var projectId = Guid.NewGuid();

        Assert.False(ProjectCatalogEligibility.IsEligibleVersion(
            ProductStatus.ACTIVE,
            ProductStatus.ACTIVE,
            isPublic: false,
            isProjectSpecific: true,
            versionProjectId: Guid.NewGuid(),
            projectId));
    }
}
