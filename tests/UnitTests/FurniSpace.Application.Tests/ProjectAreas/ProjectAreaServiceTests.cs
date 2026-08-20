#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.DTOs.ProjectAreas;
using FurniSpace.Application.Services.ProjectAreas;
using FurniSpace.Application.Tests.TestDoubles;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.ReadModels.ProjectAreas;
using FurniSpace.Infrastructure.ReadModels.Projects;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Xunit;

namespace FurniSpace.Application.Tests.ProjectAreas;

public sealed class ProjectAreaServiceTests
{
    [Fact]
    public async Task CreateAsync_AssignedSales_CreatesProjectArea()
    {
        var salesId = Guid.NewGuid();
        var project = CreateProject(assignedSalesId: salesId);
        var areaRepo = new FakeProjectAreaRepository();
        var service = BuildService(new() { Role = "SALES", ProjectDetail = project, AreaRepo = areaRepo });

        var result = await service.CreateAsync(
            project.ProjectId,
            salesId,
            ValidCreateRequest());

        Assert.Equal(201, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal("Main Cafe Area", result.Data.AreaName);
        Assert.Equal(ProjectAreaType.ZONE, result.Data.AreaType);
        Assert.Equal(ProjectAreaStatus.DRAFT, result.Data.Status);
        Assert.False(result.Data.IsSpecialLayout);
        Assert.Equal(1, areaRepo.AddCallCount);
    }

    [Fact]
    public async Task CreateAsync_AssignedDesigner_CreatesProjectArea()
    {
        var designerId = Guid.NewGuid();
        var project = CreateProject(assignedDesignerId: designerId);
        var service = BuildService(new() { Role = "DESIGNER", ProjectDetail = project });

        var result = await service.CreateAsync(
            project.ProjectId,
            designerId,
            ValidCreateRequest());

        Assert.Equal(201, result.Status);
    }

    [Fact]
    public async Task CreateAsync_Customer_ReturnsForbidden()
    {
        var project = CreateProject();
        var service = BuildService(new() { Role = "CUSTOMER", ProjectDetail = project });

        var result = await service.CreateAsync(
            project.ProjectId,
            Guid.NewGuid(),
            ValidCreateRequest());

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task CreateAsync_InvalidParentArea_ReturnsInvalidParentArea()
    {
        var salesId = Guid.NewGuid();
        var project = CreateProject(assignedSalesId: salesId);
        var service = BuildService(new() { Role = "SALES", ProjectDetail = project });
        var request = ValidCreateRequest();
        request.ParentAreaId = Guid.NewGuid();

        var result = await service.CreateAsync(project.ProjectId, salesId, request);

        Assert.Equal(400, result.Status);
        Assert.Equal(ProjectAreaErrorCodes.InvalidParentArea, result.ErrorCode);
    }

    [Fact]
    public async Task CreateAsync_NegativeDimension_ReturnsInvalidAreaDimension()
    {
        var salesId = Guid.NewGuid();
        var project = CreateProject(assignedSalesId: salesId);
        var service = BuildService(new() { Role = "SALES", ProjectDetail = project });
        var request = ValidCreateRequest();
        request.Width = -1;

        var result = await service.CreateAsync(project.ProjectId, salesId, request);

        Assert.Equal(400, result.Status);
        Assert.Equal(ProjectAreaErrorCodes.InvalidAreaDimension, result.ErrorCode);
    }

    [Fact]
    public async Task CreateAsync_StandardLayoutWithMismatchedArea_ReturnsInvalidAreaDimension()
    {
        var salesId = Guid.NewGuid();
        var project = CreateProject(assignedSalesId: salesId);
        var service = BuildService(new() { Role = "SALES", ProjectDetail = project });
        var request = ValidCreateRequest();
        request.AreaSqm = 99m;

        var result = await service.CreateAsync(project.ProjectId, salesId, request);

        Assert.Equal(400, result.Status);
        Assert.Equal(ProjectAreaErrorCodes.InvalidAreaDimension, result.ErrorCode);
    }

    [Fact]
    public async Task CreateAsync_SpecialLayoutAcceptsNullWidthAndLength()
    {
        var salesId = Guid.NewGuid();
        var project = CreateProject(assignedSalesId: salesId);
        var areaRepo = new FakeProjectAreaRepository();
        var service = BuildService(new() { Role = "SALES", ProjectDetail = project, AreaRepo = areaRepo });
        var request = ValidCreateRequest();
        request.IsSpecialLayout = true;
        request.Width = null;
        request.Length = null;

        var result = await service.CreateAsync(project.ProjectId, salesId, request);

        Assert.Equal(201, result.Status);
        Assert.NotNull(result.Data);
        Assert.True(result.Data.IsSpecialLayout);
        Assert.Null(areaRepo.EntityById?.Width);
        Assert.Null(areaRepo.EntityById?.Length);
    }

    [Fact]
    public async Task CreateAsync_SpecialLayoutWithoutHeight_ReturnsInvalidAreaDimension()
    {
        var salesId = Guid.NewGuid();
        var project = CreateProject(assignedSalesId: salesId);
        var service = BuildService(new() { Role = "SALES", ProjectDetail = project });
        var request = ValidCreateRequest();
        request.IsSpecialLayout = true;
        request.Height = null;

        var result = await service.CreateAsync(project.ProjectId, salesId, request);

        Assert.Equal(400, result.Status);
        Assert.Equal(ProjectAreaErrorCodes.InvalidAreaDimension, result.ErrorCode);
    }

    [Fact]
    public async Task GetListByProjectAsync_CustomerOwner_ReturnsSuccess()
    {
        var customerId = Guid.NewGuid();
        var project = CreateProject(customerId: customerId);
        var areaRepo = new FakeProjectAreaRepository
        {
            ListItems =
            [
                CreateAreaDetail(project.ProjectId, customerId: customerId)
            ]
        };
        var service = BuildService(new() { Role = "CUSTOMER", ProjectDetail = project, AreaRepo = areaRepo });

        var result = await service.GetListByProjectAsync(project.ProjectId, customerId, includeCancelled: false);

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data);
    }

    [Fact]
    public async Task GetListByProjectAsync_UnauthorizedUser_ReturnsForbidden()
    {
        var project = CreateProject(assignedDesignerId: Guid.NewGuid());
        var service = BuildService(new() { Role = "DESIGNER", ProjectDetail = project });

        var result = await service.GetListByProjectAsync(project.ProjectId, Guid.NewGuid(), includeCancelled: false);

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task GetDetailAsync_AuthorizedMember_ReturnsSuccess()
    {
        var designerId = Guid.NewGuid();
        var detail = CreateAreaDetail(Guid.NewGuid(), assignedDesignerId: designerId);
        var service = BuildService(new() { Role = "DESIGNER", AreaRepo = new FakeProjectAreaRepository { Detail = detail } });

        var result = await service.GetDetailAsync(detail.ProjectAreaId, designerId);

        Assert.Equal(200, result.Status);
        Assert.Equal(detail.ProjectAreaId, result.Data!.ProjectAreaId);
    }

    [Fact]
    public async Task GetDetailAsync_UnauthorizedUser_ReturnsForbidden()
    {
        var detail = CreateAreaDetail(Guid.NewGuid(), assignedDesignerId: Guid.NewGuid());
        var service = BuildService(new() { Role = "DESIGNER", AreaRepo = new FakeProjectAreaRepository { Detail = detail } });

        var result = await service.GetDetailAsync(detail.ProjectAreaId, Guid.NewGuid());

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task UpdateAsync_AssignedDesigner_UpdatesArea()
    {
        var designerId = Guid.NewGuid();
        var entity = CreateAreaEntity();
        var detail = CreateAreaDetail(entity.ProjectId, entity.ProjectAreaId, assignedDesignerId: designerId);
        var areaRepo = new FakeProjectAreaRepository { Detail = detail, EntityById = entity };
        var service = BuildService(new() { Role = "DESIGNER", AreaRepo = areaRepo });

        var result = await service.UpdateAsync(entity.ProjectAreaId, designerId, new UpdateProjectAreaRequestDto
        {
            AreaName = "Updated Area",
            Status = ProjectAreaStatus.VERIFIED
        });

        Assert.Equal(200, result.Status);
        Assert.Equal("Updated Area", entity.AreaName);
        Assert.Equal(ProjectAreaStatus.VERIFIED, entity.Status);
    }

    [Fact]
    public async Task UpdateAsync_Customer_ReturnsForbidden()
    {
        var detail = CreateAreaDetail(Guid.NewGuid());
        var service = BuildService(new() { Role = "CUSTOMER", AreaRepo = new FakeProjectAreaRepository { Detail = detail } });

        var result = await service.UpdateAsync(detail.ProjectAreaId, detail.CustomerId, new UpdateProjectAreaRequestDto());

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task UpdateAsync_InvalidParentArea_ReturnsInvalidParentArea()
    {
        var salesId = Guid.NewGuid();
        var entity = CreateAreaEntity();
        var detail = CreateAreaDetail(entity.ProjectId, entity.ProjectAreaId, assignedSalesId: salesId);
        var areaRepo = new FakeProjectAreaRepository { Detail = detail, EntityById = entity };
        var service = BuildService(new() { Role = "SALES", AreaRepo = areaRepo });

        var result = await service.UpdateAsync(entity.ProjectAreaId, salesId, new UpdateProjectAreaRequestDto
        {
            ParentAreaId = Guid.NewGuid()
        });

        Assert.Equal(400, result.Status);
        Assert.Equal(ProjectAreaErrorCodes.InvalidParentArea, result.ErrorCode);
    }

    [Fact]
    public async Task CancelAsync_AssignedSales_CancelsUnusedArea()
    {
        var salesId = Guid.NewGuid();
        var entity = CreateAreaEntity();
        var detail = CreateAreaDetail(entity.ProjectId, entity.ProjectAreaId, assignedSalesId: salesId);
        var areaRepo = new FakeProjectAreaRepository { Detail = detail, EntityById = entity };
        var service = BuildService(new() { Role = "SALES", AreaRepo = areaRepo });

        var result = await service.CancelAsync(entity.ProjectAreaId, salesId);

        Assert.Equal(200, result.Status);
        Assert.Equal(ProjectAreaStatus.CANCELLED, entity.Status);
    }

    [Fact]
    public async Task CancelAsync_Customer_ReturnsForbidden()
    {
        var detail = CreateAreaDetail(Guid.NewGuid());
        var service = BuildService(new() { Role = "CUSTOMER", AreaRepo = new FakeProjectAreaRepository { Detail = detail } });

        var result = await service.CancelAsync(detail.ProjectAreaId, detail.CustomerId);

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task CancelAsync_AreaUsedByScene_ReturnsProjectAreaInUseByScene()
    {
        var salesId = Guid.NewGuid();
        var entity = CreateAreaEntity();
        var detail = CreateAreaDetail(entity.ProjectId, entity.ProjectAreaId, assignedSalesId: salesId);
        var areaRepo = new FakeProjectAreaRepository
        {
            Detail = detail,
            EntityById = entity,
            HasActiveSceneUsage = true
        };
        var service = BuildService(new() { Role = "SALES", AreaRepo = areaRepo });

        var result = await service.CancelAsync(entity.ProjectAreaId, salesId);

        Assert.Equal(400, result.Status);
        Assert.Equal(ProjectAreaErrorCodes.ProjectAreaInUseByScene, result.ErrorCode);
        Assert.Equal(ProjectAreaStatus.DRAFT, entity.Status);
    }

    [Fact]
    public async Task CancelAsync_AreaUsedByProposalItem_ReturnsProjectAreaInUseByProposalItem()
    {
        var salesId = Guid.NewGuid();
        var entity = CreateAreaEntity();
        var detail = CreateAreaDetail(entity.ProjectId, entity.ProjectAreaId, assignedSalesId: salesId);
        var areaRepo = new FakeProjectAreaRepository
        {
            Detail = detail,
            EntityById = entity,
            HasActiveProposalItemUsage = true
        };
        var service = BuildService(new() { Role = "SALES", AreaRepo = areaRepo });

        var result = await service.CancelAsync(entity.ProjectAreaId, salesId);

        Assert.Equal(400, result.Status);
        Assert.Equal(ProjectAreaErrorCodes.ProjectAreaInUseByProposalItem, result.ErrorCode);
        Assert.Equal(ProjectAreaStatus.DRAFT, entity.Status);
    }

    [Fact]
    public async Task CreateAsync_Admin_CreatesProjectArea()
    {
        var adminId = Guid.NewGuid();
        var project = CreateProject();
        var areaRepo = new FakeProjectAreaRepository();
        var service = BuildService(new() { Role = "ADMIN", ProjectDetail = project, AreaRepo = areaRepo });

        var result = await service.CreateAsync(project.ProjectId, adminId, ValidCreateRequest());

        Assert.Equal(201, result.Status);
        Assert.Equal(1, areaRepo.AddCallCount);
    }

    [Fact]
    public async Task CreateAsync_ProjectNotFound_ReturnsNotFound()
    {
        var service = BuildService(new() { Role = "ADMIN" });

        var result = await service.CreateAsync(Guid.NewGuid(), Guid.NewGuid(), ValidCreateRequest());

        Assert.Equal(404, result.Status);
    }

    [Fact]
    public async Task CreateAsync_EmptyProjectId_ReturnsBadRequest()
    {
        var service = BuildService(new() { Role = "ADMIN", ProjectDetail = CreateProject() });

        var result = await service.CreateAsync(Guid.Empty, Guid.NewGuid(), ValidCreateRequest());

        Assert.Equal(400, result.Status);
    }

    [Fact]
    public async Task CreateAsync_MissingAreaName_ReturnsBadRequest()
    {
        var salesId = Guid.NewGuid();
        var project = CreateProject(assignedSalesId: salesId);
        var service = BuildService(new() { Role = "SALES", ProjectDetail = project });
        var request = ValidCreateRequest();
        request.AreaName = "   ";

        var result = await service.CreateAsync(project.ProjectId, salesId, request);

        Assert.Equal(400, result.Status);
    }

    [Fact]
    public async Task CreateAsync_MissingAreaType_ReturnsBadRequest()
    {
        var salesId = Guid.NewGuid();
        var project = CreateProject(assignedSalesId: salesId);
        var service = BuildService(new() { Role = "SALES", ProjectDetail = project });
        var request = ValidCreateRequest();
        request.AreaType = null;

        var result = await service.CreateAsync(project.ProjectId, salesId, request);

        Assert.Equal(400, result.Status);
    }

    [Fact]
    public async Task UpdateAsync_SelfParent_ReturnsInvalidParentArea()
    {
        var salesId = Guid.NewGuid();
        var entity = CreateAreaEntity();
        var detail = CreateAreaDetail(entity.ProjectId, entity.ProjectAreaId, assignedSalesId: salesId);
        var service = BuildService(new()
        {
            Role = "SALES",
            AreaRepo = new FakeProjectAreaRepository { Detail = detail, EntityById = entity }
        });

        var result = await service.UpdateAsync(entity.ProjectAreaId, salesId, new UpdateProjectAreaRequestDto
        {
            ParentAreaId = entity.ProjectAreaId
        });

        Assert.Equal(400, result.Status);
        Assert.Equal(ProjectAreaErrorCodes.InvalidParentArea, result.ErrorCode);
    }

    [Fact]
    public async Task GetListByProjectAsync_ProjectNotFound_ReturnsNotFound()
    {
        var service = BuildService(new() { Role = "ADMIN" });

        var result = await service.GetListByProjectAsync(Guid.NewGuid(), Guid.NewGuid(), includeCancelled: false);

        Assert.Equal(404, result.Status);
    }

    [Fact]
    public async Task GetListByProjectAsync_EmptyUserId_ReturnsUnauthorized()
    {
        var service = BuildService(new() { Role = "ADMIN", ProjectDetail = CreateProject() });

        var result = await service.GetListByProjectAsync(Guid.NewGuid(), Guid.Empty, includeCancelled: false);

        Assert.Equal(401, result.Status);
    }

    [Fact]
    public async Task GetDetailAsync_NotFound_ReturnsNotFound()
    {
        var service = BuildService(new() { Role = "ADMIN" });

        var result = await service.GetDetailAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(404, result.Status);
        Assert.Equal(ProjectAreaErrorCodes.ProjectAreaNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task UpdateAsync_NotFound_ReturnsNotFound()
    {
        var service = BuildService(new() { Role = "ADMIN" });

        var result = await service.UpdateAsync(Guid.NewGuid(), Guid.NewGuid(), new UpdateProjectAreaRequestDto());

        Assert.Equal(404, result.Status);
    }

    [Fact]
    public async Task UpdateAsync_NegativeDimension_ReturnsInvalidAreaDimension()
    {
        var salesId = Guid.NewGuid();
        var entity = CreateAreaEntity();
        var detail = CreateAreaDetail(entity.ProjectId, entity.ProjectAreaId, assignedSalesId: salesId);
        var service = BuildService(new()
        {
            Role = "SALES",
            AreaRepo = new FakeProjectAreaRepository { Detail = detail, EntityById = entity }
        });

        var result = await service.UpdateAsync(entity.ProjectAreaId, salesId, new UpdateProjectAreaRequestDto
        {
            Height = -2
        });

        Assert.Equal(400, result.Status);
        Assert.Equal(ProjectAreaErrorCodes.InvalidAreaDimension, result.ErrorCode);
    }

    [Fact]
    public async Task UpdateAsync_SpecialToStandardWithoutStandardDimensions_ReturnsInvalidAreaDimension()
    {
        var salesId = Guid.NewGuid();
        var entity = CreateAreaEntity();
        entity.IsSpecialLayout = true;
        entity.Width = null;
        entity.Length = null;
        var detail = CreateAreaDetail(entity.ProjectId, entity.ProjectAreaId, assignedSalesId: salesId);
        detail.IsSpecialLayout = true;
        detail.Width = null;
        detail.Length = null;
        var service = BuildService(new()
        {
            Role = "SALES",
            AreaRepo = new FakeProjectAreaRepository { Detail = detail, EntityById = entity }
        });

        var result = await service.UpdateAsync(entity.ProjectAreaId, salesId, new UpdateProjectAreaRequestDto
        {
            IsSpecialLayout = false
        });

        Assert.Equal(400, result.Status);
        Assert.Equal(ProjectAreaErrorCodes.InvalidAreaDimension, result.ErrorCode);
    }

    [Fact]
    public async Task UpdateAsync_SpecialToStandardWithValidDimensions_UpdatesArea()
    {
        var salesId = Guid.NewGuid();
        var entity = CreateAreaEntity();
        entity.IsSpecialLayout = true;
        entity.Width = null;
        entity.Length = null;
        var detail = CreateAreaDetail(entity.ProjectId, entity.ProjectAreaId, assignedSalesId: salesId);
        detail.IsSpecialLayout = true;
        detail.Width = null;
        detail.Length = null;
        var service = BuildService(new()
        {
            Role = "SALES",
            AreaRepo = new FakeProjectAreaRepository { Detail = detail, EntityById = entity }
        });

        var result = await service.UpdateAsync(entity.ProjectAreaId, salesId, new UpdateProjectAreaRequestDto
        {
            IsSpecialLayout = false,
            AreaSqm = 20m,
            Width = 5m,
            Length = 4m,
            Height = 3m
        });

        Assert.Equal(200, result.Status);
        Assert.False(entity.IsSpecialLayout);
        Assert.Equal(20m, entity.AreaSqm);
    }

    [Fact]
    public async Task CancelAsync_AlreadyCancelled_ReturnsProjectAreaAlreadyCancelled()
    {
        var salesId = Guid.NewGuid();
        var entity = CreateAreaEntity();
        entity.Status = ProjectAreaStatus.CANCELLED;
        var detail = CreateAreaDetail(entity.ProjectId, entity.ProjectAreaId, assignedSalesId: salesId);
        detail.Status = ProjectAreaStatus.CANCELLED;
        var service = BuildService(new()
        {
            Role = "SALES",
            AreaRepo = new FakeProjectAreaRepository { Detail = detail, EntityById = entity }
        });

        var result = await service.CancelAsync(entity.ProjectAreaId, salesId);

        Assert.Equal(400, result.Status);
        Assert.Equal(ProjectAreaErrorCodes.ProjectAreaAlreadyCancelled, result.ErrorCode);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task GetListByProjectAsync_UnassignedSalesWithoutDesigner_ReturnsSuccess()
    {
        var salesId = Guid.NewGuid();
        var project = CreateProject(assignedSalesId: null, assignedDesignerId: null);
        var service = BuildService(new() { Role = "SALES", ProjectDetail = project });

        var result = await service.GetListByProjectAsync(project.ProjectId, salesId, includeCancelled: false);

        Assert.Equal(200, result.Status);
    }

    private sealed class AreaServiceTestOptions
    {
        public string? Role { get; init; }
        public ProjectDetailReadModel? ProjectDetail { get; init; }
        public FakeProjectAreaRepository? AreaRepo { get; init; }
    }

    private static ProjectAreaService BuildService(AreaServiceTestOptions? options = null)
    {
        options ??= new AreaServiceTestOptions();
        var areaRepo = options.AreaRepo ?? new FakeProjectAreaRepository();
        var projectRepo = new FakeProjectRepository(options.Role, options.ProjectDetail);
        return new ProjectAreaService(
            areaRepo,
            projectRepo,
            TestUnitOfWork.ForSaveChanges(areaRepo.SaveChangesAsync));
    }

    private static CreateProjectAreaRequestDto ValidCreateRequest() => new()
    {
        AreaName = "Main Cafe Area",
        AreaType = ProjectAreaType.ZONE,
        FloorNumber = 1,
        Description = "Main seating area",
        AreaSqm = 45.5m,
        Width = 6.5m,
        Length = 7.0m,
        Height = 3.2m,
        Status = ProjectAreaStatus.DRAFT
    };

    private static ProjectDetailReadModel CreateProject(
        Guid? customerId = null,
        Guid? assignedSalesId = null,
        Guid? assignedDesignerId = null)
    {
        return new ProjectDetailReadModel
        {
            ProjectId = Guid.NewGuid(),
            CustomerId = customerId ?? Guid.NewGuid(),
            AssignedSalesId = assignedSalesId,
            AssignedDesignerId = assignedDesignerId,
            ProjectName = "Test Project"
        };
    }

    private static ProjectArea CreateAreaEntity(Guid? projectId = null, Guid? projectAreaId = null)
    {
        return new ProjectArea
        {
            ProjectAreaId = projectAreaId ?? Guid.NewGuid(),
            ProjectId = projectId ?? Guid.NewGuid(),
            AreaName = "Main Cafe Area",
            AreaType = ProjectAreaType.ZONE,
            AreaSqm = 45.5m,
            Width = 6.5m,
            Length = 7.0m,
            Height = 3.2m,
            Status = ProjectAreaStatus.DRAFT,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static ProjectAreaDetailReadModel CreateAreaDetail(
        Guid projectId,
        Guid? projectAreaId = null,
        Guid? customerId = null,
        Guid? assignedSalesId = null,
        Guid? assignedDesignerId = null)
    {
        return new ProjectAreaDetailReadModel
        {
            ProjectAreaId = projectAreaId ?? Guid.NewGuid(),
            ProjectId = projectId,
            CustomerId = customerId ?? Guid.NewGuid(),
            AssignedSalesId = assignedSalesId,
            AssignedDesignerId = assignedDesignerId,
            AreaName = "Main Cafe Area",
            AreaType = ProjectAreaType.ZONE,
            AreaSqm = 45.5m,
            Width = 6.5m,
            Length = 7.0m,
            Height = 3.2m,
            Status = ProjectAreaStatus.DRAFT,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private sealed class FakeProjectAreaRepository : IProjectAreaRepository
    {
        public ProjectAreaDetailReadModel? Detail { get; set; }
        public ProjectArea? EntityById { get; set; }
        public IReadOnlyList<ProjectAreaDetailReadModel> ListItems { get; set; } = [];
        public bool HasActiveSceneUsage { get; set; }
        public bool HasActiveProposalItemUsage { get; set; }
        public int AddCallCount { get; private set; }

        public Task<ProjectAreaDetailReadModel?> GetDetailAsync(
            Guid projectAreaId,
            CancellationToken cancellationToken = default)
        {
            if (Detail?.ProjectAreaId == projectAreaId)
            {
                return Task.FromResult<ProjectAreaDetailReadModel?>(Detail);
            }

            return Task.FromResult<ProjectAreaDetailReadModel?>(null);
        }

        public Task<IReadOnlyList<ProjectAreaDetailReadModel>> GetListByProjectAsync(
            Guid projectId,
            bool includeCancelled,
            CancellationToken cancellationToken = default)
        {
            var items = ListItems
                .Where(item => item.ProjectId == projectId)
                .Where(item => includeCancelled || item.Status != ProjectAreaStatus.CANCELLED)
                .ToList();

            return Task.FromResult<IReadOnlyList<ProjectAreaDetailReadModel>>(items);
        }

        public Task<bool> BelongsToProjectAsync(
            Guid projectAreaId,
            Guid projectId,
            CancellationToken cancellationToken = default)
        {
            if (Detail?.ProjectAreaId == projectAreaId && Detail.ProjectId == projectId)
            {
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }

        public Task<bool> HasActiveUsageAsync(
            Guid projectAreaId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(HasActiveSceneUsage || HasActiveProposalItemUsage);

        public Task<bool> HasActiveSceneUsageAsync(
            Guid projectAreaId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(HasActiveSceneUsage);

        public Task<bool> HasActiveProposalItemUsageAsync(
            Guid projectAreaId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(HasActiveProposalItemUsage);

        public IQueryable<ProjectArea> Query() => Enumerable.Empty<ProjectArea>().AsQueryable();

        public Task<ProjectArea?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            if (EntityById?.ProjectAreaId == id)
            {
                return Task.FromResult<ProjectArea?>(EntityById);
            }

            return Task.FromResult<ProjectArea?>(null);
        }

        public Task<IReadOnlyList<ProjectArea>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProjectArea>>([]);

        public Task AddAsync(ProjectArea entity, CancellationToken cancellationToken = default)
        {
            AddCallCount++;
            EntityById = entity;
            Detail = new ProjectAreaDetailReadModel
            {
                ProjectAreaId = entity.ProjectAreaId,
                ProjectId = entity.ProjectId,
                AreaName = entity.AreaName,
                AreaType = entity.AreaType,
                IsSpecialLayout = entity.IsSpecialLayout,
                AreaSqm = entity.AreaSqm,
                Width = entity.Width,
                Length = entity.Length,
                Height = entity.Height,
                Status = entity.Status,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt
            };
            return Task.CompletedTask;
        }

        public Task AddRangeAsync(IEnumerable<ProjectArea> entities, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public void Update(ProjectArea entity) { }

        public void Remove(ProjectArea entity) { }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(1);
    }

    private sealed class FakeProjectRepository : IProjectRepository
    {
        private readonly string? _role;
        private readonly ProjectDetailReadModel? _detail;

        public FakeProjectRepository(string? role, ProjectDetailReadModel? detail)
        {
            _role = role;
            _detail = detail;
        }

        public Task<string?> GetAccountRoleNameAsync(Guid accountId, CancellationToken cancellationToken = default)
            => Task.FromResult(_role);

        public Task<ProjectDetailReadModel?> GetDetailAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            if (_detail?.ProjectId == projectId)
            {
                return Task.FromResult<ProjectDetailReadModel?>(_detail);
            }

            return Task.FromResult<ProjectDetailReadModel?>(null);
        }

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

        public void Update(Project entity) { }

        public void Remove(Project entity) { }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(0);
    }
}
