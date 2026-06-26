#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.Common.Notifications;
using FurniSpace.Application.DTOs.ProjectSchedules;
using FurniSpace.Application.Interfaces.Notifications;
using FurniSpace.Application.Services.ProjectSchedules;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.DTOs.ProjectSchedules;
using FurniSpace.Infrastructure.DTOs.Projects;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Xunit;

namespace FurniSpace.Application.Tests.ProjectSchedules;

public sealed class ProjectScheduleServiceTests
{
    // ── SCH-01: Create ──────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_ReturnsUnauthorized_WhenUserIdIsEmpty()
    {
        var service = BuildService(role: "SALES");

        var result = await service.CreateAsync(Guid.NewGuid(), Guid.Empty, ValidCreateRequest());

        Assert.Equal(401, result.Status);
    }

    [Fact]
    public async Task CreateAsync_ReturnsBadRequest_WhenProjectIdIsEmpty()
    {
        var service = BuildService(role: "SALES");

        var result = await service.CreateAsync(Guid.Empty, Guid.NewGuid(), ValidCreateRequest());

        Assert.Equal(400, result.Status);
    }

    [Fact]
    public async Task CreateAsync_ReturnsForbidden_WhenRoleIsNotSalesOrAdmin()
    {
        var service = BuildService(role: "CUSTOMER");

        var result = await service.CreateAsync(Guid.NewGuid(), Guid.NewGuid(), ValidCreateRequest());

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task CreateAsync_ReturnsNotFound_WhenProjectDoesNotExist()
    {
        var service = BuildService(role: "SALES", projectDetail: null);

        var result = await service.CreateAsync(Guid.NewGuid(), Guid.NewGuid(), ValidCreateRequest());

        Assert.Equal(404, result.Status);
    }

    [Fact]
    public async Task CreateAsync_ReturnsForbidden_WhenSalesIsNotAssignedToProject()
    {
        var salesId = Guid.NewGuid();
        var project = CreateProject(assignedSalesId: Guid.NewGuid());
        var service = BuildService(role: "SALES", projectDetail: project);

        var result = await service.CreateAsync(project.ProjectId, salesId, ValidCreateRequest());

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task CreateAsync_ReturnsBadRequest_WhenScheduledStartIsInThePast()
    {
        var salesId = Guid.NewGuid();
        var project = CreateProject(assignedSalesId: salesId);
        var service = BuildService(role: "SALES", projectDetail: project);
        var request = ValidCreateRequest();
        request.ScheduledStart = DateTime.UtcNow.AddHours(-1);

        var result = await service.CreateAsync(project.ProjectId, salesId, request);

        Assert.Equal(400, result.Status);
    }

    [Fact]
    public async Task CreateAsync_ReturnsBadRequest_WhenScheduledEndIsBeforeStart()
    {
        var salesId = Guid.NewGuid();
        var project = CreateProject(assignedSalesId: salesId);
        var service = BuildService(role: "SALES", projectDetail: project);
        var request = ValidCreateRequest();
        request.ScheduledStart = DateTime.UtcNow.AddDays(1);
        request.ScheduledEnd = DateTime.UtcNow.AddHours(1);

        var result = await service.CreateAsync(project.ProjectId, salesId, request);

        Assert.Equal(400, result.Status);
    }

    [Fact]
    public async Task CreateAsync_ReturnsCreated_WithPendingConfirmationStatus()
    {
        var salesId = Guid.NewGuid();
        var project = CreateProject(assignedSalesId: salesId);
        var scheduleRepo = new FakeProjectScheduleRepository();
        var service = BuildService(role: "SALES", projectDetail: project, scheduleRepo: scheduleRepo);

        var result = await service.CreateAsync(project.ProjectId, salesId, ValidCreateRequest());

        Assert.Equal(201, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(ProjectScheduleStatus.PENDING_CONFIRMATION, result.Data.Status);
        Assert.Equal(1, scheduleRepo.AddCallCount);
        Assert.Equal(1, scheduleRepo.SaveChangesCallCount);
    }

    [Fact]
    public async Task CreateAsync_AdminCanCreateForAnyProject()
    {
        var project = CreateProject(assignedSalesId: Guid.NewGuid());
        var scheduleRepo = new FakeProjectScheduleRepository();
        var service = BuildService(role: "ADMIN", projectDetail: project, scheduleRepo: scheduleRepo);

        var result = await service.CreateAsync(project.ProjectId, Guid.NewGuid(), ValidCreateRequest());

        Assert.Equal(201, result.Status);
        Assert.Equal(1, scheduleRepo.AddCallCount);
    }

    [Fact]
    public async Task CreateAsync_DispatchesNotification_AfterSuccessfulCreate()
    {
        var salesId = Guid.NewGuid();
        var project = CreateProject(assignedSalesId: salesId);
        var dispatcher = new FakeNotificationDispatcher();
        var service = BuildService(role: "SALES", projectDetail: project, dispatcher: dispatcher);

        await service.CreateAsync(project.ProjectId, salesId, ValidCreateRequest());

        Assert.Equal(1, dispatcher.DispatchCallCount);
        Assert.Equal(NotificationType.ProjectScheduleCreated, dispatcher.LastType);
    }

    // ── SCH-02: GetList ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetListByProjectAsync_ReturnsUnauthorized_WhenUserIdIsEmpty()
    {
        var service = BuildService(role: "SALES");

        var result = await service.GetListByProjectAsync(Guid.NewGuid(), Guid.Empty, new ProjectScheduleListQueryDto());

        Assert.Equal(401, result.Status);
    }

    [Fact]
    public async Task GetListByProjectAsync_ReturnsNotFound_WhenProjectDoesNotExist()
    {
        var service = BuildService(role: "ADMIN", projectDetail: null);

        var result = await service.GetListByProjectAsync(Guid.NewGuid(), Guid.NewGuid(), new ProjectScheduleListQueryDto());

        Assert.Equal(404, result.Status);
    }

    [Fact]
    public async Task GetListByProjectAsync_ReturnsForbidden_WhenCustomerDoesNotOwnProject()
    {
        var project = CreateProject();
        var service = BuildService(role: "CUSTOMER", projectDetail: project);

        var result = await service.GetListByProjectAsync(project.ProjectId, Guid.NewGuid(), new ProjectScheduleListQueryDto());

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task GetListByProjectAsync_ReturnsSuccess_WhenCustomerIsOwner()
    {
        var customerId = Guid.NewGuid();
        var project = CreateProject(customerId: customerId);
        var service = BuildService(role: "CUSTOMER", projectDetail: project);

        var result = await service.GetListByProjectAsync(project.ProjectId, customerId, new ProjectScheduleListQueryDto());

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public async Task GetListByProjectAsync_ReturnsSuccess_ForAdmin()
    {
        var project = CreateProject();
        var service = BuildService(role: "ADMIN", projectDetail: project);

        var result = await service.GetListByProjectAsync(project.ProjectId, Guid.NewGuid(), new ProjectScheduleListQueryDto());

        Assert.Equal(200, result.Status);
    }

    // ── SCH-03: GetDetail ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetDetailAsync_ReturnsNotFound_WhenScheduleDoesNotExist()
    {
        var service = BuildService(role: "ADMIN", scheduleDetail: null);

        var result = await service.GetDetailAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(404, result.Status);
    }

    [Fact]
    public async Task GetDetailAsync_ReturnsForbidden_WhenUserHasNoAccess()
    {
        var detail = CreateScheduleDetail();
        var service = BuildService(role: "CUSTOMER", scheduleDetail: detail);

        var result = await service.GetDetailAsync(detail.ScheduleId, Guid.NewGuid());

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task GetDetailAsync_ReturnsSuccess_WhenAdminAccesses()
    {
        var detail = CreateScheduleDetail();
        var service = BuildService(role: "ADMIN", scheduleDetail: detail);

        var result = await service.GetDetailAsync(detail.ScheduleId, Guid.NewGuid());

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(detail.ScheduleId, result.Data.ScheduleId);
    }

    [Fact]
    public async Task GetDetailAsync_ReturnsSuccess_WhenAssignedStaffAccesses()
    {
        var staffId = Guid.NewGuid();
        var detail = CreateScheduleDetail(assignedStaffId: staffId);
        var service = BuildService(role: "DESIGNER", scheduleDetail: detail);

        var result = await service.GetDetailAsync(detail.ScheduleId, staffId);

        Assert.Equal(200, result.Status);
    }

    // ── SCH-04: Update ──────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_ReturnsNotFound_WhenScheduleDoesNotExist()
    {
        var service = BuildService(role: "ADMIN", scheduleDetail: null);

        var result = await service.UpdateAsync(Guid.NewGuid(), Guid.NewGuid(), new UpdateProjectScheduleRequestDto());

        Assert.Equal(404, result.Status);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsForbidden_WhenSalesIsNotAssigned()
    {
        var detail = CreateScheduleDetail(assignedSalesId: Guid.NewGuid(), status: ProjectScheduleStatus.PENDING_CONFIRMATION);
        var service = BuildService(role: "SALES", scheduleDetail: detail);

        var result = await service.UpdateAsync(detail.ScheduleId, Guid.NewGuid(), new UpdateProjectScheduleRequestDto());

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsBadRequest_WhenStatusIsCompleted_AndNotAdmin()
    {
        var salesId = Guid.NewGuid();
        var detail = CreateScheduleDetail(assignedSalesId: salesId, status: ProjectScheduleStatus.COMPLETED);
        var service = BuildService(role: "SALES", scheduleDetail: detail);

        var result = await service.UpdateAsync(detail.ScheduleId, salesId, new UpdateProjectScheduleRequestDto());

        Assert.Equal(400, result.Status);
    }

    [Fact]
    public async Task UpdateAsync_ResetsStatusToPending_WhenScheduledStartChanges()
    {
        var salesId = Guid.NewGuid();
        var schedule = CreateScheduleEntity(status: ProjectScheduleStatus.CONFIRMED, scheduledStart: DateTime.UtcNow.AddDays(2));
        var detail = CreateScheduleDetail(assignedSalesId: salesId, status: ProjectScheduleStatus.CONFIRMED, scheduleId: schedule.ScheduleId);
        var scheduleRepo = new FakeProjectScheduleRepository(entityById: schedule, detail: detail);
        var service = BuildService(role: "SALES", scheduleDetail: detail, scheduleRepo: scheduleRepo);

        var request = new UpdateProjectScheduleRequestDto
        {
            ScheduledStart = DateTime.UtcNow.AddDays(5)
        };

        var result = await service.UpdateAsync(schedule.ScheduleId, salesId, request);

        Assert.Equal(200, result.Status);
        Assert.Equal(ProjectScheduleStatus.PENDING_CONFIRMATION, schedule.Status);
    }

    // ── SCH-05: UpdateStatus ────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateStatusAsync_ReturnsBadRequest_WhenStatusIsAlreadyTerminal()
    {
        var detail = CreateScheduleDetail(status: ProjectScheduleStatus.CANCELLED);
        var service = BuildService(role: "ADMIN", scheduleDetail: detail);

        var result = await service.UpdateStatusAsync(detail.ScheduleId, Guid.NewGuid(),
            new UpdateProjectScheduleStatusRequestDto { Status = ProjectScheduleStatus.CONFIRMED });

        Assert.Equal(400, result.Status);
    }

    [Fact]
    public async Task UpdateStatusAsync_ReturnsForbidden_WhenSalesTryToConfirm()
    {
        var salesId = Guid.NewGuid();
        var detail = CreateScheduleDetail(assignedSalesId: salesId, status: ProjectScheduleStatus.PENDING_CONFIRMATION);
        var service = BuildService(role: "SALES", scheduleDetail: detail);

        var result = await service.UpdateStatusAsync(detail.ScheduleId, salesId,
            new UpdateProjectScheduleStatusRequestDto { Status = ProjectScheduleStatus.CONFIRMED });

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task UpdateStatusAsync_ReturnsSuccess_WhenCustomerConfirms()
    {
        var customerId = Guid.NewGuid();
        var schedule = CreateScheduleEntity(status: ProjectScheduleStatus.PENDING_CONFIRMATION);
        var detail = CreateScheduleDetail(customerId: customerId, status: ProjectScheduleStatus.PENDING_CONFIRMATION, scheduleId: schedule.ScheduleId);
        var scheduleRepo = new FakeProjectScheduleRepository(entityById: schedule, detail: detail);
        var service = BuildService(role: "CUSTOMER", scheduleDetail: detail, scheduleRepo: scheduleRepo);

        var result = await service.UpdateStatusAsync(schedule.ScheduleId, customerId,
            new UpdateProjectScheduleStatusRequestDto { Status = ProjectScheduleStatus.CONFIRMED });

        Assert.Equal(200, result.Status);
        Assert.Equal(ProjectScheduleStatus.CONFIRMED, schedule.Status);
    }

    [Fact]
    public async Task UpdateStatusAsync_SetsCancelledAt_WhenCancelling()
    {
        var customerId = Guid.NewGuid();
        var schedule = CreateScheduleEntity(status: ProjectScheduleStatus.PENDING_CONFIRMATION);
        var detail = CreateScheduleDetail(customerId: customerId, status: ProjectScheduleStatus.PENDING_CONFIRMATION, scheduleId: schedule.ScheduleId);
        var scheduleRepo = new FakeProjectScheduleRepository(entityById: schedule, detail: detail);
        var service = BuildService(role: "CUSTOMER", scheduleDetail: detail, scheduleRepo: scheduleRepo);

        var result = await service.UpdateStatusAsync(schedule.ScheduleId, customerId,
            new UpdateProjectScheduleStatusRequestDto { Status = ProjectScheduleStatus.CANCELLED });

        Assert.Equal(200, result.Status);
        Assert.Equal(ProjectScheduleStatus.CANCELLED, schedule.Status);
        Assert.NotNull(schedule.CancelledAt);
    }

    [Fact]
    public async Task UpdateStatusAsync_ReturnsBadRequest_WhenCompletingNonConfirmed()
    {
        var detail = CreateScheduleDetail(status: ProjectScheduleStatus.PENDING_CONFIRMATION);
        var service = BuildService(role: "ADMIN", scheduleDetail: detail);

        var result = await service.UpdateStatusAsync(detail.ScheduleId, Guid.NewGuid(),
            new UpdateProjectScheduleStatusRequestDto { Status = ProjectScheduleStatus.COMPLETED });

        Assert.Equal(400, result.Status);
    }

    // ── SCH-06: GetMyAssigned ───────────────────────────────────────────────────

    [Fact]
    public async Task GetMyAssignedAsync_ReturnsUnauthorized_WhenUserIdIsEmpty()
    {
        var service = BuildService(role: "SALES");

        var result = await service.GetMyAssignedAsync(Guid.Empty, new ProjectScheduleListQueryDto());

        Assert.Equal(401, result.Status);
    }

    [Fact]
    public async Task GetMyAssignedAsync_ReturnsSuccess_WithOwnSchedules()
    {
        var staffId = Guid.NewGuid();
        var scheduleRepo = new FakeProjectScheduleRepository(
            myAssignedItems: [CreateListItem()],
            myAssignedTotal: 1);
        var service = BuildService(role: "SALES", scheduleRepo: scheduleRepo);

        var result = await service.GetMyAssignedAsync(staffId, new ProjectScheduleListQueryDto());

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(1, result.Data.Total);
        Assert.Single(result.Data.Items);
        Assert.Equal(staffId, scheduleRepo.LastMyAssignedStaffId);
    }

    [Fact]
    public async Task GetMyAssignedAsync_PassesNullStaffId_WhenAdmin()
    {
        var scheduleRepo = new FakeProjectScheduleRepository();
        var service = BuildService(role: "ADMIN", scheduleRepo: scheduleRepo);

        await service.GetMyAssignedAsync(Guid.NewGuid(), new ProjectScheduleListQueryDto());

        Assert.Null(scheduleRepo.LastMyAssignedStaffId);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    private static ProjectScheduleService BuildService(
        string? role = null,
        ProjectDetailReadModel? projectDetail = null,
        ProjectScheduleDetailReadModel? scheduleDetail = null,
        FakeProjectScheduleRepository? scheduleRepo = null,
        FakeNotificationDispatcher? dispatcher = null)
    {
        scheduleRepo ??= new FakeProjectScheduleRepository(detail: scheduleDetail);
        var projectRepo = new FakeProjectRepository(role: role, detail: projectDetail);
        dispatcher ??= new FakeNotificationDispatcher();
        return new ProjectScheduleService(
            scheduleRepo,
            projectRepo,
            dispatcher,
            global::FurniSpace.Application.Tests.TestDoubles.TestUnitOfWork.ForSaveChanges(scheduleRepo.SaveChangesAsync));
    }

    private static CreateProjectScheduleRequestDto ValidCreateRequest() => new()
    {
        ScheduleType = ProjectScheduleType.MEASUREMENT,
        Title = "First measurement",
        AssignedStaffId = Guid.NewGuid(),
        ScheduledStart = DateTime.UtcNow.AddDays(1),
        ScheduledEnd = DateTime.UtcNow.AddDays(1).AddHours(2),
        Location = "123 Test St"
    };

    private static ProjectDetailReadModel CreateProject(
        Guid? customerId = null,
        Guid? assignedSalesId = null)
    {
        var id = Guid.NewGuid();
        return new ProjectDetailReadModel
        {
            ProjectId = id,
            CustomerId = customerId ?? Guid.NewGuid(),
            AssignedSalesId = assignedSalesId,
            ProjectName = "Test Project"
        };
    }

    private static ProjectScheduleDetailReadModel CreateScheduleDetail(
        Guid? scheduleId = null,
        Guid? customerId = null,
        Guid? assignedSalesId = null,
        Guid? assignedDesignerId = null,
        Guid? assignedStaffId = null,
        ProjectScheduleStatus status = ProjectScheduleStatus.PENDING_CONFIRMATION)
    {
        return new ProjectScheduleDetailReadModel
        {
            ScheduleId = scheduleId ?? Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            ProjectName = "Test Project",
            CustomerId = customerId ?? Guid.NewGuid(),
            AssignedSalesId = assignedSalesId,
            AssignedDesignerId = assignedDesignerId,
            AssignedStaffId = assignedStaffId,
            ScheduleType = ProjectScheduleType.MEASUREMENT,
            Title = "Test Schedule",
            ScheduledStart = DateTime.UtcNow.AddDays(1),
            Status = status
        };
    }

    private static ProjectSchedule CreateScheduleEntity(
        ProjectScheduleStatus status = ProjectScheduleStatus.PENDING_CONFIRMATION,
        DateTime? scheduledStart = null)
    {
        return new ProjectSchedule
        {
            ScheduleId = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            ScheduleType = ProjectScheduleType.MEASUREMENT,
            Title = "Test",
            ScheduledStart = scheduledStart ?? DateTime.UtcNow.AddDays(1),
            Status = status,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static ProjectScheduleListItemReadModel CreateListItem()
    {
        return new ProjectScheduleListItemReadModel
        {
            ScheduleId = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            ScheduleType = ProjectScheduleType.MEASUREMENT,
            ScheduledStart = DateTime.UtcNow.AddDays(1),
            Status = ProjectScheduleStatus.PENDING_CONFIRMATION
        };
    }

    // ── Fakes ────────────────────────────────────────────────────────────────────

    private sealed class FakeProjectScheduleRepository : IProjectScheduleRepository
    {
        private readonly ProjectScheduleDetailReadModel? _detail;
        private readonly ProjectSchedule? _entityById;
        private readonly IReadOnlyList<ProjectScheduleListItemReadModel> _listItems;
        private readonly int _listTotal;
        private readonly IReadOnlyList<ProjectScheduleListItemReadModel> _myAssignedItems;
        private readonly int _myAssignedTotal;
        private readonly List<ProjectSchedule> _entities = [];

        public FakeProjectScheduleRepository(
            ProjectScheduleDetailReadModel? detail = null,
            ProjectSchedule? entityById = null,
            IReadOnlyList<ProjectScheduleListItemReadModel>? listItems = null,
            int listTotal = 0,
            IReadOnlyList<ProjectScheduleListItemReadModel>? myAssignedItems = null,
            int myAssignedTotal = 0)
        {
            _detail = detail;
            _entityById = entityById;
            _listItems = listItems ?? [];
            _listTotal = listTotal;
            _myAssignedItems = myAssignedItems ?? [];
            _myAssignedTotal = myAssignedTotal;
        }

        public int AddCallCount { get; private set; }
        public int SaveChangesCallCount { get; private set; }
        public Guid? LastMyAssignedStaffId { get; private set; }

        public Task<ProjectScheduleDetailReadModel?> GetDetailAsync(
            Guid scheduleId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_detail?.ScheduleId == scheduleId ? _detail : null);
        }

        public Task<(IReadOnlyList<ProjectScheduleListItemReadModel> Items, int Total)> GetListByProjectAsync(
            Guid projectId, ProjectScheduleListQueryReadModel query,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult((_listItems, _listTotal));
        }

        public Task<(IReadOnlyList<ProjectScheduleListItemReadModel> Items, int Total)> GetMyAssignedAsync(
            Guid? staffId, ProjectScheduleListQueryReadModel query,
            CancellationToken cancellationToken = default)
        {
            LastMyAssignedStaffId = staffId;
            return Task.FromResult((_myAssignedItems, _myAssignedTotal));
        }

        public Task<ProjectSchedule?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                _entityById?.ScheduleId == id ? _entityById :
                _entities.FirstOrDefault(s => s.ScheduleId == id));
        }

        public Task AddAsync(ProjectSchedule entity, CancellationToken cancellationToken = default)
        {
            AddCallCount++;
            _entities.Add(entity);
            return Task.CompletedTask;
        }

        public void Update(ProjectSchedule entity) { }
        public void Remove(ProjectSchedule entity) => _entities.Remove(entity);

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCallCount++;
            return Task.FromResult(1);
        }

        public IQueryable<ProjectSchedule> Query() => _entities.AsQueryable();
        public Task<IReadOnlyList<ProjectSchedule>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProjectSchedule>>(_entities);
        public Task AddRangeAsync(IEnumerable<ProjectSchedule> entities, CancellationToken cancellationToken = default)
        {
            _entities.AddRange(entities);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeProjectRepository : IProjectRepository
    {
        private readonly string? _role;
        private readonly ProjectDetailReadModel? _detail;

        public FakeProjectRepository(string? role = null, ProjectDetailReadModel? detail = null)
        {
            _role = role;
            _detail = detail;
        }

        public Task<string?> GetAccountRoleNameAsync(Guid accountId, CancellationToken cancellationToken = default)
            => Task.FromResult(_role);

        public Task<string?> GetAccountFullNameAsync(Guid accountId, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);

        public Task<IReadOnlyList<Guid>> GetActiveAccountIdsByRoleNamesAsync(
            IReadOnlyCollection<string> roleNames,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Guid>>([]);

        public Task<ProjectDetailReadModel?> GetDetailAsync(Guid projectId, CancellationToken cancellationToken = default)
            => Task.FromResult(_detail?.ProjectId == projectId ? _detail : null);

        public Task<int> CountSubmittedInYearAsync(int year, CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<DesignerAccountReadModel?> GetActiveDesignerAsync(Guid designerId, CancellationToken cancellationToken = default)
            => Task.FromResult<DesignerAccountReadModel?>(null);

        public Task<IReadOnlyList<ProjectListItemReadModel>> GetListAsync(
            ProjectListQueryReadModel query, CancellationToken cancellationToken = default)
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
            => Task.FromResult(1);
    }

    private sealed class FakeNotificationDispatcher : INotificationDispatcher
    {
        public int DispatchCallCount { get; private set; }
        public NotificationType LastType { get; private set; }

        public Task DispatchAsync(
            NotificationType type,
            IReadOnlyDictionary<string, string> parameters,
            IEnumerable<Guid> receiverIds,
            Guid? projectId = null,
            string? referenceType = null,
            Guid? referenceId = null,
            CancellationToken cancellationToken = default)
        {
            DispatchCallCount++;
            LastType = type;
            return Task.CompletedTask;
        }
    }
}
