#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.DTOs.Proposals;
using FurniSpace.Application.Common.Notifications;
using FurniSpace.Application.Interfaces.Notifications;
using FurniSpace.Application.Services.Proposals;
using FurniSpace.Application.Tests.TestDoubles;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.ReadModels.Products;
using FurniSpace.Infrastructure.ReadModels.Projects;
using FurniSpace.Infrastructure.ReadModels.Proposals;
using FurniSpace.Infrastructure.Persistence;
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

    [Fact]
    public async Task SyncItemsFromSceneAsync_WithDraftProposal_AddsProposalItems()
    {
        var proposalId = Guid.NewGuid();
        var sceneId = Guid.NewGuid();
        var designerId = Guid.NewGuid();
        var productVersionId = Guid.NewGuid();
        var context = CreateProposalContext(proposalId, assignedDesignerId: designerId);
        var repository = new FakeProposalRepository(
            context: context,
            sceneContext: new ProposalSceneContextReadModel
            {
                ProposalId = proposalId,
                SceneId = sceneId,
                ProjectId = context.ProjectId,
                ProjectAreaId = Guid.NewGuid(),
                ProposalStatus = ProposalStatus.DRAFT,
                AssignedDesignerId = designerId
            });
        var productVersions = new FakeProductVersionRepository();
        productVersions.ProductVersions.Add(new ProductVersionDetailReadModel
        {
            ProductVersionId = productVersionId,
            ProductId = Guid.NewGuid(),
            ProductName = "Cafe Chair",
            VersionName = "Brown Wood",
            VersionType = ProductVersionType.STANDARD,
            EstimatedPrice = 1200000m,
            Status = ProductStatus.ACTIVE
        });
        var beginCount = 0;
        var commitCount = 0;
        var unitOfWork = TestUnitOfWork.ForTransaction(
            _ => { beginCount++; return Task.CompletedTask; },
            repository.SaveChangesAsync,
            _ => { commitCount++; return Task.CompletedTask; },
            _ => Task.CompletedTask);
        var service = CreateService(
            repository,
            new FakeProjectRepository("DESIGNER"),
            productVersions,
            unitOfWork);

        var result = await service.SyncItemsFromSceneAsync(proposalId, designerId, new SyncProposalItemsFromSceneRequestDto
        {
            SceneId = sceneId,
            Items =
            [
                new SyncProposalItemFromSceneDto
                {
                    SceneObjectId = "chair-001",
                    ProductVersionId = productVersionId,
                    Quantity = 4,
                    CustomizationNote = "Use brown wood version."
                }
            ]
        });

        Assert.Equal(200, result.Status);
        Assert.Equal("Proposal items synced from scene successfully.", result.Message);
        Assert.Single(repository.Items);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data.Items);
        Assert.Equal("chair-001", result.Data.Items[0].SceneObjectId);
        Assert.Equal("Cafe Chair", result.Data.Items[0].ProductNameSnapshot);
        Assert.Equal("Brown Wood", result.Data.Items[0].VersionNameSnapshot);
        Assert.Equal(4800000m, result.Data.Items[0].SubtotalAmount);
        Assert.Equal(1, beginCount);
        Assert.Equal(1, commitCount);
        Assert.Equal(1, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task SyncItemsFromSceneAsync_WithMissingProductVersion_ReturnsProductVersionNotFound()
    {
        var proposalId = Guid.NewGuid();
        var sceneId = Guid.NewGuid();
        var designerId = Guid.NewGuid();
        var context = CreateProposalContext(proposalId, assignedDesignerId: designerId);
        var repository = new FakeProposalRepository(
            context: context,
            sceneContext: new ProposalSceneContextReadModel
            {
                ProposalId = proposalId,
                SceneId = sceneId,
                ProjectId = context.ProjectId,
                ProposalStatus = ProposalStatus.DRAFT,
                AssignedDesignerId = designerId
            });
        var service = CreateService(repository, new FakeProjectRepository("DESIGNER"));

        var result = await service.SyncItemsFromSceneAsync(proposalId, designerId, new SyncProposalItemsFromSceneRequestDto
        {
            SceneId = sceneId,
            Items = [new SyncProposalItemFromSceneDto { ProductVersionId = Guid.NewGuid(), Quantity = 1 }]
        });

        Assert.Equal(404, result.Status);
        Assert.Equal("PRODUCT_VERSION_NOT_FOUND", result.ErrorCode);
        Assert.Empty(repository.Items);
    }

    [Fact]
    public async Task SyncItemsFromSceneAsync_WithSceneOutsideProposal_ReturnsInvalidScene()
    {
        var proposalId = Guid.NewGuid();
        var designerId = Guid.NewGuid();
        var service = CreateService(
            new FakeProposalRepository(context: CreateProposalContext(proposalId, assignedDesignerId: designerId)),
            new FakeProjectRepository("DESIGNER"));

        var result = await service.SyncItemsFromSceneAsync(proposalId, designerId, new SyncProposalItemsFromSceneRequestDto
        {
            SceneId = Guid.NewGuid(),
            Items = [new SyncProposalItemFromSceneDto { ProductVersionId = Guid.NewGuid(), Quantity = 1 }]
        });

        Assert.Equal(400, result.Status);
        Assert.Equal("INVALID_SCENE", result.ErrorCode);
    }

    [Fact]
    public async Task SelectFinalAsync_WithPublishedProposal_SelectsProposalAndRejectsOtherProposals()
    {
        var customerId = Guid.NewGuid();
        var proposalId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var salesId = Guid.NewGuid();
        var designerId = Guid.NewGuid();
        var detail = CreateDetail(proposalId, customerId: customerId);
        detail.ProjectId = projectId;
        detail.Status = ProposalStatus.PUBLISHED;
        detail.AssignedSalesId = salesId;
        detail.AssignedDesignerId = designerId;
        var selectedProposal = new Proposal
        {
            ProposalId = proposalId,
            ProjectId = projectId,
            ProposalName = "Selected",
            Status = ProposalStatus.PUBLISHED
        };
        var otherProposal = new Proposal
        {
            ProposalId = Guid.NewGuid(),
            ProjectId = projectId,
            ProposalName = "Other",
            Status = ProposalStatus.PUBLISHED
        };
        var project = new Project
        {
            ProjectId = projectId,
            CustomerId = customerId,
            ProjectName = "Cafe",
            Status = ProjectStatus.WAITING_FOR_CUSTOMER_REVIEW
        };
        var repository = new FakeProposalRepository(detail: detail);
        repository.Proposals.AddRange([selectedProposal, otherProposal]);
        var dispatcher = new FakeNotificationDispatcher();
        var service = CreateService(
            repository,
            new FakeProjectRepository("CUSTOMER", project),
            notifications: dispatcher);

        var result = await service.SelectFinalAsync(proposalId, customerId, new SelectFinalProposalRequestDto
        {
            Note = "I confirm this as the final design proposal."
        });

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(ProposalStatus.SELECTED, result.Data.ProposalStatus);
        Assert.Equal(ProjectStatus.PROPOSAL_SELECTED, result.Data.ProjectStatus);
        Assert.Equal(ProposalStatus.SELECTED, selectedProposal.Status);
        Assert.Equal(ProposalStatus.REJECTED, otherProposal.Status);
        Assert.Equal(1, repository.RejectOtherActiveProposalsCallCount);
        Assert.Equal(1, repository.SaveChangesCallCount);
        Assert.Equal(NotificationType.ProposalFinalSelected, dispatcher.LastType);
        Assert.Equal(projectId, dispatcher.LastProjectId);
        Assert.Equal(proposalId, dispatcher.LastReferenceId);
        Assert.Contains(salesId, dispatcher.LastReceiverIds);
        Assert.Contains(designerId, dispatcher.LastReceiverIds);
    }

    [Fact]
    public async Task SelectFinalAsync_WithDifferentCustomer_ReturnsForbidden()
    {
        var proposalId = Guid.NewGuid();
        var detail = CreateDetail(proposalId, customerId: Guid.NewGuid());
        var service = CreateService(
            new FakeProposalRepository(detail: detail),
            new FakeProjectRepository("CUSTOMER"));

        var result = await service.SelectFinalAsync(
            proposalId,
            Guid.NewGuid(),
            new SelectFinalProposalRequestDto());

        Assert.Equal(403, result.Status);
    }

    private static ProposalService CreateService(
        FakeProposalRepository proposals,
        FakeProjectRepository? projects = null,
        FakeProductVersionRepository? productVersions = null,
        IUnitOfWork? unitOfWork = null,
        INotificationDispatcher? notifications = null)
    {
        return new ProposalService(
            proposals,
            projects ?? new FakeProjectRepository("ADMIN"),
            productVersions ?? new FakeProductVersionRepository(),
            unitOfWork ?? TestUnitOfWork.ForSaveChanges(proposals.SaveChangesAsync),
            notifications);
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
        private readonly ProposalSceneContextReadModel? _sceneContext;

        public FakeProposalRepository(
            ProposalProjectAccessReadModel? project = null,
            ProposalContextReadModel? context = null,
            ProposalDetailReadModel? detail = null,
            IReadOnlyList<ProposalReadModel>? listItems = null,
            int proposalCount = 0,
            int sceneCount = 0,
            ProposalSceneContextReadModel? sceneContext = null)
        {
            _project = project;
            _context = context;
            _detail = detail;
            _listItems = listItems ?? [];
            _proposalCount = proposalCount;
            _sceneCount = sceneCount;
            _sceneContext = sceneContext;
        }

        public List<Proposal> Proposals { get; } = [];
        public List<ProposalScene> Scenes { get; } = [];
        public List<ProposalItem> Items { get; } = [];
        public int RejectOtherActiveProposalsCallCount { get; private set; }
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

        public Task<ProposalSceneContextReadModel?> GetSceneContextAsync(Guid proposalId, Guid sceneId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                _sceneContext?.ProposalId == proposalId && _sceneContext.SceneId == sceneId
                    ? _sceneContext
                    : null);
        }

        public Task<IReadOnlyList<ProposalItem>> GetItemsBySceneAsync(Guid proposalId, Guid sceneId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ProposalItem>>(
                Items.Where(item => item.ProposalId == proposalId && item.SceneId == sceneId).ToList());
        }

        public Task AddItemAsync(ProposalItem item, CancellationToken cancellationToken = default)
        {
            Items.Add(item);
            return Task.CompletedTask;
        }

        public Task<Proposal?> GetProposalEntityAsync(Guid proposalId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Proposals.FirstOrDefault(proposal => proposal.ProposalId == proposalId));
        }

        public Task RejectOtherActiveProposalsAsync(Guid projectId, Guid selectedProposalId, DateTime rejectedAt, CancellationToken cancellationToken = default)
        {
            RejectOtherActiveProposalsCallCount++;
            foreach (var proposal in Proposals.Where(proposal =>
                proposal.ProjectId == projectId &&
                proposal.ProposalId != selectedProposalId &&
                proposal.Status != ProposalStatus.ARCHIVED &&
                proposal.Status != ProposalStatus.REJECTED))
            {
                proposal.Status = ProposalStatus.REJECTED;
                proposal.RejectedAt = rejectedAt;
            }

            return Task.CompletedTask;
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
        private readonly Project? _project;

        public FakeProjectRepository(string? roleName, Project? project = null)
        {
            _roleName = roleName;
            _project = project;
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
        public Task<ProjectSearchIndexItemReadModel?> GetSearchIndexItemAsync(Guid projectId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ProjectSearchIndexItemReadModel?>(null);
        public Task<IReadOnlyList<ProjectSearchIndexItemReadModel>> GetSearchIndexPageAsync(int page, int limit, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ProjectSearchIndexItemReadModel>>([]);
        public IQueryable<Project> Query() => Array.Empty<Project>().AsQueryable();
        public Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_project?.ProjectId == id ? _project : null);
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

    private sealed class FakeProductVersionRepository : IProductVersionRepository
    {
        public List<ProductVersionDetailReadModel> ProductVersions { get; } = [];

        public Task<IReadOnlyList<ProductVersionDetailReadModel>> GetValidDetailsAsync(
            IReadOnlyCollection<Guid> productVersionIds,
            Guid projectId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ProductVersionDetailReadModel>>(
                ProductVersions
                    .Where(version => productVersionIds.Contains(version.ProductVersionId))
                    .ToList());
        }

        public IQueryable<ProductVersion> Query() => Array.Empty<ProductVersion>().AsQueryable();
        public Task<ProductVersion?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<ProductVersion?>(null);
        public Task<IReadOnlyList<ProductVersion>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProductVersion>>([]);
        public Task AddAsync(ProductVersion entity, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddRangeAsync(IEnumerable<ProductVersion> entities, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Update(ProductVersion entity) { }
        public void Remove(ProductVersion entity) { }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<bool> VersionCodeExistsAsync(string versionCode, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> ProductExistsAsync(Guid productId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<ProductVersionDetailReadModel?> GetPublicDetailAsync(Guid productVersionId, CancellationToken cancellationToken = default) => Task.FromResult<ProductVersionDetailReadModel?>(null);
        public Task SetDefaultAsync(ProductVersion productVersion, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeNotificationDispatcher : INotificationDispatcher
    {
        public NotificationType? LastType { get; private set; }
        public IReadOnlyDictionary<string, string>? LastParameters { get; private set; }
        public List<Guid> LastReceiverIds { get; } = [];
        public Guid? LastProjectId { get; private set; }
        public string? LastReferenceType { get; private set; }
        public Guid? LastReferenceId { get; private set; }

        public Task DispatchAsync(
            NotificationType type,
            IReadOnlyDictionary<string, string> parameters,
            IEnumerable<Guid> receiverIds,
            Guid? projectId = null,
            string? referenceType = null,
            Guid? referenceId = null,
            CancellationToken cancellationToken = default)
        {
            LastType = type;
            LastParameters = parameters;
            LastReceiverIds.Clear();
            LastReceiverIds.AddRange(receiverIds);
            LastProjectId = projectId;
            LastReferenceType = referenceType;
            LastReferenceId = referenceId;
            return Task.CompletedTask;
        }
    }
}
