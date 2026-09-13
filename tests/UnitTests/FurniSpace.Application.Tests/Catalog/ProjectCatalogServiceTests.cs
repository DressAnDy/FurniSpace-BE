#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.DTOs.Catalog;
using FurniSpace.Application.Services.Catalog;
using FurniSpace.Application.Tests.TestDoubles;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.ReadModels.Products;
using FurniSpace.Infrastructure.ReadModels.ProjectFiles;
using FurniSpace.Infrastructure.ReadModels.Projects;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Xunit;

namespace FurniSpace.Application.Tests.Catalog;

public sealed class ProjectCatalogServiceTests
{
    [Fact]
    public async Task GetProductsAsync_WithAssignedDesigner_ReturnsEligibleProducts()
    {
        var projectId = Guid.NewGuid();
        var designerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var catalog = new FakeCatalogRepository
        {
            ProjectCatalogItems =
            [
                new ProjectCatalogProductListItemReadModel
                {
                    ProductId = productId,
                    ProductCode = "PM-001",
                    ProductName = "Counter",
                    EligibleVersions =
                    [
                        new ProjectCatalogEligibleVersionReadModel
                        {
                            ProductVersionId = Guid.NewGuid(),
                            ProductId = productId,
                            VersionCode = "PV-001",
                            VersionName = "Standard"
                        }
                    ]
                }
            ],
            ProjectCatalogTotal = 1
        };
        var service = CreateService(
            catalog,
            project: CreateProject(projectId, designerId));

        var result = await service.GetProductsAsync(
            projectId,
            designerId,
            "DESIGNER",
            new ProjectCatalogQueryDto { Page = 1, PageSize = 20 });

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data.Items);
        Assert.Equal(1, result.Data.Items[0].EligibleVersionCount);
    }

    [Fact]
    public async Task GetProductsAsync_WithUnassignedDesigner_ReturnsForbidden()
    {
        var projectId = Guid.NewGuid();
        var service = CreateService(
            new FakeCatalogRepository(),
            project: CreateProject(projectId, assignedDesignerId: Guid.NewGuid()));

        var result = await service.GetProductsAsync(
            projectId,
            Guid.NewGuid(),
            "DESIGNER",
            new ProjectCatalogQueryDto { Page = 1, PageSize = 20 });

        Assert.Equal(403, result.Status);
        Assert.Equal(CatalogErrorCodes.DesignerNotAssigned, result.ErrorCode);
    }

    [Fact]
    public async Task GetProductsAsync_WithAdminRole_AllowsAccessWithoutAssignment()
    {
        var projectId = Guid.NewGuid();
        var service = CreateService(
            new FakeCatalogRepository { ProjectCatalogItems = [] },
            project: CreateProject(projectId, assignedDesignerId: Guid.NewGuid()));

        var result = await service.GetProductsAsync(
            projectId,
            Guid.NewGuid(),
            "ADMIN",
            new ProjectCatalogQueryDto { Page = 1, PageSize = 20 });

        Assert.Equal(200, result.Status);
    }

    [Fact]
    public async Task GetProductByIdAsync_WithEligibleProduct_ReturnsDetailWithoutTaxRate()
    {
        var projectId = Guid.NewGuid();
        var designerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var catalog = new FakeCatalogRepository
        {
            ProjectCatalogProductDetail = new ProjectCatalogProductListItemReadModel
            {
                ProductId = productId,
                ProductName = "Counter",
                EligibleVersions =
                [
                    new ProjectCatalogEligibleVersionReadModel
                    {
                        ProductVersionId = versionId,
                        ProductId = productId,
                        VersionCode = "PV-001",
                        VersionName = "Standard",
                        EstimatedPrice = 1000m
                    }
                ]
            }
        };
        var service = CreateService(catalog, project: CreateProject(projectId, designerId));

        var result = await service.GetProductByIdAsync(projectId, productId, designerId, "DESIGNER");

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data.EligibleVersions);
        Assert.Equal("PV-001", result.Data.EligibleVersions[0].VersionCode);
        Assert.DoesNotContain(
            result.Data.GetType().GetProperties().Select(property => property.Name),
            name => name == "DefaultTaxRate");
    }

    [Fact]
    public async Task GetProductVersionByIdAsync_WithEligibleVersion_ReturnsDetail()
    {
        var projectId = Guid.NewGuid();
        var designerId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var catalog = new FakeCatalogRepository
        {
            ProjectEligibleVersionDetail = new ProjectCatalogEligibleVersionReadModel
            {
                ProductVersionId = versionId,
                ProductId = productId,
                ProjectId = projectId,
                VersionCode = "PV-001",
                VersionName = "Standard",
                EstimatedPrice = 2500m
            }
        };
        var files = new FakeCatalogProjectFileRepository
        {
            CatalogFiles =
            [
                new CatalogFileReadModel
                {
                    FileId = Guid.NewGuid(),
                    ReferenceId = versionId,
                    ReferenceType = "PRODUCT_VERSION",
                    OriginalFileName = "preview.jpg",
                    FileUrl = "https://example.com/preview.jpg",
                    Visibility = FileVisibility.CUSTOMER_VISIBLE,
                    Status = FileStatus.ACTIVE
                }
            ]
        };
        var service = CreateService(
            catalog,
            project: CreateProject(projectId, designerId),
            files: files);

        var result = await service.GetProductVersionByIdAsync(
            projectId,
            versionId,
            designerId,
            "DESIGNER");

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(versionId, result.Data.ProductVersionId);
        Assert.Single(result.Data.Files);
    }

    [Fact]
    public async Task GetProductVersionByIdAsync_WithMissingVersion_ReturnsNotFound()
    {
        var projectId = Guid.NewGuid();
        var designerId = Guid.NewGuid();
        var service = CreateService(
            new FakeCatalogRepository(),
            project: CreateProject(projectId, designerId));

        var result = await service.GetProductVersionByIdAsync(
            projectId,
            Guid.NewGuid(),
            designerId,
            "DESIGNER");

        Assert.Equal(404, result.Status);
        Assert.Equal(CatalogErrorCodes.CatalogVersionNotEligible, result.ErrorCode);
    }

    [Fact]
    public async Task GetProductsAsync_WithEmptyUserId_ReturnsUnauthorized()
    {
        var service = CreateService(new FakeCatalogRepository(), project: CreateProject(Guid.NewGuid(), Guid.NewGuid()));

        var result = await service.GetProductsAsync(
            Guid.NewGuid(),
            Guid.Empty,
            "DESIGNER",
            new ProjectCatalogQueryDto { Page = 1, PageSize = 20 });

        Assert.Equal(401, result.Status);
    }

    [Fact]
    public async Task GetProductsAsync_WithInvalidPagination_ReturnsBadRequest()
    {
        var projectId = Guid.NewGuid();
        var designerId = Guid.NewGuid();
        var service = CreateService(
            new FakeCatalogRepository(),
            project: CreateProject(projectId, designerId));

        var result = await service.GetProductsAsync(
            projectId,
            designerId,
            "DESIGNER",
            new ProjectCatalogQueryDto { Page = 0, PageSize = 20 });

        Assert.Equal(400, result.Status);
        Assert.Equal(CatalogErrorCodes.CatalogFilterInvalid, result.ErrorCode);
    }

    [Fact]
    public async Task GetProductByIdAsync_WithMissingProduct_ReturnsNotFound()
    {
        var projectId = Guid.NewGuid();
        var designerId = Guid.NewGuid();
        var service = CreateService(
            new FakeCatalogRepository(),
            project: CreateProject(projectId, designerId));

        var result = await service.GetProductByIdAsync(
            projectId,
            Guid.NewGuid(),
            designerId,
            "DESIGNER");

        Assert.Equal(404, result.Status);
        Assert.Equal(CatalogErrorCodes.CatalogProductNotEligible, result.ErrorCode);
    }

    private static ProjectCatalogService CreateService(
        ICatalogRepository catalog,
        ProjectDetailReadModel? project = null,
        IProjectFileRepository? files = null)
    {
        return new ProjectCatalogService(
            catalog,
            new FakeProjectRepository(project),
            files ?? new FakeCatalogProjectFileRepository());
    }

    private static ProjectDetailReadModel CreateProject(Guid projectId, Guid assignedDesignerId)
        => new()
        {
            ProjectId = projectId,
            CustomerId = Guid.NewGuid(),
            AssignedDesignerId = assignedDesignerId,
            ProjectName = "Cafe Project"
        };

    private sealed class FakeProjectRepository : IProjectRepository
    {
        private readonly ProjectDetailReadModel? _detail;

        public FakeProjectRepository(ProjectDetailReadModel? detail)
        {
            _detail = detail;
        }

        public Task<ProjectDetailReadModel?> GetDetailAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(_detail?.ProjectId == projectId ? _detail : null);

        public Task<string?> GetAccountRoleNameAsync(Guid accountId, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);

        public Task<string?> GetAccountFullNameAsync(Guid accountId, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);

        public Task<IReadOnlyList<Guid>> GetActiveAccountIdsByRoleNamesAsync(
            IReadOnlyCollection<string> roleNames,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Guid>>([]);

        public Task<int> CountSubmittedInYearAsync(int year, CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<DesignerAccountReadModel?> GetActiveDesignerAsync(
            Guid designerId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<DesignerAccountReadModel?>(null);

        public Task<IReadOnlyList<ProjectListItemReadModel>> GetListAsync(
            ProjectListQueryReadModel query,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProjectListItemReadModel>>([]);

        public Task<int> CountAsync(ProjectListQueryReadModel query, CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<IReadOnlyList<ProjectByUserItemReadModel>> GetByUserAsync(
            ProjectByUserQueryReadModel query,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProjectByUserItemReadModel>>([]);

        public Task<int> CountByUserAsync(ProjectByUserQueryReadModel query, CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<ProjectSearchIndexItemReadModel?> GetSearchIndexItemAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<ProjectSearchIndexItemReadModel?>(null);

        public Task<IReadOnlyList<ProjectSearchIndexItemReadModel>> GetSearchIndexPageAsync(
            int page,
            int limit,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProjectSearchIndexItemReadModel>>([]);

        public IQueryable<Project> Query() => Enumerable.Empty<Project>().AsQueryable();

        public Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<Project?>(null);

        public Task<IReadOnlyList<Project>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Project>>([]);

        public Task AddAsync(Project entity, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task AddRangeAsync(IEnumerable<Project> entities, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public void Update(Project entity)
        {
        }

        public void Remove(Project entity)
        {
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(0);
    }
}
