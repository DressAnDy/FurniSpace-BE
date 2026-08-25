#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.ReadModels.ProjectSchedules;
using FurniSpace.Infrastructure.Repositories.IRepository;
using FurniSpace.Infrastructure.Repositories.Repository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FurniSpace.Infrastructure.Tests.Repositories;

public sealed class ProjectScheduleRepositoryTests
{
    [Fact]
    public async Task GetDetailAsync_ReturnsJoinedProjectData()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new ProjectScheduleRepository(context);

        var detail = await repository.GetDetailAsync(data.DeliveryScheduleId);

        Assert.NotNull(detail);
        Assert.Equal(data.ProjectId, detail.ProjectId);
        Assert.Equal("Luxury Cafe", detail.ProjectName);
        Assert.Equal(data.ProductionId, detail.AssignedStaffId);
        Assert.Equal(ProjectScheduleType.DELIVERY, detail.ScheduleType);
    }

    [Fact]
    public async Task GetListByProjectAsync_FiltersByScheduleType()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new ProjectScheduleRepository(context);

        var (items, total) = await repository.GetListByProjectAsync(
            data.ProjectId,
            new ProjectScheduleListQueryReadModel
            {
                ScheduleType = ProjectScheduleType.DELIVERY,
                Page = 1,
                Limit = 10
            });

        Assert.Equal(1, total);
        Assert.Single(items);
        Assert.Equal(data.DeliveryScheduleId, items[0].ScheduleId);
    }

    [Fact]
    public async Task GetMyAssignedAsync_ReturnsSchedulesForAssignedStaff()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new ProjectScheduleRepository(context);

        var (items, total) = await repository.GetMyAssignedAsync(
            data.ProductionId,
            new ProjectScheduleListQueryReadModel { Page = 1, Limit = 10 });

        Assert.Equal(1, total);
        Assert.Single(items);
        Assert.Equal(data.ProductionId, items[0].AssignedStaffId);
    }

    [Fact]
    public async Task HasAssignedScheduleAsync_ReturnsTrueForMatchingAssignment()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new ProjectScheduleRepository(context);

        var assigned = await repository.HasAssignedScheduleAsync(data.ProjectId, data.ProductionId);
        var unassigned = await repository.HasAssignedScheduleAsync(data.ProjectId, Guid.NewGuid());

        Assert.True(assigned);
        Assert.False(unassigned);
    }

    [Fact]
    public async Task HasCompletedMeasurementScheduleAsync_ReturnsTrueWhenCompletedMeasurementExists()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new ProjectScheduleRepository(context);

        var hasCompleted = await repository.HasCompletedMeasurementScheduleAsync(data.ProjectId);
        var existsCompleted = await repository.ExistsMeasurementScheduleAsync(
            data.ProjectId,
            ProjectScheduleStatus.COMPLETED);

        Assert.True(hasCompleted);
        Assert.True(existsCompleted);
    }

    [Fact]
    public async Task ExistsMeasurementScheduleAsync_ReturnsFalseWhenOnlyCancelledMeasurementExists()
    {
        await using var context = CreateContext();
        var projectId = Guid.NewGuid();
        context.ProjectSet.Add(new Project
        {
            ProjectId = projectId,
            CustomerId = Guid.NewGuid(),
            ProjectName = "Luxury Cafe",
            BusinessType = "Cafe",
            FurnitureRequirement = "Tables",
            Status = ProjectStatus.PROPOSAL_CONSULTING
        });
        context.ProjectScheduleSet.Add(new ProjectSchedule
        {
            ScheduleId = Guid.NewGuid(),
            ProjectId = projectId,
            ScheduleType = ProjectScheduleType.MEASUREMENT,
            Title = "Cancelled measurement",
            ScheduledStart = DateTime.UtcNow.AddDays(-1),
            Status = ProjectScheduleStatus.CANCELLED,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var repository = new ProjectScheduleRepository(context);
        var existsAnyMeasurement = await repository.ExistsMeasurementScheduleAsync(projectId, status: null);

        Assert.False(existsAnyMeasurement);
    }

    [Fact]
    public async Task HasActiveDeliveryScheduleAsync_ReturnsTrueForPendingOrConfirmedDelivery()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new ProjectScheduleRepository(context);

        var hasActive = await repository.HasActiveDeliveryScheduleAsync(data.ProjectId);

        Assert.True(hasActive);
    }

    [Fact]
    public async Task HasActiveDeliveryScheduleAsync_ReturnsFalseWhenOnlyCancelledDeliveryExists()
    {
        await using var context = CreateContext();
        var projectId = Guid.NewGuid();
        context.ProjectSet.Add(new Project
        {
            ProjectId = projectId,
            CustomerId = Guid.NewGuid(),
            ProjectName = "Cancelled delivery project",
            BusinessType = "Cafe",
            FurnitureRequirement = "Tables",
            Status = ProjectStatus.READY_FOR_DELIVERY
        });
        context.ProjectScheduleSet.Add(new ProjectSchedule
        {
            ScheduleId = Guid.NewGuid(),
            ProjectId = projectId,
            ScheduleType = ProjectScheduleType.DELIVERY,
            Title = "Cancelled delivery",
            ScheduledStart = DateTime.UtcNow.AddDays(1),
            Status = ProjectScheduleStatus.CANCELLED,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        var repository = new ProjectScheduleRepository(context);

        var hasActive = await repository.HasActiveDeliveryScheduleAsync(projectId);

        Assert.False(hasActive);
    }

    [Fact]
    public async Task HasConfirmedDeliveryScheduleAsync_ReturnsTrueWhenConfirmedDeliveryExists()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new ProjectScheduleRepository(context);

        var hasConfirmed = await repository.HasConfirmedDeliveryScheduleAsync(data.ProjectId);

        Assert.True(hasConfirmed);
    }

    [Fact]
    public async Task HasActiveStaffOverlapAsync_ReturnsTrueForCrossProjectOverlap()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new ProjectScheduleRepository(context);
        var overlappingStart = DateTime.UtcNow.AddDays(2).AddMinutes(30);

        var hasOverlap = await repository.HasActiveStaffOverlapAsync(
            data.ProductionId,
            overlappingStart,
            overlappingStart.AddHours(1));

        Assert.True(hasOverlap);
    }

    [Fact]
    public async Task HasActiveStaffOverlapAsync_AllowsAdjacentAndIgnoresInactiveOrExcluded()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var staffId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var scheduleId = Guid.NewGuid();
        var activeStart = DateTime.UtcNow.AddDays(5);
        context.ProjectSet.Add(new Project
        {
            ProjectId = projectId,
            CustomerId = Guid.NewGuid(),
            ProjectName = "Overlap project",
            BusinessType = "Cafe",
            FurnitureRequirement = "Tables",
            Status = ProjectStatus.IN_CONSULTATION
        });
        context.ProjectScheduleSet.AddRange(
            new ProjectSchedule
            {
                ScheduleId = scheduleId,
                ProjectId = projectId,
                ScheduleType = ProjectScheduleType.CONSULTATION,
                Title = "Active",
                AssignedStaffId = staffId,
                ScheduledStart = activeStart,
                ScheduledEnd = activeStart.AddHours(2),
                Status = ProjectScheduleStatus.CONFIRMED,
                CreatedAt = DateTime.UtcNow
            },
            new ProjectSchedule
            {
                ScheduleId = Guid.NewGuid(),
                ProjectId = data.ProjectId,
                ScheduleType = ProjectScheduleType.CONSULTATION,
                Title = "Completed overlap",
                AssignedStaffId = staffId,
                ScheduledStart = activeStart.AddMinutes(30),
                ScheduledEnd = activeStart.AddHours(3),
                Status = ProjectScheduleStatus.COMPLETED,
                CreatedAt = DateTime.UtcNow
            });
        await context.SaveChangesAsync();
        var repository = new ProjectScheduleRepository(context);

        var adjacent = await repository.HasActiveStaffOverlapAsync(
            staffId,
            activeStart.AddHours(2),
            activeStart.AddHours(3));
        var excluded = await repository.HasActiveStaffOverlapAsync(
            staffId,
            activeStart.AddMinutes(30),
            activeStart.AddHours(1),
            scheduleId);

        Assert.False(adjacent);
        Assert.False(excluded);
    }

    [Fact]
    public async Task GetMaxOperationalScheduleDateAsync_ReturnsLatestNonCancelledScheduleDate()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var laterEnd = DateTime.UtcNow.AddDays(10);
        context.ProjectScheduleSet.Add(new ProjectSchedule
        {
            ScheduleId = Guid.NewGuid(),
            ProjectId = data.ProjectId,
            ScheduleType = ProjectScheduleType.OTHER,
            Title = "Handover",
            ScheduledStart = DateTime.UtcNow.AddDays(8),
            ScheduledEnd = laterEnd,
            Status = ProjectScheduleStatus.CONFIRMED,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        var repository = new ProjectScheduleRepository(context);

        var maxDate = await repository.GetMaxOperationalScheduleDateAsync(data.ProjectId);

        Assert.Equal(DateOnly.FromDateTime(laterEnd.ToUniversalTime()), maxDate);
    }

    [Fact]
    public async Task GetMaxOperationalScheduleDateAsync_IgnoresCancelledSchedules()
    {
        await using var context = CreateContext();
        var projectId = Guid.NewGuid();
        var activeStart = DateTime.UtcNow.AddDays(3);
        context.ProjectSet.Add(new Project
        {
            ProjectId = projectId,
            CustomerId = Guid.NewGuid(),
            ProjectName = "Schedule max project",
            BusinessType = "Cafe",
            FurnitureRequirement = "Tables",
            Status = ProjectStatus.PROPOSAL_CONSULTING
        });
        context.ProjectScheduleSet.AddRange(
            new ProjectSchedule
            {
                ScheduleId = Guid.NewGuid(),
                ProjectId = projectId,
                ScheduleType = ProjectScheduleType.MEASUREMENT,
                Title = "Active",
                ScheduledStart = activeStart,
                Status = ProjectScheduleStatus.CONFIRMED,
                CreatedAt = DateTime.UtcNow
            },
            new ProjectSchedule
            {
                ScheduleId = Guid.NewGuid(),
                ProjectId = projectId,
                ScheduleType = ProjectScheduleType.MEASUREMENT,
                Title = "Cancelled later",
                ScheduledStart = DateTime.UtcNow.AddDays(30),
                Status = ProjectScheduleStatus.CANCELLED,
                CreatedAt = DateTime.UtcNow
            });
        await context.SaveChangesAsync();
        var repository = new ProjectScheduleRepository(context);

        var maxDate = await repository.GetMaxOperationalScheduleDateAsync(projectId);

        Assert.Equal(DateOnly.FromDateTime(activeStart.ToUniversalTime()), maxDate);
    }

    [Fact]
    public async Task GetUnusedFutureDeliverySchedulesAsync_ExcludesSchedulesLinkedToDelivery()
    {
        await using var context = CreateContext();
        var projectId = Guid.NewGuid();
        var linkedScheduleId = Guid.NewGuid();
        var unusedScheduleId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        context.ProjectSet.Add(new Project
        {
            ProjectId = projectId,
            CustomerId = Guid.NewGuid(),
            ProjectName = "Delivery schedules",
            Status = ProjectStatus.DELIVERING
        });
        context.OrderSet.Add(new Order
        {
            OrderId = orderId,
            ProjectId = projectId,
            QuotationId = Guid.NewGuid(),
            OrderCode = "ORD-DLV",
            CustomerId = Guid.NewGuid(),
            VatRate = 0.08m,
            VatAmount = 0m,
            OriginalTotalAmount = 100m,
            FinalTotalAmount = 100m
        });
        context.ProjectScheduleSet.AddRange(
            new ProjectSchedule
            {
                ScheduleId = linkedScheduleId,
                ProjectId = projectId,
                ScheduleType = ProjectScheduleType.DELIVERY,
                Status = ProjectScheduleStatus.CONFIRMED,
                ScheduledStart = DateTime.UtcNow.AddDays(1),
                Title = "Linked"
            },
            new ProjectSchedule
            {
                ScheduleId = unusedScheduleId,
                ProjectId = projectId,
                ScheduleType = ProjectScheduleType.DELIVERY,
                Status = ProjectScheduleStatus.PENDING_CONFIRMATION,
                ScheduledStart = DateTime.UtcNow.AddDays(2),
                Title = "Unused"
            });
        context.DeliverySet.Add(new Delivery
        {
            DeliveryId = Guid.NewGuid(),
            OrderId = orderId,
            ProjectScheduleId = linkedScheduleId,
            Status = DeliveryStatus.IN_PROGRESS,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        var repository = new ProjectScheduleRepository(context);

        var unused = await repository.GetUnusedFutureDeliverySchedulesAsync(projectId);

        Assert.Single(unused);
        Assert.Equal(unusedScheduleId, unused[0].ScheduleId);
    }

    [Fact]
    public async Task HasUnresolvedConfirmedDeliveryScheduleAsync_ReturnsTrueWhenConfirmedWithoutCompletedBatch()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new ProjectScheduleRepository(context);

        var unresolved = await repository.HasUnresolvedConfirmedDeliveryScheduleAsync(data.ProjectId);

        Assert.True(unresolved);
    }

    [Fact]
    public async Task HasUnresolvedConfirmedDeliveryScheduleAsync_ReturnsFalseWhenConfirmedBatchCompleted()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var orderId = Guid.NewGuid();
        context.OrderSet.Add(new Order
        {
            OrderId = orderId,
            ProjectId = data.ProjectId,
            QuotationId = Guid.NewGuid(),
            OrderCode = "ORD-COMPLETE",
            CustomerId = Guid.NewGuid(),
            VatRate = 0.08m,
            VatAmount = 0m,
            OriginalTotalAmount = 100m,
            FinalTotalAmount = 100m
        });
        context.DeliverySet.Add(new Delivery
        {
            DeliveryId = Guid.NewGuid(),
            OrderId = orderId,
            ProjectScheduleId = data.DeliveryScheduleId,
            Status = DeliveryStatus.COMPLETED,
            CreatedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        var repository = new ProjectScheduleRepository(context);

        var unresolved = await repository.HasUnresolvedConfirmedDeliveryScheduleAsync(data.ProjectId);

        Assert.False(unresolved);
    }

    [Fact]
    public async Task HasLinkedInProgressDeliveryAsync_ReturnsTrueWhenBatchInProgress()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var orderId = Guid.NewGuid();
        context.OrderSet.Add(new Order
        {
            OrderId = orderId,
            ProjectId = data.ProjectId,
            QuotationId = Guid.NewGuid(),
            OrderCode = "ORD-IP",
            CustomerId = Guid.NewGuid(),
            VatRate = 0.08m,
            VatAmount = 0m,
            OriginalTotalAmount = 100m,
            FinalTotalAmount = 100m
        });
        context.DeliverySet.Add(new Delivery
        {
            DeliveryId = Guid.NewGuid(),
            OrderId = orderId,
            ProjectScheduleId = data.DeliveryScheduleId,
            Status = DeliveryStatus.IN_PROGRESS,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        var repository = new ProjectScheduleRepository(context);

        var linked = await repository.HasLinkedInProgressDeliveryAsync(data.DeliveryScheduleId);

        Assert.True(linked);
    }

    [Fact]
    public async Task ProjectScheduleRepositoryInterfaceDefaults_ReturnConfiguredFallbacks()
    {
        IProjectScheduleRepository repository = new MinimalProjectScheduleRepository();

        Assert.False(await repository.HasActiveDeliveryScheduleAsync(Guid.NewGuid()));
        Assert.False(await repository.HasActiveStaffOverlapAsync(
            Guid.NewGuid(),
            DateTime.UtcNow,
            DateTime.UtcNow.AddHours(1)));
        Assert.Null(await repository.GetMaxOperationalScheduleDateAsync(Guid.NewGuid()));
        Assert.False(await repository.HasLinkedInProgressDeliveryAsync(Guid.NewGuid()));
        Assert.Empty(await repository.GetUnusedFutureDeliverySchedulesAsync(Guid.NewGuid()));
        Assert.False(await repository.HasUnresolvedConfirmedDeliveryScheduleAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task HasActiveDeliveryScheduleAsync_DefaultInterfaceImplementation_ReturnsFalse()
    {
        IProjectScheduleRepository repository = new MinimalProjectScheduleRepository();

        var hasActive = await repository.HasActiveDeliveryScheduleAsync(Guid.NewGuid());
        var hasOverlap = await repository.HasActiveStaffOverlapAsync(
            Guid.NewGuid(),
            DateTime.UtcNow.AddHours(1),
            DateTime.UtcNow.AddHours(2));
        var conflict = await repository.GetStaffScheduleConflictAsync(
            Guid.NewGuid(),
            DateTime.UtcNow.AddHours(1),
            DateTime.UtcNow.AddHours(2));

        Assert.False(hasActive);
        Assert.False(hasOverlap);
        Assert.Equal(StaffScheduleConflictKind.None, conflict);
    }

    [Fact]
    public async Task GetStaffScheduleConflictAsync_ReturnsOverlapForIntersectingAppointment()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new ProjectScheduleRepository(context);
        var existing = await context.ProjectScheduleSet.FindAsync(data.DeliveryScheduleId);

        var conflict = await repository.GetStaffScheduleConflictAsync(
            data.ProductionId,
            existing!.ScheduledStart.AddMinutes(30),
            existing.ScheduledStart.AddHours(3));

        Assert.Equal(StaffScheduleConflictKind.Overlap, conflict);
    }

    [Fact]
    public async Task GetStaffScheduleConflictAsync_ReturnsMinimumGapForShortGap()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new ProjectScheduleRepository(context);
        var existing = await context.ProjectScheduleSet.FindAsync(data.DeliveryScheduleId);

        var conflict = await repository.GetStaffScheduleConflictAsync(
            data.ProductionId,
            existing!.ScheduledEnd!.Value.AddHours(1),
            existing.ScheduledEnd.Value.AddHours(2));

        Assert.Equal(StaffScheduleConflictKind.MinimumGapNotMet, conflict);
    }

    [Fact]
    public async Task GetStaffScheduleConflictAsync_UsesCompletedAtAndIgnoresCancelledSchedules()
    {
        await using var context = CreateContext();
        var projectId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var start = DateTime.UtcNow.AddDays(2);
        context.ProjectSet.Add(new Project
        {
            ProjectId = projectId,
            CustomerId = Guid.NewGuid(),
            ProjectName = "Cafe",
            BusinessType = "Cafe",
            FurnitureRequirement = "Tables"
        });
        context.ProjectScheduleSet.AddRange(
            new ProjectSchedule
            {
                ScheduleId = Guid.NewGuid(),
                ProjectId = projectId,
                ScheduleType = ProjectScheduleType.MEASUREMENT,
                Title = "Completed",
                AssignedStaffId = staffId,
                ScheduledStart = start,
                ScheduledEnd = start.AddHours(3),
                CompletedAt = start.AddHours(1),
                Status = ProjectScheduleStatus.COMPLETED
            },
            new ProjectSchedule
            {
                ScheduleId = Guid.NewGuid(),
                ProjectId = projectId,
                ScheduleType = ProjectScheduleType.DELIVERY,
                Title = "Cancelled",
                AssignedStaffId = staffId,
                ScheduledStart = start.AddHours(3),
                ScheduledEnd = start.AddHours(5),
                Status = ProjectScheduleStatus.CANCELLED
            });
        await context.SaveChangesAsync();
        var repository = new ProjectScheduleRepository(context);

        var conflict = await repository.GetStaffScheduleConflictAsync(
            staffId,
            start.AddHours(3),
            start.AddHours(4));

        Assert.Equal(StaffScheduleConflictKind.None, conflict);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<SeededData> SeedAsync(AppDbContext context)
    {
        var customerId = Guid.NewGuid();
        var salesId = Guid.NewGuid();
        var designerId = Guid.NewGuid();
        var productionId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var deliveryScheduleId = Guid.NewGuid();
        var measurementScheduleId = Guid.NewGuid();

        context.ProjectSet.Add(new Project
        {
            ProjectId = projectId,
            CustomerId = customerId,
            AssignedSalesId = salesId,
            AssignedDesignerId = designerId,
            ProjectName = "Luxury Cafe",
            BusinessType = "Cafe",
            FurnitureRequirement = "Tables",
            Status = ProjectStatus.PROPOSAL_CONSULTING
        });
        context.ProjectScheduleSet.AddRange(
            new ProjectSchedule
            {
                ScheduleId = deliveryScheduleId,
                ProjectId = projectId,
                ScheduleType = ProjectScheduleType.DELIVERY,
                Title = "Delivery",
                AssignedStaffId = productionId,
                ScheduledStart = DateTime.UtcNow.AddDays(2),
                ScheduledEnd = DateTime.UtcNow.AddDays(2).AddHours(2),
                Status = ProjectScheduleStatus.CONFIRMED,
                CreatedAt = DateTime.UtcNow
            },
            new ProjectSchedule
            {
                ScheduleId = measurementScheduleId,
                ProjectId = projectId,
                ScheduleType = ProjectScheduleType.MEASUREMENT,
                Title = "Measurement",
                AssignedStaffId = designerId,
                ScheduledStart = DateTime.UtcNow.AddDays(1),
                Status = ProjectScheduleStatus.COMPLETED,
                CreatedAt = DateTime.UtcNow
            },
            new ProjectSchedule
            {
                ScheduleId = Guid.NewGuid(),
                ProjectId = projectId,
                ScheduleType = ProjectScheduleType.MEASUREMENT,
                Title = "Cancelled measurement",
                AssignedStaffId = designerId,
                ScheduledStart = DateTime.UtcNow.AddDays(-1),
                Status = ProjectScheduleStatus.CANCELLED,
                CreatedAt = DateTime.UtcNow
            });

        await context.SaveChangesAsync();

        return new SeededData(projectId, productionId, deliveryScheduleId);
    }

    private sealed record SeededData(Guid ProjectId, Guid ProductionId, Guid DeliveryScheduleId);

    private sealed class MinimalProjectScheduleRepository : IProjectScheduleRepository
    {
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
}
