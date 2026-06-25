#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.DTOs.Proposals;
using FurniSpace.Application.Services.Proposals;
using FurniSpace.Application.Tests.TestDoubles;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.DTOs.Products;
using FurniSpace.Infrastructure.DTOs.Projects;
using FurniSpace.Infrastructure.DTOs.Proposals;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Xunit;

namespace FurniSpace.Application.Tests.Proposals;

public sealed class ProposalServiceTests
{
    [Fact]
    public async Task CreateAsync_WithAssignedDesignerAndDraftingProject_CreatesDraftProposal()
    {
        var projectId = Guid.NewGuid();
        var designerId = Guid.NewGuid();
        var repository = new FakeProposalRepository(
            project: CreateProjectAccess(projectId, assignedDesignerId: designerId),
            proposalCount: 2);
        var projects = new FakeProjectRepository("DESIGNER");
        var service = CreateService(repository, projects);

        var result = await service.CreateAsync(projectId, designerId, new CreateProposalRequestDto
        {
            ProposalName = " Cafe Proposal V3 ",
            Description = " Initial proposal "
        });

        Assert.Equal(201, result.Status);
        Assert.Equal("Proposal created successfully.", result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal(projectId, result.Data.ProjectId);
        Assert.Equal("Cafe Proposal V3", result.Data.ProposalName);
        Assert.Equal("Initial proposal", result.Data.Description);
        Assert.Equal(3, result.Data.VersionNo);
        Assert.Equal(ProposalStatus.DRAFT, result.Data.Status);
        Assert.Single(repository.Proposals);
        Assert.Equal(designerId, repository.Proposals[0].CreatedBy);
        Assert.Equal(1, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task CreateAsync_WithProjectWithoutDesigner_ReturnsDesignerNotAssigned()
    {
        var projectId = Guid.NewGuid();
        var service = CreateService(new FakeProposalRepository(
            project: CreateProjectAccess(projectId, assignedDesignerId: null)));

        var result = await service.CreateAsync(projectId, Guid.NewGuid(), ValidCreateRequest());

        Assert.Equal(400, result.Status);
        Assert.Equal("DESIGNER_NOT_ASSIGNED", result.ErrorCode);
        Assert.Equal("Project must have an assigned designer before creating a proposal.", result.Message);
    }

    [Fact]
    public async Task CreateAsync_WithInvalidProjectStatus_ReturnsInvalidProjectStatus()
    {
        var project = CreateProjectAccess(Guid.NewGuid(), assignedDesignerId: Guid.NewGuid());
        project.ProjectStatus = ProjectStatus.SPACE_VERIFIED;
        var service = CreateService(new FakeProposalRepository(project: project));

        var result = await service.CreateAsync(project.ProjectId, project.AssignedDesignerId!.Value, ValidCreateRequest());

        Assert.Equal(400, result.Status);
        Assert.Equal("INVALID_PROJECT_STATUS", result.ErrorCode);
    }

    [Fact]
    public async Task CreateAsync_WithUnassignedDesigner_ReturnsForbidden()
    {
        var project = CreateProjectAccess(Guid.NewGuid(), assignedDesignerId: Guid.NewGuid());
        var repository = new FakeProposalRepository(project: project);
        var service = CreateService(repository, new FakeProjectRepository("DESIGNER"));

        var result = await service.CreateAsync(project.ProjectId, Guid.NewGuid(), ValidCreateRequest());

        Assert.Equal(403, result.Status);
        Assert.Empty(repository.Proposals);
    }

    [Fact]
    public async Task CreateAsync_WithInvalidInput_ReturnsValidationErrors()
    {
        var service = CreateService(new FakeProposalRepository());

        var result = await service.CreateAsync(Guid.NewGuid(), Guid.NewGuid(), new CreateProposalRequestDto
        {
            ProposalName = " ",
            Description = new string('D', 1001)
        });

        Assert.Equal(400, result.Status);
        Assert.NotNull(result.Errors);
        Assert.Contains("Proposal name is required.", result.Errors);
        Assert.Contains("Proposal description must not exceed 1000 characters.", result.Errors);
    }

    [Fact]
    public async Task GetListByProjectAsync_WithCustomer_SetsCustomerVisibleFilter()
    {
        var customerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var repository = new FakeProposalRepository(
            project: CreateProjectAccess(projectId, customerId: customerId),
            listItems:
            [
                new ProposalReadModel
                {
                    ProposalId = Guid.NewGuid(),
                    ProjectId = projectId,
                    ProposalName = "Published",
                    Status = ProposalStatus.PUBLISHED
                }
            ]);
        var service = CreateService(repository, new FakeProjectRepository("CUSTOMER"));

        var result = await service.GetListByProjectAsync(
            projectId,
            customerId,
            new ProposalListQueryDto { Status = ProposalStatus.PUBLISHED, Page = 2, Limit = 5 });

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data.Items);
        Assert.Equal(2, result.Data.Page);
        Assert.Equal(5, result.Data.Limit);
        Assert.Equal(1, result.Data.Total);
        Assert.NotNull(repository.LastListQuery);
        Assert.True(repository.LastListQuery.CustomerVisibleOnly);
        Assert.Equal(ProposalStatus.PUBLISHED, repository.LastListQuery.Status);
    }

    [Fact]
    public async Task GetListByProjectAsync_WithUnauthorizedUser_ReturnsForbidden()
    {
        var project = CreateProjectAccess(Guid.NewGuid(), assignedSalesId: Guid.NewGuid());
        var service = CreateService(
            new FakeProposalRepository(project: project),
            new FakeProjectRepository("SALES"));

        var result = await service.GetListByProjectAsync(
            project.ProjectId,
            Guid.NewGuid(),
            new ProposalListQueryDto());

        Assert.Equal(403, result.Status);
    }

    [Theory]
    [InlineData(0, 20, "Page must be greater than zero.")]
    [InlineData(1, 101, "Limit must be between 1 and 100.")]
    public async Task GetListByProjectAsync_WithInvalidPagination_ReturnsBadRequest(
        int page,
        int limit,
        string expectedMessage)
    {
        var service = CreateService(new FakeProposalRepository());

        var result = await service.GetListByProjectAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new ProposalListQueryDto { Page = page, Limit = limit });

        Assert.Equal(400, result.Status);
        Assert.Equal(expectedMessage, result.Message);
    }

    [Fact]
    public async Task CreateSceneAsync_WithDraftProposal_CreatesActiveScene()
    {
        var proposalId = Guid.NewGuid();
        var designerId = Guid.NewGuid();
        var repository = new FakeProposalRepository(
            context: CreateProposalContext(proposalId, assignedDesignerId: designerId),
            sceneCount: 1);
        var service = CreateService(repository, new FakeProjectRepository("DESIGNER"));

        var result = await service.CreateSceneAsync(proposalId, designerId, new CreateProposalSceneRequestDto
        {
            SceneName = " Main Layout ",
            SceneType = ProposalSceneType.THREE_D,
            MongoSceneId = " mongo-id ",
            PreviewFileId = Guid.NewGuid()
        });

        Assert.Equal(201, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal("Main Layout", result.Data.SceneName);
        Assert.Equal("mongo-id", result.Data.MongoSceneId);
        Assert.Equal(2, result.Data.VersionNo);
        Assert.True(result.Data.IsActive);
        Assert.Single(repository.Scenes);
        Assert.Equal(1, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task CreateSceneAsync_WithPublishedProposal_ReturnsInvalidProposalStatus()
    {
        var context = CreateProposalContext(Guid.NewGuid(), assignedDesignerId: Guid.NewGuid());
        context.ProposalStatus = ProposalStatus.PUBLISHED;
        var service = CreateService(new FakeProposalRepository(context: context), new FakeProjectRepository("DESIGNER"));

        var result = await service.CreateSceneAsync(
            context.ProposalId,
            context.AssignedDesignerId!.Value,
            ValidCreateSceneRequest());

        Assert.Equal(400, result.Status);
        Assert.Equal("INVALID_PROPOSAL_STATUS", result.ErrorCode);
    }

    [Fact]
    public async Task CreateSceneAsync_WithMissingSceneFields_ReturnsValidationErrors()
    {
        var service = CreateService(new FakeProposalRepository());

        var result = await service.CreateSceneAsync(Guid.NewGuid(), Guid.NewGuid(), new CreateProposalSceneRequestDto());

        Assert.Equal(400, result.Status);
        Assert.NotNull(result.Errors);
        Assert.Contains("Scene name is required.", result.Errors);
        Assert.Contains("Scene type is required.", result.Errors);
    }

    [Fact]
    public async Task GetDetailAsync_WithAssignedSales_ReturnsScenesAndItems()
    {
        var salesId = Guid.NewGuid();
        var proposalId = Guid.NewGuid();
        var repository = new FakeProposalRepository(
            detail: CreateDetail(proposalId, assignedSalesId: salesId));
        var service = CreateService(repository, new FakeProjectRepository("SALES"));

        var result = await service.GetDetailAsync(proposalId, salesId);

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(proposalId, result.Data.ProposalId);
        Assert.Single(result.Data.Scenes);
        Assert.Single(result.Data.Items);
        Assert.Equal("Proposal detail retrieved successfully.", result.Message);
    }

    [Fact]
    public async Task GetDetailAsync_WithCustomerDraftProposal_ReturnsForbidden()
    {
        var customerId = Guid.NewGuid();
        var detail = CreateDetail(Guid.NewGuid(), customerId: customerId);
        detail.Status = ProposalStatus.DRAFT;
        var service = CreateService(
            new FakeProposalRepository(detail: detail),
            new FakeProjectRepository("CUSTOMER"));

        var result = await service.GetDetailAsync(detail.ProposalId, customerId);

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task GetDetailAsync_WithMissingProposal_ReturnsProposalNotFound()
    {
        var service = CreateService(new FakeProposalRepository());

        var result = await service.GetDetailAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(404, result.Status);
        Assert.Equal("PROPOSAL_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task GetDetailAsync_WithEmptyUser_ReturnsUnauthorized()
    {
        var service = CreateService(new FakeProposalRepository());

        var result = await service.GetDetailAsync(Guid.NewGuid(), Guid.Empty);

        Assert.Equal(401, result.Status);
        Assert.Equal("Authenticated account id is required.", result.Message);
    }

    private static ProposalService CreateService(
        FakeProposalRepository proposals,
        FakeProjectRepository? projects = null)
    {
        return new ProposalService(
            proposals,
            projects ?? new FakeProjectRepository("ADMIN"),
            TestUnitOfWork.ForSaveChanges(proposals.SaveChangesAsync));
    }

    private static CreateProposalRequestDto ValidCreateRequest()
    {
        return new CreateProposalRequestDto
        {
            ProposalName = "Cafe Interior Design Proposal V1",
            Description = "Initial design proposal."
        };
    }

    private static CreateProposalSceneRequestDto ValidCreateSceneRequest()
    {
        return new CreateProposalSceneRequestDto
        {
            SceneName = "Main cafe layout",
            SceneType = ProposalSceneType.THREE_D
        };
    }

    private static ProposalProjectAccessReadModel CreateProjectAccess(
        Guid projectId,
        Guid? customerId = null,
        Guid? assignedSalesId = null,
        Guid? assignedDesignerId = null)
    {
        return new ProposalProjectAccessReadModel
        {
            ProjectId = projectId,
            CustomerId = customerId ?? Guid.NewGuid(),
            AssignedSalesId = assignedSalesId,
            AssignedDesignerId = assignedDesignerId,
            ProjectStatus = ProjectStatus.PROPOSAL_DRAFTING
        };
    }

    private static ProposalContextReadModel CreateProposalContext(
        Guid proposalId,
        Guid? assignedSalesId = null,
        Guid? assignedDesignerId = null)
    {
        return new ProposalContextReadModel
        {
            ProposalId = proposalId,
            ProjectId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            AssignedSalesId = assignedSalesId,
            AssignedDesignerId = assignedDesignerId,
            ProposalStatus = ProposalStatus.DRAFT
        };
    }

    private static ProposalDetailReadModel CreateDetail(
        Guid proposalId,
        Guid? customerId = null,
        Guid? assignedSalesId = null,
        Guid? assignedDesignerId = null)
    {
        return new ProposalDetailReadModel
        {
            ProposalId = proposalId,
            ProjectId = Guid.NewGuid(),
            CustomerId = customerId ?? Guid.NewGuid(),
            AssignedSalesId = assignedSalesId,
            AssignedDesignerId = assignedDesignerId,
            ProposalName = "Cafe Interior Design Proposal V1",
            Description = "Initial design proposal.",
            VersionNo = 1,
            Status = ProposalStatus.PUBLISHED,
            Scenes =
            [
                new ProposalSceneReadModel
                {
                    SceneId = Guid.NewGuid(),
                    ProposalId = proposalId,
                    SceneName = "Main layout",
                    SceneType = ProposalSceneType.THREE_D,
                    MongoSceneId = "mongo-id",
                    IsActive = true
                }
            ],
            Items =
            [
                new ProposalItemReadModel
                {
                    ProposalItemId = Guid.NewGuid(),
                    ProductNameSnapshot = "Cafe Chair",
                    Quantity = 4,
                    UnitPriceSnapshot = 1200000,
                    SubtotalAmount = 4800000
                }
            ]
        };
    }

    private sealed class FakeProposalRepository : IProposalRepository
    {
        private readonly ProposalProjectAccessReadModel? _project;
        private readonly ProposalContextReadModel? _context;
        private readonly ProposalDetailReadModel? _detail;
        private readonly IReadOnlyList<ProposalReadModel> _listItems;
        private readonly int _proposalCount;
        private readonly int _sceneCount;

        public FakeProposalRepository(
            ProposalProjectAccessReadModel? project = null,
            ProposalContextReadModel? context = null,
            ProposalDetailReadModel? detail = null,
            IReadOnlyList<ProposalReadModel>? listItems = null,
            int proposalCount = 0,
            int sceneCount = 0)
        {
            _project = project;
            _context = context;
            _detail = detail;
            _listItems = listItems ?? [];
            _proposalCount = proposalCount;
            _sceneCount = sceneCount;
        }

        public List<Proposal> Proposals { get; } = [];
        public List<ProposalScene> Scenes { get; } = [];
        public int SaveChangesCallCount { get; private set; }
        public ProposalListQueryReadModel? LastListQuery { get; private set; }

        public Task<ProposalProjectAccessReadModel?> GetProjectAccessAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_project?.ProjectId == projectId ? _project : null);
        }

        public Task<ProposalContextReadModel?> GetProposalContextAsync(Guid proposalId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_context?.ProposalId == proposalId ? _context : null);
        }

        public Task<int> CountByProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_proposalCount);
        }

        public Task<IReadOnlyList<ProposalReadModel>> GetListAsync(
            ProposalListQueryReadModel query,
            CancellationToken cancellationToken = default)
        {
            LastListQuery = query;
            return Task.FromResult(_listItems);
        }

        public Task<int> CountListAsync(ProposalListQueryReadModel query, CancellationToken cancellationToken = default)
        {
            LastListQuery = query;
            return Task.FromResult(_listItems.Count);
        }

        public Task<int> CountScenesAsync(Guid proposalId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_sceneCount);
        }

        public Task AddSceneAsync(ProposalScene scene, CancellationToken cancellationToken = default)
        {
            Scenes.Add(scene);
            return Task.CompletedTask;
        }

        public Task<ProposalDetailReadModel?> GetDetailAsync(Guid proposalId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_detail?.ProposalId == proposalId ? _detail : null);
        }

        public IQueryable<Proposal> Query() => Proposals.AsQueryable();
        public Task<Proposal?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Proposals.FirstOrDefault(proposal => proposal.ProposalId == id));
        public Task<IReadOnlyList<Proposal>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Proposal>>(Proposals);
        public Task AddAsync(Proposal entity, CancellationToken cancellationToken = default)
        {
            Proposals.Add(entity);
            return Task.CompletedTask;
        }
        public Task AddRangeAsync(IEnumerable<Proposal> entities, CancellationToken cancellationToken = default)
        {
            Proposals.AddRange(entities);
            return Task.CompletedTask;
        }
        public void Update(Proposal entity) { }
        public void Remove(Proposal entity) => Proposals.Remove(entity);
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCallCount++;
            return Task.FromResult(1);
        }
    }

    private sealed class FakeProjectRepository : IProjectRepository
    {
        private readonly string? _roleName;

        public FakeProjectRepository(string? roleName)
        {
            _roleName = roleName;
        }

        public Task<string?> GetAccountRoleNameAsync(Guid accountId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_roleName);
        public Task<string?> GetAccountFullNameAsync(Guid accountId, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);
        public Task<IReadOnlyList<Guid>> GetActiveAccountIdsByRoleNamesAsync(IReadOnlyCollection<string> roleNames, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);
        public Task<int> CountSubmittedInYearAsync(int year, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);
        public Task<ProjectDetailReadModel?> GetDetailAsync(Guid projectId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ProjectDetailReadModel?>(null);
        public Task<DesignerAccountReadModel?> GetActiveDesignerAsync(Guid designerId, CancellationToken cancellationToken = default) =>
            Task.FromResult<DesignerAccountReadModel?>(null);
        public Task<IReadOnlyList<ProjectListItemReadModel>> GetListAsync(ProjectListQueryReadModel query, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ProjectListItemReadModel>>([]);
        public Task<int> CountAsync(ProjectListQueryReadModel query, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);
        public Task<IReadOnlyList<ProjectByUserItemReadModel>> GetByUserAsync(
            ProjectByUserQueryReadModel query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ProjectByUserItemReadModel>>([]);
        public Task<int> CountByUserAsync(ProjectByUserQueryReadModel query, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);
        public IQueryable<Project> Query() => Array.Empty<Project>().AsQueryable();
        public Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Project?>(null);
        public Task<IReadOnlyList<Project>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Project>>([]);
        public Task AddAsync(Project entity, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
        public Task AddRangeAsync(IEnumerable<Project> entities, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
        public void Update(Project entity) { }
        public void Remove(Project entity) { }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(0);
    }
}
