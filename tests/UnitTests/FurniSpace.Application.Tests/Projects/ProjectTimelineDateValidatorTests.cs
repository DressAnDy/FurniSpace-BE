#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.Common.Projects;
using FurniSpace.Application.DTOs.ProjectSchedules;
using FurniSpace.Application.DTOs.Projects;
using FurniSpace.Application.Services.Production;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.ReadModels.Production;
using FurniSpace.Infrastructure.ReadModels.ProjectSchedules;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Xunit;

namespace FurniSpace.Application.Tests.Projects;

public sealed class ProjectTimelineDateValidatorTests
{
    [Fact]
    public void ValidateTargetNotInPast_WhenDateIsPast_ReturnsValidationError()
    {
        var today = new DateOnly(2026, 8, 15);
        var target = new DateOnly(2026, 8, 14);

        var error = ProjectTimelineDateValidator.ValidateTargetNotInPast(target, today);

        Assert.NotNull(error);
        Assert.Equal(ProjectErrorCodes.InvalidTargetCompletionDate, error!.Code);
    }

    [Fact]
    public void ValidateTargetNotInPast_WhenDateIsToday_ReturnsNull()
    {
        var today = new DateOnly(2026, 8, 15);

        var error = ProjectTimelineDateValidator.ValidateTargetNotInPast(today, today);

        Assert.Null(error);
    }

    [Fact]
    public void ValidateScheduleDateWithinTarget_WhenScheduleExceedsTarget_ReturnsValidationError()
    {
        var target = new DateOnly(2026, 8, 20);
        var scheduleDate = new DateTime(2026, 8, 21, 10, 0, 0, DateTimeKind.Utc);

        var error = ProjectTimelineDateValidator.ValidateScheduleDateWithinTarget(scheduleDate, target);

        Assert.NotNull(error);
        Assert.Equal(ProjectScheduleErrorCodes.ScheduleDateExceedsTarget, error!.Code);
    }

    [Fact]
    public void ValidateScheduleDateWithinTarget_WhenScheduleOnTarget_ReturnsNull()
    {
        var target = new DateOnly(2026, 8, 20);
        var scheduleDate = new DateTime(2026, 8, 20, 15, 0, 0, DateTimeKind.Utc);

        var error = ProjectTimelineDateValidator.ValidateScheduleDateWithinTarget(scheduleDate, target);

        Assert.Null(error);
    }

    [Fact]
    public void ValidateScheduleDateWithinTarget_WhenTargetMissing_ReturnsNull()
    {
        var scheduleDate = DateTime.UtcNow.AddDays(30);

        var error = ProjectTimelineDateValidator.ValidateScheduleDateWithinTarget(scheduleDate, targetCompletionDate: null);

        Assert.Null(error);
    }

    [Fact]
    public void ValidateDateOnlyWithinTarget_WhenDateExceedsTarget_ReturnsValidationError()
    {
        var target = new DateOnly(2026, 8, 20);
        var date = new DateOnly(2026, 8, 21);

        var error = ProjectTimelineDateValidator.ValidateDateOnlyWithinTarget(
            date,
            target,
            ProductionErrorCodes.ProductionDateExceedsTarget,
            "Date exceeds target.");

        Assert.NotNull(error);
        Assert.Equal(ProductionErrorCodes.ProductionDateExceedsTarget, error!.Code);
    }

    [Fact]
    public void ValidateDateOnlyWithinTarget_WhenDateWithinTarget_ReturnsNull()
    {
        var target = new DateOnly(2026, 8, 20);

        var error = ProjectTimelineDateValidator.ValidateDateOnlyWithinTarget(
            target,
            target,
            ProductionErrorCodes.ProductionDateExceedsTarget,
            "Date exceeds target.");

        Assert.Null(error);
    }

    [Fact]
    public async Task ValidateTargetNotBeforeCommittedDatesAsync_WhenScheduleDateConflicts_ReturnsConflictError()
    {
        var projectId = Guid.NewGuid();
        var schedules = new FakeTimelineScheduleRepository { MaxOperationalScheduleDate = new DateOnly(2026, 9, 1) };

        var error = await ProjectTimelineDateValidator.ValidateTargetNotBeforeCommittedDatesAsync(
            projectId,
            new DateOnly(2026, 8, 31),
            schedules,
            new FakeTimelineProductionRequestRepository(),
            CancellationToken.None);

        Assert.NotNull(error);
        Assert.Equal(ProjectErrorCodes.TargetDateConflictsWithOperationalDates, error!.Code);
    }

    [Fact]
    public async Task ValidateTargetNotBeforeCommittedDatesAsync_WhenProductionDateConflicts_ReturnsConflictError()
    {
        var projectId = Guid.NewGuid();
        var productionRequests = new FakeTimelineProductionRequestRepository
        {
            MaxOperationalProductionDate = new DateOnly(2026, 9, 15)
        };

        var error = await ProjectTimelineDateValidator.ValidateTargetNotBeforeCommittedDatesAsync(
            projectId,
            new DateOnly(2026, 9, 1),
            new FakeTimelineScheduleRepository(),
            productionRequests,
            CancellationToken.None);

        Assert.NotNull(error);
        Assert.Equal(ProjectErrorCodes.TargetDateConflictsWithOperationalDates, error!.Code);
    }

    [Fact]
    public async Task ValidateTargetNotBeforeCommittedDatesAsync_WhenTargetMissing_ReturnsNull()
    {
        var error = await ProjectTimelineDateValidator.ValidateTargetNotBeforeCommittedDatesAsync(
            Guid.NewGuid(),
            newTargetCompletionDate: null,
            new FakeTimelineScheduleRepository { MaxOperationalScheduleDate = new DateOnly(2026, 9, 1) },
            new FakeTimelineProductionRequestRepository { MaxOperationalProductionDate = new DateOnly(2026, 9, 1) },
            CancellationToken.None);

        Assert.Null(error);
    }

    private sealed class FakeTimelineScheduleRepository : IProjectScheduleRepository
    {
        public DateOnly? MaxOperationalScheduleDate { get; init; }

        public Task<DateOnly?> GetMaxOperationalScheduleDateAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(MaxOperationalScheduleDate);

        public Task<ProjectSchedule?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<ProjectSchedule?>(null);

        public Task AddAsync(ProjectSchedule entity, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task AddRangeAsync(IEnumerable<ProjectSchedule> entities, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public void Update(ProjectSchedule entity)
        {
        }

        public void Remove(ProjectSchedule entity)
        {
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public IQueryable<ProjectSchedule> Query()
            => Enumerable.Empty<ProjectSchedule>().AsQueryable();

        public Task<IReadOnlyList<ProjectSchedule>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProjectSchedule>>([]);

        public Task<ProjectScheduleDetailReadModel?> GetDetailAsync(
            Guid scheduleId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<ProjectScheduleDetailReadModel?>(null);

        public Task<(IReadOnlyList<ProjectScheduleListItemReadModel> Items, int Total)> GetListByProjectAsync(
            Guid projectId,
            ProjectScheduleListQueryReadModel query,
            CancellationToken cancellationToken = default)
            => Task.FromResult<(IReadOnlyList<ProjectScheduleListItemReadModel>, int)>(([], 0));

        public Task<(IReadOnlyList<ProjectScheduleListItemReadModel> Items, int Total)> GetMyAssignedAsync(
            Guid? staffId,
            ProjectScheduleListQueryReadModel query,
            CancellationToken cancellationToken = default)
            => Task.FromResult<(IReadOnlyList<ProjectScheduleListItemReadModel>, int)>(([], 0));

        public Task<bool> HasCompletedMeasurementScheduleAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<bool> ExistsMeasurementScheduleAsync(
            Guid projectId,
            ProjectScheduleStatus? status,
            CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<bool> HasAssignedScheduleAsync(
            Guid projectId,
            Guid staffId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(false);
    }

    private sealed class FakeTimelineProductionRequestRepository : IProductionRequestRepository
    {
        public DateOnly? MaxOperationalProductionDate { get; init; }

        public Task<DateOnly?> GetMaxOperationalProductionDateAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(MaxOperationalProductionDate);

        public Task<bool> ExistsForOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<bool> HasActiveRequestForOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<int> CountCreatedOnAsync(DateOnly date, CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<List<OrderItem>> GetProductOrderItemsAsync(
            Guid orderId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new List<OrderItem>());

        public Task AddItemsAsync(List<ProductionItem> items, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<bool> IsActiveProductionStaffAsync(Guid accountId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<ProductionAssigneeReadModel?> GetAssigneeAsync(
            Guid accountId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<ProductionAssigneeReadModel?>(null);

        public Task<List<AvailableProductionStaffReadModel>> GetAvailableStaffAsync(
            string? search,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new List<AvailableProductionStaffReadModel>());

        public Task<List<ProductionRequestListItemReadModel>> GetQueueAsync(
            ProductionRequestQueueReadModel query,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new List<ProductionRequestListItemReadModel>());

        public Task<bool> HasViewableAssignedRequestAsync(
            Guid projectId,
            Guid productionAccountId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<ProductionRequestDetailReadModel?> GetDetailAsync(
            Guid productionRequestId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<ProductionRequestDetailReadModel?>(null);

        public Task<ProductionItem?> GetItemByIdAsync(Guid productionItemId, CancellationToken cancellationToken = default)
            => Task.FromResult<ProductionItem?>(null);

        public Task<ProductionRequestDetailReadModel?> GetDetailByItemIdAsync(
            Guid productionItemId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<ProductionRequestDetailReadModel?>(null);

        public void UpdateItem(ProductionItem item)
        {
        }

        public IQueryable<ProductionRequest> Query()
            => Enumerable.Empty<ProductionRequest>().AsQueryable();

        public Task<IReadOnlyList<ProductionRequest>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProductionRequest>>([]);

        public Task AddAsync(ProductionRequest entity, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<ProductionRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<ProductionRequest?>(null);

        public Task AddRangeAsync(IEnumerable<ProductionRequest> entities, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public void Update(ProductionRequest entity)
        {
        }

        public void Remove(ProductionRequest entity)
        {
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(0);
    }
}
