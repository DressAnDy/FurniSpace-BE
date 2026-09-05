#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.Tests.Projects;
using FurniSpace.Application.Common.Notifications;
using FurniSpace.Application.Common.Projects;
using FurniSpace.Application.DTOs.ProjectSchedules;
using FurniSpace.Application.Interfaces.Notifications;
using FurniSpace.Application.Services.ProjectSchedules;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.ReadModels.Orders;
using FurniSpace.Infrastructure.ReadModels.Production;
using FurniSpace.Infrastructure.ReadModels.ProjectSchedules;
using FurniSpace.Infrastructure.ReadModels.Projects;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Xunit;

namespace FurniSpace.Application.Tests.ProjectSchedules;

public sealed class ProjectScheduleServiceTests
{
    // ── SCH-01: Create ──────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_ReturnsUnauthorized_WhenUserIdIsEmpty()
    {
        var service = BuildService(new() { Role = "SALES" });

        var result = await service.CreateAsync(Guid.NewGuid(), Guid.Empty, ValidCreateRequest());

        Assert.Equal(401, result.Status);
    }

    [Fact]
    public async Task CreateAsync_ReturnsBadRequest_WhenProjectIdIsEmpty()
    {
        var service = BuildService(new() { Role = "SALES" });

        var result = await service.CreateAsync(Guid.Empty, Guid.NewGuid(), ValidCreateRequest());

        Assert.Equal(400, result.Status);
    }

    [Fact]
    public async Task CreateAsync_ReturnsForbidden_WhenRoleCannotCreateSchedules()
    {
        var service = BuildService(new() { Role = "CUSTOMER" });

        var result = await service.CreateAsync(Guid.NewGuid(), Guid.NewGuid(), ValidCreateRequest());

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task CreateAsync_ProductionCanCreateDeliverySchedule_WhenAssignedToSchedule()
    {
        var productionId = Guid.NewGuid();
        var project = CreateProject(status: ProjectStatus.READY_FOR_DELIVERY);
        var scheduleRepo = new FakeProjectScheduleRepository();
        var service = BuildService(new()
        {
            Role = "PRODUCTION",
            ProjectDetail = project,
            ScheduleRepo = scheduleRepo,
            OrderRepo = new FakeOrderRepository(hasProjectOrderInStatuses: true),
            ProductionRequestRepo = new FakeProductionRequestRepository
            {
                HasViewableAssignedRequest = true,
                HasAssignedCompletedProduction = true
            }
        });

        var result = await service.CreateAsync(
            project.ProjectId,
            productionId,
            ValidProductionCreateRequest(ProjectScheduleType.DELIVERY, productionId));

        Assert.Equal(201, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(ProjectScheduleType.DELIVERY, result.Data.ScheduleType);
        Assert.Equal(productionId, result.Data.AssignedStaffId);
        Assert.Equal(1, scheduleRepo.AddCallCount);
    }

    [Fact]
    public async Task CreateAsync_SalesCannotCreateDeliverySchedule_ReturnsForbidden()
    {
        var salesId = Guid.NewGuid();
        var project = CreateProject(assignedSalesId: salesId, status: ProjectStatus.READY_FOR_DELIVERY);
        var service = BuildService(new()
        {
            Role = "SALES",
            ProjectDetail = project,
            OrderRepo = new FakeOrderRepository(hasProjectOrderInStatuses: true)
        });

        var result = await service.CreateAsync(project.ProjectId, salesId, ValidDeliveryCreateRequest());

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task CreateAsync_DeliveryWithoutLocation_ReturnsLocationRequired()
    {
        var productionId = Guid.NewGuid();
        var project = CreateProject(status: ProjectStatus.READY_FOR_DELIVERY);
        var service = BuildService(new()
        {
            Role = "PRODUCTION",
            ProjectDetail = project,
            OrderRepo = new FakeOrderRepository(hasProjectOrderInStatuses: true),
            ProductionRequestRepo = new FakeProductionRequestRepository
            {
                HasAssignedCompletedProduction = true
            }
        });
        var request = ValidProductionCreateRequest(ProjectScheduleType.DELIVERY, productionId);
        request.Location = " ";

        var result = await service.CreateAsync(project.ProjectId, productionId, request);

        Assert.Equal(400, result.Status);
        Assert.Equal(ProjectScheduleErrorCodes.DeliveryScheduleLocationRequired, result.ErrorCode);
    }

    [Theory]
    [InlineData(ProjectScheduleType.MEASUREMENT)]
    [InlineData(ProjectScheduleType.CONSULTATION)]
    [InlineData(ProjectScheduleType.DESIGN_REVIEW)]
    public async Task CreateAsync_ProductionCreatingRestrictedScheduleType_ReturnsInvalidScheduleType(
        ProjectScheduleType scheduleType)
    {
        var productionId = Guid.NewGuid();
        var project = CreateProject();
        var service = BuildService(new() { Role = "PRODUCTION", ProjectDetail = project });

        var result = await service.CreateAsync(
            project.ProjectId,
            productionId,
            ValidProductionCreateRequest(scheduleType, productionId));

        Assert.Equal(400, result.Status);
        Assert.Equal(ProjectScheduleErrorCodes.InvalidScheduleType, result.ErrorCode);
    }

    [Fact]
    public async Task CreateAsync_ProductionWithoutRelatedWork_ReturnsForbidden()
    {
        var project = CreateProject();
        var service = BuildService(new() { Role = "PRODUCTION", ProjectDetail = project });

        var result = await service.CreateAsync(
            project.ProjectId,
            Guid.NewGuid(),
            ValidProductionCreateRequest(ProjectScheduleType.DELIVERY, Guid.NewGuid()));

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task CreateAsync_ReturnsNotFound_WhenProjectDoesNotExist()
    {
        var service = BuildService(new() { Role = "SALES" });

        var result = await service.CreateAsync(Guid.NewGuid(), Guid.NewGuid(), ValidCreateRequest());

        Assert.Equal(404, result.Status);
    }

    [Fact]
    public async Task CreateAsync_ReturnsForbidden_WhenSalesIsNotAssignedToProject()
    {
        var salesId = Guid.NewGuid();
        var project = CreateProject(assignedSalesId: Guid.NewGuid());
        var service = BuildService(new() { Role = "SALES", ProjectDetail = project });

        var result = await service.CreateAsync(project.ProjectId, salesId, ValidCreateRequest());

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task CreateAsync_ReturnsBadRequest_WhenScheduledStartIsInThePast()
    {
        var salesId = Guid.NewGuid();
        var project = CreateProject(assignedSalesId: salesId);
        var service = BuildService(new() { Role = "SALES", ProjectDetail = project });
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
        var service = BuildService(new() { Role = "SALES", ProjectDetail = project });
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
        var service = BuildService(new() { Role = "SALES", ProjectDetail = project, ScheduleRepo = scheduleRepo });

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
        var service = BuildService(new() { Role = "ADMIN", ProjectDetail = project, ScheduleRepo = scheduleRepo });

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
        var service = BuildService(new() { Role = "SALES", ProjectDetail = project, Dispatcher = dispatcher });

        await service.CreateAsync(project.ProjectId, salesId, ValidCreateRequest());

        Assert.Equal(1, dispatcher.DispatchCallCount);
        Assert.Equal(NotificationType.ProjectScheduleCreated, dispatcher.LastType);
    }

    [Fact]
    public async Task CreateAsync_DeliverySchedule_WhenOrderReady_CreatesPendingConfirmation()
    {
        var productionId = Guid.NewGuid();
        var project = CreateProject(status: ProjectStatus.READY_FOR_DELIVERY);
        var scheduleRepo = new FakeProjectScheduleRepository();
        var orderRepo = new FakeOrderRepository(hasProjectOrderInStatuses: true);
        var service = BuildService(new()
        {
            Role = "PRODUCTION",
            ProjectDetail = project,
            ScheduleRepo = scheduleRepo,
            OrderRepo = orderRepo,
            ProductionRequestRepo = new FakeProductionRequestRepository
            {
                HasAssignedCompletedProduction = true
            }
        });

        var result = await service.CreateAsync(
            project.ProjectId,
            productionId,
            ValidProductionCreateRequest(ProjectScheduleType.DELIVERY, productionId));

        Assert.Equal(201, result.Status);
        Assert.Equal(ProjectScheduleType.DELIVERY, result.Data!.ScheduleType);
        Assert.Equal(ProjectScheduleStatus.PENDING_CONFIRMATION, result.Data.Status);
        Assert.Equal(1, scheduleRepo.AddCallCount);
        Assert.Equal(project.ProjectId, orderRepo.LastProjectId);
    }

    [Fact]
    public async Task CreateAsync_DeliverySchedule_AllowsMultipleActiveSchedules()
    {
        var productionId = Guid.NewGuid();
        var project = CreateProject(status: ProjectStatus.DELIVERING);
        var scheduleRepo = new FakeProjectScheduleRepository();
        var service = BuildService(new()
        {
            Role = "PRODUCTION",
            ProjectDetail = project,
            ScheduleRepo = scheduleRepo,
            OrderRepo = new FakeOrderRepository(hasProjectOrderInStatuses: true),
            ProductionRequestRepo = new FakeProductionRequestRepository
            {
                HasAssignedCompletedProduction = true
            }
        });

        var firstRequest = ValidProductionCreateRequest(ProjectScheduleType.DELIVERY, productionId);
        var secondRequest = ValidProductionCreateRequest(ProjectScheduleType.DELIVERY, productionId);
        secondRequest.ScheduledStart = firstRequest.ScheduledStart.AddDays(2);
        secondRequest.ScheduledEnd = firstRequest.ScheduledEnd?.AddDays(2);

        var first = await service.CreateAsync(project.ProjectId, productionId, firstRequest);
        var second = await service.CreateAsync(project.ProjectId, productionId, secondRequest);

        Assert.Equal(201, first.Status);
        Assert.Equal(201, second.Status);
        Assert.Equal(2, scheduleRepo.AddCallCount);
    }

    [Theory]
    [InlineData(ProjectStatus.IN_PRODUCTION, true)]
    [InlineData(ProjectStatus.READY_FOR_DELIVERY, false)]
    public async Task CreateAsync_DeliverySchedule_WhenNotReady_ReturnsOrderNotReady(
        ProjectStatus projectStatus,
        bool hasReadyOrder)
    {
        var productionId = Guid.NewGuid();
        var project = CreateProject(status: projectStatus);
        var service = BuildService(new()
        {
            Role = "PRODUCTION",
            ProjectDetail = project,
            OrderRepo = new FakeOrderRepository(hasProjectOrderInStatuses: hasReadyOrder),
            ProductionRequestRepo = new FakeProductionRequestRepository
            {
                HasAssignedCompletedProduction = true
            }
        });

        var result = await service.CreateAsync(
            project.ProjectId,
            productionId,
            ValidProductionCreateRequest(ProjectScheduleType.DELIVERY, productionId));

        Assert.Equal(400, result.Status);
        Assert.Equal(ProjectScheduleErrorCodes.OrderNotReadyForDelivery, result.ErrorCode);
    }

    [Fact]
    public async Task CreateAsync_WhenScheduleDateExceedsTarget_ReturnsValidationError()
    {
        var salesId = Guid.NewGuid();
        var targetDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));
        var project = CreateProject(assignedSalesId: salesId, status: ProjectStatus.MEASUREMENT_REQUIRED);
        project.TargetCompletionDate = targetDate;
        var service = BuildService(new()
        {
            Role = "SALES",
            ProjectDetail = project
        });

        var result = await service.CreateAsync(
            project.ProjectId,
            salesId,
            new CreateProjectScheduleRequestDto
            {
                ScheduleType = ProjectScheduleType.MEASUREMENT,
                Title = "Late measurement",
                AssignedStaffId = Guid.NewGuid(),
                ScheduledStart = VietnamLocalAsUtc(dayOffset: 10, hour: 8),
                ScheduledEnd = VietnamLocalAsUtc(dayOffset: 10, hour: 10),
                Location = "Site"
            });

        Assert.Equal(400, result.Status);
        Assert.Equal(ProjectScheduleErrorCodes.ScheduleDateExceedsTarget, result.ErrorCode);
    }

    [Fact]
    public async Task CreateAsync_DeliverySchedule_WhenDeliveryAlreadyCompleted_ReturnsConflict()
    {
        var productionId = Guid.NewGuid();
        var project = CreateProject(status: ProjectStatus.DELIVERING);
        var service = BuildService(new()
        {
            Role = "PRODUCTION",
            ProjectDetail = project,
            OrderRepo = new FakeOrderRepository(
                hasProjectOrderInStatuses: true,
                hasCompletedDeliveryFlow: true),
            ProductionRequestRepo = new FakeProductionRequestRepository
            {
                HasAssignedCompletedProduction = true
            }
        });

        var result = await service.CreateAsync(
            project.ProjectId,
            productionId,
            ValidProductionCreateRequest(ProjectScheduleType.DELIVERY, productionId));

        Assert.Equal(409, result.Status);
        Assert.Equal(ProjectScheduleErrorCodes.DeliveryScheduleNotAllowedAfterCompletion, result.ErrorCode);
    }

    [Fact]
    public async Task CreateAsync_MeasurementScheduleWithoutEnd_ReturnsScheduleTimeInvalid()
    {
        var salesId = Guid.NewGuid();
        var designerId = Guid.NewGuid();
        var project = CreateProject(
            assignedSalesId: salesId,
            assignedDesignerId: designerId,
            status: ProjectStatus.MEASUREMENT_REQUIRED);
        var request = ValidMeasurementCreateRequest(designerId);
        request.ScheduledEnd = null;
        var service = BuildService(new() { Role = "SALES", ProjectDetail = project });

        var result = await service.CreateAsync(project.ProjectId, salesId, request);

        Assert.Equal(400, result.Status);
        Assert.Equal(ProjectScheduleErrorCodes.ScheduleTimeInvalid, result.ErrorCode);
    }

    [Fact]
    public async Task CreateAsync_DeliveryScheduleOutsideBusinessHours_ReturnsBusinessHoursError()
    {
        var productionId = Guid.NewGuid();
        var project = CreateProject(status: ProjectStatus.READY_FOR_DELIVERY);
        var request = ValidProductionCreateRequest(ProjectScheduleType.DELIVERY, productionId);
        request.ScheduledStart = VietnamLocalAsUtc(hour: 5, minute: 59);
        request.ScheduledEnd = VietnamLocalAsUtc(hour: 8);
        var service = BuildDeliveryScheduleService(project);

        var result = await service.CreateAsync(project.ProjectId, productionId, request);

        Assert.Equal(400, result.Status);
        Assert.Equal(ProjectScheduleErrorCodes.ScheduleOutsideBusinessHours, result.ErrorCode);
    }

    [Fact]
    public async Task CreateAsync_DeliveryScheduleCrossesVietnamMidnight_Succeeds()
    {
        var productionId = Guid.NewGuid();
        var project = CreateProject(status: ProjectStatus.READY_FOR_DELIVERY);
        var request = ValidProductionCreateRequest(ProjectScheduleType.DELIVERY, productionId);
        request.ScheduledStart = VietnamLocalAsUtc(dayOffset: 1, hour: 21);
        request.ScheduledEnd = VietnamLocalAsUtc(dayOffset: 2, hour: 6);
        var service = BuildDeliveryScheduleService(project);

        var result = await service.CreateAsync(project.ProjectId, productionId, request);

        Assert.Equal(201, result.Status);
    }

    [Fact]
    public async Task CreateAsync_DeliverySchedule_WhenExistingTenToTwelveAndNewNineToTen_ReturnsMinimumGapError()
    {
        var productionId = Guid.NewGuid();
        var project = CreateProject(status: ProjectStatus.READY_FOR_DELIVERY);
        var scheduleRepo = new FakeProjectScheduleRepository();
        var existingStart = VietnamLocalAsUtc(hour: 10);
        scheduleRepo.AddExistingSchedule(CreateScheduleEntity(
            project.ProjectId,
            productionId,
            ProjectScheduleStatus.CONFIRMED,
            existingStart,
            existingStart.AddHours(2),
            scheduleType: ProjectScheduleType.DELIVERY));
        var request = ValidProductionCreateRequest(ProjectScheduleType.DELIVERY, productionId);
        request.ScheduledStart = VietnamLocalAsUtc(hour: 9);
        request.ScheduledEnd = VietnamLocalAsUtc(hour: 10);
        var service = BuildService(new()
        {
            Role = "PRODUCTION",
            ProjectDetail = project,
            ScheduleRepo = scheduleRepo,
            OrderRepo = new FakeOrderRepository(hasProjectOrderInStatuses: true),
            ProductionRequestRepo = new FakeProductionRequestRepository
            {
                HasAssignedCompletedProduction = true
            }
        });

        var result = await service.CreateAsync(project.ProjectId, productionId, request);

        Assert.Equal(409, result.Status);
        Assert.Equal(ProjectScheduleErrorCodes.ScheduleMinimumGapNotMet, result.ErrorCode);
    }

    [Fact]
    public async Task CreateAsync_DeliverySchedule_WhenExistingTenToTwelveAndNewEndsAtNine_ReturnsMinimumGapError()
    {
        var productionId = Guid.NewGuid();
        var project = CreateProject(status: ProjectStatus.READY_FOR_DELIVERY);
        var scheduleRepo = new FakeProjectScheduleRepository();
        var existingStart = VietnamLocalAsUtc(hour: 10);
        scheduleRepo.AddExistingSchedule(CreateScheduleEntity(
            project.ProjectId,
            productionId,
            ProjectScheduleStatus.CONFIRMED,
            existingStart,
            existingStart.AddHours(2),
            scheduleType: ProjectScheduleType.DELIVERY));
        var request = ValidProductionCreateRequest(ProjectScheduleType.DELIVERY, productionId);
        request.ScheduledStart = VietnamLocalAsUtc(hour: 8);
        request.ScheduledEnd = VietnamLocalAsUtc(hour: 9);
        var service = BuildService(new()
        {
            Role = "PRODUCTION",
            ProjectDetail = project,
            ScheduleRepo = scheduleRepo,
            OrderRepo = new FakeOrderRepository(hasProjectOrderInStatuses: true),
            ProductionRequestRepo = new FakeProductionRequestRepository
            {
                HasAssignedCompletedProduction = true
            }
        });

        var result = await service.CreateAsync(project.ProjectId, productionId, request);

        Assert.Equal(409, result.Status);
        Assert.Equal(ProjectScheduleErrorCodes.ScheduleMinimumGapNotMet, result.ErrorCode);
    }

    [Fact]
    public async Task CreateAsync_DeliverySchedule_WithoutAssignedStaff_ReturnsBadRequest()
    {
        var project = CreateProject(status: ProjectStatus.READY_FOR_DELIVERY);
        var request = ValidProductionCreateRequest(ProjectScheduleType.DELIVERY, Guid.NewGuid());
        request.AssignedStaffId = null;
        var service = BuildService(new()
        {
            Role = "ADMIN",
            ProjectDetail = project,
            OrderRepo = new FakeOrderRepository(hasProjectOrderInStatuses: true),
            ProductionRequestRepo = new FakeProductionRequestRepository
            {
                HasAssignedCompletedProduction = true
            }
        });

        var result = await service.CreateAsync(project.ProjectId, Guid.NewGuid(), request);

        Assert.Equal(400, result.Status);
    }

    // ── SCH-02: GetList ─────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_WhenSameStaffHasOverlappingSameProjectSchedule_ReturnsConflict()
    {
        var salesId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var project = CreateProject(assignedSalesId: salesId);
        var scheduleRepo = new FakeProjectScheduleRepository();
        var existingStart = DateTime.UtcNow.AddDays(2);
        scheduleRepo.AddExistingSchedule(CreateScheduleEntity(
            project.ProjectId,
            staffId,
            ProjectScheduleStatus.CONFIRMED,
            existingStart,
            existingStart.AddHours(2)));
        var request = ValidCreateRequest();
        request.AssignedStaffId = staffId;
        request.ScheduledStart = existingStart.AddMinutes(30);
        request.ScheduledEnd = existingStart.AddHours(3);
        var service = BuildService(new() { Role = "SALES", ProjectDetail = project, ScheduleRepo = scheduleRepo });

        var result = await service.CreateAsync(project.ProjectId, salesId, request);

        Assert.Equal(409, result.Status);
        Assert.Equal(ProjectScheduleErrorCodes.StaffScheduleOverlap, result.ErrorCode);
        Assert.Equal(0, scheduleRepo.AddCallCount);
    }

    [Fact]
    public async Task CreateAsync_WhenSameStaffHasOverlappingCrossProjectSchedule_ReturnsConflict()
    {
        var salesId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var project = CreateProject(assignedSalesId: salesId);
        var scheduleRepo = new FakeProjectScheduleRepository();
        var existingStart = DateTime.UtcNow.AddDays(2);
        scheduleRepo.AddExistingSchedule(CreateScheduleEntity(
            Guid.NewGuid(),
            staffId,
            ProjectScheduleStatus.PENDING_CONFIRMATION,
            existingStart,
            existingStart.AddHours(2)));
        var request = ValidCreateRequest();
        request.AssignedStaffId = staffId;
        request.ScheduledStart = existingStart.AddHours(1);
        request.ScheduledEnd = existingStart.AddHours(3);
        var service = BuildService(new() { Role = "SALES", ProjectDetail = project, ScheduleRepo = scheduleRepo });

        var result = await service.CreateAsync(project.ProjectId, salesId, request);

        Assert.Equal(409, result.Status);
        Assert.Equal(ProjectScheduleErrorCodes.StaffScheduleOverlap, result.ErrorCode);
    }

    [Fact]
    public async Task CreateAsync_WhenSameStaffScheduleIsAdjacent_ReturnsCreated()
    {
        var salesId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var project = CreateProject(assignedSalesId: salesId);
        var scheduleRepo = new FakeProjectScheduleRepository();
        var existingStart = DateTime.UtcNow.AddDays(2);
        scheduleRepo.AddExistingSchedule(CreateScheduleEntity(
            Guid.NewGuid(),
            staffId,
            ProjectScheduleStatus.CONFIRMED,
            existingStart,
            existingStart.AddHours(2)));
        var request = ValidCreateRequest();
        request.AssignedStaffId = staffId;
        request.ScheduledStart = existingStart.AddHours(2);
        request.ScheduledEnd = existingStart.AddHours(4);
        var service = BuildService(new() { Role = "SALES", ProjectDetail = project, ScheduleRepo = scheduleRepo });

        var result = await service.CreateAsync(project.ProjectId, salesId, request);

        Assert.Equal(201, result.Status);
        Assert.Equal(1, scheduleRepo.AddCallCount);
    }

    [Theory]
    [InlineData(ProjectScheduleStatus.COMPLETED)]
    [InlineData(ProjectScheduleStatus.CANCELLED)]
    public async Task CreateAsync_WhenSameStaffOverlapIsInactive_ReturnsCreated(ProjectScheduleStatus status)
    {
        var salesId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var project = CreateProject(assignedSalesId: salesId);
        var scheduleRepo = new FakeProjectScheduleRepository();
        var existingStart = DateTime.UtcNow.AddDays(2);
        scheduleRepo.AddExistingSchedule(CreateScheduleEntity(
            Guid.NewGuid(),
            staffId,
            status,
            existingStart,
            existingStart.AddHours(2)));
        var request = ValidCreateRequest();
        request.AssignedStaffId = staffId;
        request.ScheduledStart = existingStart.AddMinutes(30);
        request.ScheduledEnd = existingStart.AddHours(3);
        var service = BuildService(new() { Role = "SALES", ProjectDetail = project, ScheduleRepo = scheduleRepo });

        var result = await service.CreateAsync(project.ProjectId, salesId, request);

        Assert.Equal(201, result.Status);
    }

    [Fact]
    public async Task CreateAsync_WhenMeasurementStaffGapIsLessThanTwoHours_ReturnsMinimumGapError()
    {
        var salesId = Guid.NewGuid();
        var designerId = Guid.NewGuid();
        var project = CreateProject(
            assignedSalesId: salesId,
            assignedDesignerId: designerId,
            status: ProjectStatus.MEASUREMENT_REQUIRED);
        var scheduleRepo = new FakeProjectScheduleRepository();
        var existingStart = VietnamLocalAsUtc(hour: 8);
        scheduleRepo.AddExistingSchedule(CreateScheduleEntity(
            Guid.NewGuid(),
            designerId,
            ProjectScheduleStatus.CONFIRMED,
            existingStart,
            existingStart.AddHours(2),
            scheduleType: ProjectScheduleType.MEASUREMENT));
        var request = ValidMeasurementCreateRequest(designerId);
        request.ScheduledStart = existingStart.AddHours(3).AddMinutes(59);
        request.ScheduledEnd = existingStart.AddHours(5);
        var service = BuildService(new() { Role = "SALES", ProjectDetail = project, ScheduleRepo = scheduleRepo });

        var result = await service.CreateAsync(project.ProjectId, salesId, request);

        Assert.Equal(409, result.Status);
        Assert.Equal(ProjectScheduleErrorCodes.ScheduleMinimumGapNotMet, result.ErrorCode);
    }

    [Fact]
    public async Task CreateAsync_WhenDurationLessThanOneHour_ReturnsMinimumDurationError()
    {
        var productionId = Guid.NewGuid();
        var project = CreateProject(status: ProjectStatus.READY_FOR_DELIVERY);
        var request = ValidProductionCreateRequest(ProjectScheduleType.DELIVERY, productionId);
        request.ScheduledStart = VietnamLocalAsUtc(hour: 8);
        request.ScheduledEnd = VietnamLocalAsUtc(hour: 8, minute: 30);
        var service = BuildDeliveryScheduleService(project);

        var result = await service.CreateAsync(project.ProjectId, productionId, request);

        Assert.Equal(400, result.Status);
        Assert.Equal(ProjectScheduleErrorCodes.ScheduleMinimumDurationNotMet, result.ErrorCode);
    }

    [Fact]
    public async Task CreateAsync_WhenDifferentStaffButSameCustomerGapTooShort_ReturnsCustomerMinimumGapError()
    {
        var customerId = Guid.NewGuid();
        var productionA = Guid.NewGuid();
        var productionB = Guid.NewGuid();
        var project = CreateProject(customerId: customerId, status: ProjectStatus.READY_FOR_DELIVERY);
        var scheduleRepo = new FakeProjectScheduleRepository();
        var existingStart = VietnamLocalAsUtc(hour: 10);
        scheduleRepo.AddExistingSchedule(CreateScheduleEntity(
            project.ProjectId,
            productionA,
            ProjectScheduleStatus.CONFIRMED,
            existingStart,
            existingStart.AddHours(2),
            scheduleType: ProjectScheduleType.DELIVERY));
        var request = ValidProductionCreateRequest(ProjectScheduleType.DELIVERY, productionB);
        request.ScheduledStart = VietnamLocalAsUtc(hour: 13);
        request.ScheduledEnd = VietnamLocalAsUtc(hour: 14);
        var service = BuildService(new()
        {
            Role = "PRODUCTION",
            ProjectDetail = project,
            ScheduleRepo = scheduleRepo,
            OrderRepo = new FakeOrderRepository(hasProjectOrderInStatuses: true),
            ProductionRequestRepo = new FakeProductionRequestRepository
            {
                HasAssignedCompletedProduction = true
            }
        });

        var result = await service.CreateAsync(project.ProjectId, productionB, request);

        Assert.Equal(409, result.Status);
        Assert.Equal(ProjectScheduleErrorCodes.CustomerScheduleMinimumGapNotMet, result.ErrorCode);
    }

    [Fact]
    public async Task CreateAsync_WhenMeasurementStaffGapIsExactlyTwoHours_ReturnsCreated()
    {
        var salesId = Guid.NewGuid();
        var designerId = Guid.NewGuid();
        var project = CreateProject(
            assignedSalesId: salesId,
            assignedDesignerId: designerId,
            status: ProjectStatus.MEASUREMENT_REQUIRED);
        var scheduleRepo = new FakeProjectScheduleRepository();
        var existingStart = VietnamLocalAsUtc(hour: 8);
        scheduleRepo.AddExistingSchedule(CreateScheduleEntity(
            Guid.NewGuid(),
            designerId,
            ProjectScheduleStatus.CONFIRMED,
            existingStart,
            existingStart.AddHours(2),
            scheduleType: ProjectScheduleType.MEASUREMENT));
        var request = ValidMeasurementCreateRequest(designerId);
        request.ScheduledStart = existingStart.AddHours(4);
        request.ScheduledEnd = existingStart.AddHours(5);
        var service = BuildService(new() { Role = "SALES", ProjectDetail = project, ScheduleRepo = scheduleRepo });

        var result = await service.CreateAsync(project.ProjectId, salesId, request);

        Assert.Equal(201, result.Status);
    }

    [Fact]
    public async Task CreateAsync_WhenCompletedScheduleEndedEarly_UsesCompletedAtForGap()
    {
        var salesId = Guid.NewGuid();
        var designerId = Guid.NewGuid();
        var project = CreateProject(
            assignedSalesId: salesId,
            assignedDesignerId: designerId,
            status: ProjectStatus.MEASUREMENT_REQUIRED);
        var scheduleRepo = new FakeProjectScheduleRepository();
        var existingStart = VietnamLocalAsUtc(hour: 8);
        var completedSchedule = CreateScheduleEntity(
            Guid.NewGuid(),
            designerId,
            ProjectScheduleStatus.COMPLETED,
            existingStart,
            existingStart.AddHours(2),
            scheduleType: ProjectScheduleType.MEASUREMENT);
        completedSchedule.CompletedAt = existingStart.AddHours(1).AddMinutes(15);
        scheduleRepo.AddExistingSchedule(completedSchedule);
        var request = ValidMeasurementCreateRequest(designerId);
        request.ScheduledStart = existingStart.AddHours(3).AddMinutes(15);
        request.ScheduledEnd = existingStart.AddHours(4).AddMinutes(15);
        var service = BuildService(new() { Role = "SALES", ProjectDetail = project, ScheduleRepo = scheduleRepo });

        var result = await service.CreateAsync(project.ProjectId, salesId, request);

        Assert.Equal(201, result.Status);
    }

    [Fact]
    public async Task GetListByProjectAsync_ReturnsUnauthorized_WhenUserIdIsEmpty()
    {
        var service = BuildService(new() { Role = "SALES" });

        var result = await service.GetListByProjectAsync(Guid.NewGuid(), Guid.Empty, new ProjectScheduleListQueryDto());

        Assert.Equal(401, result.Status);
    }

    [Fact]
    public async Task GetListByProjectAsync_ReturnsNotFound_WhenProjectDoesNotExist()
    {
        var service = BuildService(new() { Role = "ADMIN" });

        var result = await service.GetListByProjectAsync(Guid.NewGuid(), Guid.NewGuid(), new ProjectScheduleListQueryDto());

        Assert.Equal(404, result.Status);
    }

    [Fact]
    public async Task GetListByProjectAsync_ReturnsForbidden_WhenCustomerDoesNotOwnProject()
    {
        var project = CreateProject();
        var service = BuildService(new() { Role = "CUSTOMER", ProjectDetail = project });

        var result = await service.GetListByProjectAsync(project.ProjectId, Guid.NewGuid(), new ProjectScheduleListQueryDto());

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task GetListByProjectAsync_ReturnsSuccess_WhenCustomerIsOwner()
    {
        var customerId = Guid.NewGuid();
        var project = CreateProject(customerId: customerId);
        var service = BuildService(new() { Role = "CUSTOMER", ProjectDetail = project });

        var result = await service.GetListByProjectAsync(project.ProjectId, customerId, new ProjectScheduleListQueryDto());

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public async Task GetListByProjectAsync_ReturnsSuccess_ForAdmin()
    {
        var project = CreateProject();
        var service = BuildService(new() { Role = "ADMIN", ProjectDetail = project });

        var result = await service.GetListByProjectAsync(project.ProjectId, Guid.NewGuid(), new ProjectScheduleListQueryDto());

        Assert.Equal(200, result.Status);
    }

    [Fact]
    public async Task GetListByProjectAsync_ReturnsSuccess_ForAssignedProductionStaff()
    {
        var productionId = Guid.NewGuid();
        var project = CreateProject();
        var scheduleRepo = new FakeProjectScheduleRepository { HasAssignedSchedule = true };
        var service = BuildService(new()
        {
            Role = "PRODUCTION",
            ProjectDetail = project,
            ScheduleRepo = scheduleRepo
        });

        var result = await service.GetListByProjectAsync(project.ProjectId, productionId, new ProjectScheduleListQueryDto());

        Assert.Equal(200, result.Status);
        Assert.True(await scheduleRepo.HasAssignedScheduleAsync(project.ProjectId, productionId));
        Assert.Equal(project.ProjectId, scheduleRepo.LastAssignedScheduleProjectId);
        Assert.Equal(productionId, scheduleRepo.LastAssignedScheduleStaffId);
    }

    [Fact]
    public async Task GetListByProjectAsync_ReturnsSuccess_WhenProductionHasAssignedProductionRequest()
    {
        var productionId = Guid.NewGuid();
        var project = CreateProject();
        var productionRepo = new FakeProductionRequestRepository { HasViewableAssignedRequest = true };
        var service = BuildService(new()
        {
            Role = "PRODUCTION",
            ProjectDetail = project,
            ProductionRequestRepo = productionRepo
        });

        var result = await service.GetListByProjectAsync(project.ProjectId, productionId, new ProjectScheduleListQueryDto());

        Assert.Equal(200, result.Status);
        Assert.Equal(project.ProjectId, productionRepo.LastProjectId);
        Assert.Equal(productionId, productionRepo.LastProductionAccountId);
    }

    [Fact]
    public async Task GetListByProjectAsync_ReturnsForbidden_ForUnrelatedProductionStaff()
    {
        var project = CreateProject();
        var service = BuildService(new() { Role = "PRODUCTION", ProjectDetail = project });

        var result = await service.GetListByProjectAsync(project.ProjectId, Guid.NewGuid(), new ProjectScheduleListQueryDto());

        Assert.Equal(403, result.Status);
    }

    // ── SCH-03: GetDetail ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetDetailAsync_ReturnsNotFound_WhenScheduleDoesNotExist()
    {
        var service = BuildService(new() { Role = "ADMIN" });

        var result = await service.GetDetailAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(404, result.Status);
    }

    [Fact]
    public async Task GetDetailAsync_ReturnsForbidden_WhenUserHasNoAccess()
    {
        var detail = CreateScheduleDetail();
        var service = BuildService(new() { Role = "CUSTOMER", ScheduleDetail = detail });

        var result = await service.GetDetailAsync(detail.ScheduleId, Guid.NewGuid());

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task GetDetailAsync_ReturnsSuccess_WhenAdminAccesses()
    {
        var detail = CreateScheduleDetail();
        var service = BuildService(new() { Role = "ADMIN", ScheduleDetail = detail });

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
        var service = BuildService(new() { Role = "DESIGNER", ScheduleDetail = detail });

        var result = await service.GetDetailAsync(detail.ScheduleId, staffId);

        Assert.Equal(200, result.Status);
    }

    [Fact]
    public async Task GetDetailAsync_ReturnsForbidden_WhenUnrelatedProductionStaffAccesses()
    {
        var detail = CreateScheduleDetail(assignedStaffId: Guid.NewGuid());
        var service = BuildService(new() { Role = "PRODUCTION", ScheduleDetail = detail });

        var result = await service.GetDetailAsync(detail.ScheduleId, Guid.NewGuid());

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task GetDetailAsync_ReturnsSuccess_WhenProductionStaffHasProjectScheduleAssignment()
    {
        var productionId = Guid.NewGuid();
        var detail = CreateScheduleDetail(assignedStaffId: Guid.NewGuid());
        var scheduleRepo = new FakeProjectScheduleRepository(detail: detail) { HasAssignedSchedule = true };
        var service = BuildService(new()
        {
            Role = "PRODUCTION",
            ScheduleDetail = detail,
            ScheduleRepo = scheduleRepo
        });

        var result = await service.GetDetailAsync(detail.ScheduleId, productionId);

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(detail.ScheduleId, result.Data.ScheduleId);
    }

    [Fact]
    public async Task GetDetailAsync_ReturnsSuccess_WhenProductionHasAssignedProductionRequest()
    {
        var productionId = Guid.NewGuid();
        var detail = CreateScheduleDetail(assignedStaffId: Guid.NewGuid());
        var productionRepo = new FakeProductionRequestRepository { HasViewableAssignedRequest = true };
        var service = BuildService(new()
        {
            Role = "PRODUCTION",
            ScheduleDetail = detail,
            ProductionRequestRepo = productionRepo
        });

        var result = await service.GetDetailAsync(detail.ScheduleId, productionId);

        Assert.Equal(200, result.Status);
        Assert.Equal(detail.ProjectId, productionRepo.LastProjectId);
        Assert.Equal(productionId, productionRepo.LastProductionAccountId);
    }

    // ── SCH-04: Update ──────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_ReturnsNotFound_WhenScheduleDoesNotExist()
    {
        var service = BuildService(new() { Role = "ADMIN" });

        var result = await service.UpdateAsync(Guid.NewGuid(), Guid.NewGuid(), new UpdateProjectScheduleRequestDto());

        Assert.Equal(404, result.Status);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsForbidden_WhenSalesIsNotAssigned()
    {
        var detail = CreateScheduleDetail(assignedSalesId: Guid.NewGuid(), status: ProjectScheduleStatus.PENDING_CONFIRMATION);
        var service = BuildService(new() { Role = "SALES", ScheduleDetail = detail });

        var result = await service.UpdateAsync(detail.ScheduleId, Guid.NewGuid(), new UpdateProjectScheduleRequestDto());

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsBadRequest_WhenStatusIsCompleted_AndNotAdmin()
    {
        var salesId = Guid.NewGuid();
        var detail = CreateScheduleDetail(assignedSalesId: salesId, status: ProjectScheduleStatus.COMPLETED);
        var service = BuildService(new() { Role = "SALES", ScheduleDetail = detail });

        var result = await service.UpdateAsync(detail.ScheduleId, salesId, new UpdateProjectScheduleRequestDto());

        Assert.Equal(400, result.Status);
    }

    [Fact]
    public async Task UpdateAsync_ResetsStatusToPending_WhenScheduledStartChanges()
    {
        var salesId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var schedule = CreateScheduleEntity(status: ProjectScheduleStatus.CONFIRMED, scheduledStart: DateTime.UtcNow.AddDays(2));
        schedule.AssignedStaffId = staffId;
        var detail = CreateScheduleDetail(
            assignedSalesId: salesId,
            assignedStaffId: staffId,
            status: ProjectScheduleStatus.CONFIRMED,
            scheduleId: schedule.ScheduleId);
        var scheduleRepo = new FakeProjectScheduleRepository(entityById: schedule, detail: detail);
        var service = BuildService(new() { Role = "SALES", ScheduleDetail = detail, ScheduleRepo = scheduleRepo });

        var request = new UpdateProjectScheduleRequestDto
        {
            ScheduledStart = VietnamLocalAsUtc(dayOffset: 5, hour: 8),
            ScheduledEnd = VietnamLocalAsUtc(dayOffset: 5, hour: 10)
        };

        var result = await service.UpdateAsync(schedule.ScheduleId, salesId, request);

        Assert.Equal(200, result.Status);
        Assert.Equal(ProjectScheduleStatus.PENDING_CONFIRMATION, schedule.Status);
    }

    [Fact]
    public async Task UpdateAsync_ProductionUpdatesAssignedDeliverySchedule()
    {
        var productionId = Guid.NewGuid();
        var schedule = CreateScheduleEntity(ProjectScheduleStatus.CONFIRMED, scheduleType: ProjectScheduleType.DELIVERY);
        schedule.AssignedStaffId = productionId;
        var detail = CreateScheduleDetail(
            scheduleId: schedule.ScheduleId,
            assignedStaffId: productionId,
            status: ProjectScheduleStatus.CONFIRMED,
            scheduleType: ProjectScheduleType.DELIVERY);
        var scheduleRepo = new FakeProjectScheduleRepository(entityById: schedule, detail: detail);
        var service = BuildService(new() { Role = "PRODUCTION", ScheduleDetail = detail, ScheduleRepo = scheduleRepo });

        var result = await service.UpdateAsync(schedule.ScheduleId, productionId, new UpdateProjectScheduleRequestDto
        {
            Title = "Delivery window updated"
        });

        Assert.Equal(200, result.Status);
        Assert.Equal("Delivery window updated", schedule.Title);
    }

    [Fact]
    public async Task UpdateAsync_ProductionUpdatingMeasurement_ReturnsInvalidScheduleType()
    {
        var productionId = Guid.NewGuid();
        var detail = CreateScheduleDetail(assignedStaffId: productionId, scheduleType: ProjectScheduleType.MEASUREMENT);
        var service = BuildService(new() { Role = "PRODUCTION", ScheduleDetail = detail });

        var result = await service.UpdateAsync(detail.ScheduleId, productionId, new UpdateProjectScheduleRequestDto());

        Assert.Equal(400, result.Status);
        Assert.Equal(ProjectScheduleErrorCodes.InvalidScheduleType, result.ErrorCode);
    }

    [Fact]
    public async Task UpdateAsync_ProductionNotAssigned_ReturnsForbidden()
    {
        var productionId = Guid.NewGuid();
        var detail = CreateScheduleDetail(
            assignedStaffId: Guid.NewGuid(),
            scheduleType: ProjectScheduleType.DELIVERY);
        var service = BuildService(new() { Role = "PRODUCTION", ScheduleDetail = detail });

        var result = await service.UpdateAsync(detail.ScheduleId, productionId, new UpdateProjectScheduleRequestDto());

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task UpdateAsync_ProductionWithOnlyProductionRequestAccess_ReturnsForbidden()
    {
        var productionId = Guid.NewGuid();
        var detail = CreateScheduleDetail(
            assignedStaffId: Guid.NewGuid(),
            scheduleType: ProjectScheduleType.DELIVERY);
        var productionRepo = new FakeProductionRequestRepository { HasViewableAssignedRequest = true };
        var service = BuildService(new()
        {
            Role = "PRODUCTION",
            ScheduleDetail = detail,
            ProductionRequestRepo = productionRepo
        });

        var result = await service.UpdateAsync(detail.ScheduleId, productionId, new UpdateProjectScheduleRequestDto());

        Assert.Equal(403, result.Status);
        Assert.Null(productionRepo.LastProjectId);
    }

    [Fact]
    public async Task UpdateAsync_ProductionCannotReassign_ReturnsForbidden()
    {
        var productionId = Guid.NewGuid();
        var detail = CreateScheduleDetail(
            assignedStaffId: productionId,
            scheduleType: ProjectScheduleType.DELIVERY);
        var service = BuildService(new() { Role = "PRODUCTION", ScheduleDetail = detail });

        var result = await service.UpdateAsync(detail.ScheduleId, productionId, new UpdateProjectScheduleRequestDto
        {
            AssignedStaffId = Guid.NewGuid()
        });

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task UpdateAsync_ProductionCompletedSchedule_ReturnsBadRequest()
    {
        var productionId = Guid.NewGuid();
        var detail = CreateScheduleDetail(
            assignedStaffId: productionId,
            status: ProjectScheduleStatus.COMPLETED,
            scheduleType: ProjectScheduleType.DELIVERY);
        var service = BuildService(new() { Role = "PRODUCTION", ScheduleDetail = detail });

        var result = await service.UpdateAsync(detail.ScheduleId, productionId, new UpdateProjectScheduleRequestDto());

        Assert.Equal(400, result.Status);
    }

    [Fact]
    public async Task UpdateAsync_SalesNotAssigned_ReturnsForbidden()
    {
        var detail = CreateScheduleDetail(assignedSalesId: Guid.NewGuid());
        var service = BuildService(new() { Role = "SALES", ScheduleDetail = detail });

        var result = await service.UpdateAsync(detail.ScheduleId, Guid.NewGuid(), new UpdateProjectScheduleRequestDto());

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task UpdateAsync_SalesOnCompletedSchedule_ReturnsBadRequest()
    {
        var salesId = Guid.NewGuid();
        var detail = CreateScheduleDetail(
            assignedSalesId: salesId,
            status: ProjectScheduleStatus.COMPLETED);
        var service = BuildService(new() { Role = "SALES", ScheduleDetail = detail });

        var result = await service.UpdateAsync(detail.ScheduleId, salesId, new UpdateProjectScheduleRequestDto());

        Assert.Equal(400, result.Status);
    }

    // ── SCH-05: UpdateStatus ────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_WhenReassignedStaffHasOverlap_ReturnsConflict()
    {
        var salesId = Guid.NewGuid();
        var originalStaffId = Guid.NewGuid();
        var newStaffId = Guid.NewGuid();
        var start = DateTime.UtcNow.AddDays(2);
        var schedule = CreateScheduleEntity(
            status: ProjectScheduleStatus.CONFIRMED,
            scheduledStart: start,
            scheduleType: ProjectScheduleType.CONSULTATION);
        schedule.AssignedStaffId = originalStaffId;
        schedule.ScheduledEnd = start.AddHours(2);
        var detail = CreateScheduleDetail(
            scheduleId: schedule.ScheduleId,
            assignedSalesId: salesId,
            assignedStaffId: originalStaffId,
            status: ProjectScheduleStatus.CONFIRMED,
            scheduleType: ProjectScheduleType.CONSULTATION,
            scheduledStart: start,
            scheduledEnd: start.AddHours(2));
        var scheduleRepo = new FakeProjectScheduleRepository(entityById: schedule, detail: detail);
        scheduleRepo.AddExistingSchedule(CreateScheduleEntity(
            Guid.NewGuid(),
            newStaffId,
            ProjectScheduleStatus.CONFIRMED,
            start.AddHours(1),
            start.AddHours(3)));
        var service = BuildService(new() { Role = "SALES", ScheduleDetail = detail, ScheduleRepo = scheduleRepo });

        var result = await service.UpdateAsync(schedule.ScheduleId, salesId, new UpdateProjectScheduleRequestDto
        {
            AssignedStaffId = newStaffId
        });

        Assert.Equal(409, result.Status);
        Assert.Equal(ProjectScheduleErrorCodes.StaffScheduleOverlap, result.ErrorCode);
    }

    [Fact]
    public async Task UpdateAsync_WhenOnlyCurrentScheduleOverlaps_ExcludesItself()
    {
        var salesId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var start = DateTime.UtcNow.AddDays(2);
        var schedule = CreateScheduleEntity(
            status: ProjectScheduleStatus.CONFIRMED,
            scheduledStart: start,
            scheduleType: ProjectScheduleType.CONSULTATION);
        schedule.AssignedStaffId = staffId;
        schedule.ScheduledEnd = start.AddHours(2);
        var detail = CreateScheduleDetail(
            scheduleId: schedule.ScheduleId,
            assignedSalesId: salesId,
            assignedStaffId: staffId,
            status: ProjectScheduleStatus.CONFIRMED,
            scheduleType: ProjectScheduleType.CONSULTATION,
            scheduledStart: start,
            scheduledEnd: start.AddHours(2));
        var scheduleRepo = new FakeProjectScheduleRepository(entityById: schedule, detail: detail);
        scheduleRepo.AddExistingSchedule(CreateScheduleEntity(
            detail.ProjectId,
            staffId,
            ProjectScheduleStatus.CONFIRMED,
            start,
            start.AddHours(2),
            schedule.ScheduleId));
        var service = BuildService(new() { Role = "SALES", ScheduleDetail = detail, ScheduleRepo = scheduleRepo });

        var result = await service.UpdateAsync(schedule.ScheduleId, salesId, new UpdateProjectScheduleRequestDto
        {
            ScheduledStart = start.AddHours(3),
            ScheduledEnd = start.AddHours(4)
        });

        Assert.Equal(200, result.Status);
    }

    [Fact]
    public async Task UpdateStatusAsync_ReturnsBadRequest_WhenStatusIsAlreadyTerminal()
    {
        var detail = CreateScheduleDetail(status: ProjectScheduleStatus.CANCELLED);
        var service = BuildService(new() { Role = "ADMIN", ScheduleDetail = detail });

        var result = await service.UpdateStatusAsync(detail.ScheduleId, Guid.NewGuid(),
            new UpdateProjectScheduleStatusRequestDto { Status = ProjectScheduleStatus.CONFIRMED });

        Assert.Equal(400, result.Status);
    }

    [Fact]
    public async Task UpdateStatusAsync_ReturnsForbidden_WhenSalesTryToConfirm()
    {
        var salesId = Guid.NewGuid();
        var detail = CreateScheduleDetail(assignedSalesId: salesId, status: ProjectScheduleStatus.PENDING_CONFIRMATION);
        var service = BuildService(new() { Role = "SALES", ScheduleDetail = detail });

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
        var service = BuildService(new() { Role = "CUSTOMER", ScheduleDetail = detail, ScheduleRepo = scheduleRepo });

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
        var service = BuildService(new() { Role = "CUSTOMER", ScheduleDetail = detail, ScheduleRepo = scheduleRepo });

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
        var service = BuildService(new() { Role = "ADMIN", ScheduleDetail = detail });

        var result = await service.UpdateStatusAsync(detail.ScheduleId, Guid.NewGuid(),
            new UpdateProjectScheduleStatusRequestDto { Status = ProjectScheduleStatus.COMPLETED });

        Assert.Equal(400, result.Status);
    }

    [Fact]
    public async Task UpdateStatusAsync_ProductionCompletesAssignedDeliverySchedule()
    {
        var productionId = Guid.NewGuid();
        var startedAt = DateTime.UtcNow.AddHours(-2);
        var schedule = CreateScheduleEntity(
            ProjectScheduleStatus.CONFIRMED,
            scheduledStart: startedAt,
            scheduleType: ProjectScheduleType.DELIVERY);
        schedule.AssignedStaffId = productionId;
        var detail = CreateScheduleDetail(
            scheduleId: schedule.ScheduleId,
            assignedStaffId: productionId,
            status: ProjectScheduleStatus.CONFIRMED,
            scheduleType: ProjectScheduleType.DELIVERY,
            scheduledStart: startedAt);
        var scheduleRepo = new FakeProjectScheduleRepository(entityById: schedule, detail: detail);
        var deliveryRepo = new FakeDeliveryRepository
        {
            LinkedDelivery = new Delivery
            {
                DeliveryId = Guid.NewGuid(),
                OrderId = Guid.NewGuid(),
                ProjectScheduleId = schedule.ScheduleId,
                Status = DeliveryStatus.COMPLETED
            }
        };
        var service = BuildService(new()
        {
            Role = "PRODUCTION",
            ScheduleDetail = detail,
            ScheduleRepo = scheduleRepo,
            DeliveryRepo = deliveryRepo
        });

        var result = await service.UpdateStatusAsync(
            schedule.ScheduleId,
            productionId,
            new UpdateProjectScheduleStatusRequestDto { Status = ProjectScheduleStatus.COMPLETED });

        Assert.Equal(200, result.Status);
        Assert.Equal(ProjectScheduleStatus.COMPLETED, schedule.Status);
    }

    [Fact]
    public async Task UpdateStatusAsync_ProductionCompletingOtherSchedule_ReturnsInvalidScheduleType()
    {
        var productionId = Guid.NewGuid();
        var detail = CreateScheduleDetail(
            assignedStaffId: productionId,
            status: ProjectScheduleStatus.CONFIRMED,
            scheduleType: ProjectScheduleType.OTHER);
        var service = BuildService(new() { Role = "PRODUCTION", ScheduleDetail = detail });

        var result = await service.UpdateStatusAsync(
            detail.ScheduleId,
            productionId,
            new UpdateProjectScheduleStatusRequestDto { Status = ProjectScheduleStatus.COMPLETED });

        Assert.Equal(400, result.Status);
        Assert.Equal(ProjectScheduleErrorCodes.InvalidScheduleType, result.ErrorCode);
    }

    [Fact]
    public async Task RequestChangeAsync_OwnerCustomer_MovesDeliveryScheduleToPendingConfirmation()
    {
        var customerId = Guid.NewGuid();
        var schedule = CreateScheduleEntity(
            status: ProjectScheduleStatus.CONFIRMED,
            scheduleType: ProjectScheduleType.DELIVERY);
        schedule.CustomerNote = "Old note";
        var detail = CreateScheduleDetail(
            scheduleId: schedule.ScheduleId,
            customerId: customerId,
            status: ProjectScheduleStatus.CONFIRMED,
            scheduleType: ProjectScheduleType.DELIVERY);
        var scheduleRepo = new FakeProjectScheduleRepository(detail: detail, entityById: schedule);
        var service = BuildService(new()
        {
            Role = "CUSTOMER",
            ScheduleDetail = detail,
            ScheduleRepo = scheduleRepo
        });

        var result = await service.RequestChangeAsync(
            schedule.ScheduleId,
            customerId,
            new RequestProjectScheduleChangeDto { Note = " Please deliver after 15:00. " });

        Assert.Equal(200, result.Status);
        Assert.Equal(ProjectScheduleStatus.PENDING_CONFIRMATION, schedule.Status);
        Assert.Equal("Please deliver after 15:00.", schedule.CustomerNote);
        Assert.Equal(schedule.CustomerNote, result.Data!.CustomerNote);
    }

    [Fact]
    public async Task RequestChangeAsync_BlankNote_ReturnsNoteRequired()
    {
        var service = BuildService(new() { Role = "CUSTOMER" });

        var result = await service.RequestChangeAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new RequestProjectScheduleChangeDto { Note = " " });

        Assert.Equal(400, result.Status);
        Assert.Equal(ProjectScheduleErrorCodes.ScheduleChangeNoteRequired, result.ErrorCode);
    }

    [Fact]
    public async Task RequestChangeAsync_NonOwnerCustomer_ReturnsForbidden()
    {
        var detail = CreateScheduleDetail(
            customerId: Guid.NewGuid(),
            scheduleType: ProjectScheduleType.DELIVERY);
        var service = BuildService(new() { Role = "CUSTOMER", ScheduleDetail = detail });

        var result = await service.RequestChangeAsync(
            detail.ScheduleId,
            Guid.NewGuid(),
            new RequestProjectScheduleChangeDto { Note = "Change request" });

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task RequestChangeAsync_AfterExecutionStarted_ReturnsConflict()
    {
        var customerId = Guid.NewGuid();
        var detail = CreateScheduleDetail(
            customerId: customerId,
            scheduleType: ProjectScheduleType.DELIVERY);
        var service = BuildService(new()
        {
            Role = "CUSTOMER",
            ScheduleDetail = detail,
            DeliveryRepo = new FakeDeliveryRepository { HasLinkedDelivery = true }
        });

        var result = await service.RequestChangeAsync(
            detail.ScheduleId,
            customerId,
            new RequestProjectScheduleChangeDto { Note = "Change request" });

        Assert.Equal(409, result.Status);
        Assert.Equal(ProjectScheduleErrorCodes.DeliveryInProgressBlocksScheduleCancel, result.ErrorCode);
    }

    // ── SCH-06: GetMyAssigned ───────────────────────────────────────────────────

    [Fact]
    public async Task GetMyAssignedAsync_ReturnsUnauthorized_WhenUserIdIsEmpty()
    {
        var service = BuildService(new() { Role = "SALES" });

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
        var service = BuildService(new() { Role = "SALES", ScheduleRepo = scheduleRepo });

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
        var service = BuildService(new() { Role = "ADMIN", ScheduleRepo = scheduleRepo });

        await service.GetMyAssignedAsync(Guid.NewGuid(), new ProjectScheduleListQueryDto());

        Assert.Null(scheduleRepo.LastMyAssignedStaffId);
    }

    // ── Measurement rules (SCRUM-165 / SCRUM-164) ─────────────────────────────

    [Fact]
    public async Task CreateAsync_Measurement_ReturnsDesignerNotAssigned_WhenProjectHasNoDesigner()
    {
        var salesId = Guid.NewGuid();
        var project = CreateProject(assignedSalesId: salesId, status: ProjectStatus.MEASUREMENT_REQUIRED);
        var service = BuildService(new() { Role = "SALES", ProjectDetail = project });

        var result = await service.CreateAsync(
            project.ProjectId,
            salesId,
            ValidMeasurementCreateRequest(Guid.NewGuid()));

        Assert.Equal(400, result.Status);
        Assert.Equal(ProjectScheduleErrorCodes.DesignerNotAssigned, result.ErrorCode);
    }

    [Fact]
    public async Task CreateAsync_Measurement_ReturnsInvalidProjectStatus_WhenNotMeasurementRequired()
    {
        var salesId = Guid.NewGuid();
        var designerId = Guid.NewGuid();
        var project = CreateProject(
            assignedSalesId: salesId,
            assignedDesignerId: designerId,
            status: ProjectStatus.SPACE_VERIFIED);
        var service = BuildService(new() { Role = "SALES", ProjectDetail = project });

        var result = await service.CreateAsync(
            project.ProjectId,
            salesId,
            ValidMeasurementCreateRequest(designerId));

        Assert.Equal(400, result.Status);
        Assert.Equal(ProjectScheduleErrorCodes.InvalidProjectStatus, result.ErrorCode);
    }

    [Fact]
    public async Task UpdateStatusAsync_CompleteMeasurement_ReturnsCanMoveToProposalConsulting_WhenGatePasses()
    {
        var salesId = Guid.NewGuid();
        var designerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var scheduleId = Guid.NewGuid();
        var startedAt = DateTime.UtcNow.AddHours(-2);
        var detail = CreateScheduleDetail(
            scheduleId,
            assignedSalesId: salesId,
            assignedDesignerId: designerId,
            assignedStaffId: designerId,
            status: ProjectScheduleStatus.CONFIRMED,
            scheduledStart: startedAt);
        detail.ProjectId = projectId;
        detail.ScheduleType = ProjectScheduleType.MEASUREMENT;
        var schedule = new ProjectSchedule
        {
            ScheduleId = scheduleId,
            ProjectId = projectId,
            ScheduledStart = startedAt,
            Status = ProjectScheduleStatus.CONFIRMED
        };
        var projectEntity = new Project
        {
            ProjectId = projectId,
            CustomerId = detail.CustomerId,
            AssignedSalesId = salesId,
            AssignedDesignerId = designerId,
            Status = ProjectStatus.MEASUREMENT_REQUIRED,
            ProjectName = "Test"
        };
        var scheduleRepo = new FakeProjectScheduleRepository(detail: detail, entityById: schedule)
        {
            HasCompletedMeasurement = true
        };
        var projectRepo = new FakeProjectRepository(role: "SALES", entity: projectEntity);
        var service = BuildService(new() { ScheduleRepo = scheduleRepo, ProjectRepo = projectRepo });

        var result = await service.UpdateStatusAsync(
            scheduleId,
            salesId,
            new UpdateProjectScheduleStatusRequestDto { Status = ProjectScheduleStatus.COMPLETED });

        Assert.Equal(200, result.Status);
        Assert.True(result.Data!.CanMoveToProposalConsulting);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    private sealed class ScheduleServiceTestOptions
    {
        public string? Role { get; init; }
        public ProjectDetailReadModel? ProjectDetail { get; init; }
        public ProjectScheduleDetailReadModel? ScheduleDetail { get; init; }
        public FakeProjectScheduleRepository? ScheduleRepo { get; init; }
        public FakeNotificationDispatcher? Dispatcher { get; init; }
        public FakeProjectRepository? ProjectRepo { get; init; }
        public FakeOrderRepository? OrderRepo { get; init; }
        public FakeProductionRequestRepository? ProductionRequestRepo { get; init; }

        public FakeDeliveryRepository? DeliveryRepo { get; init; }
    }

    private static ProjectScheduleService BuildService(ScheduleServiceTestOptions? options = null)
    {
        options ??= new ScheduleServiceTestOptions();
        var scheduleRepo = options.ScheduleRepo ?? new FakeProjectScheduleRepository(detail: options.ScheduleDetail);
        var projectDetail = options.ProjectDetail;
        if (projectDetail is null && options.ScheduleDetail is not null)
        {
            projectDetail = new ProjectDetailReadModel
            {
                ProjectId = options.ScheduleDetail.ProjectId,
                CustomerId = options.ScheduleDetail.CustomerId,
                AssignedSalesId = options.ScheduleDetail.AssignedSalesId,
                AssignedDesignerId = options.ScheduleDetail.AssignedDesignerId,
                ProjectName = options.ScheduleDetail.ProjectName,
                TargetCompletionDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1))
            };
        }

        var projectRepo = options.ProjectRepo ?? new FakeProjectRepository(role: options.Role, detail: projectDetail);
        var fileRepo = new FakeProjectFileRepository();
        var dispatcher = options.Dispatcher ?? new FakeNotificationDispatcher();
        return new ProjectScheduleService(
            scheduleRepo,
            projectRepo,
            fileRepo,
            options.OrderRepo ?? new FakeOrderRepository(),
            options.ProductionRequestRepo ?? new FakeProductionRequestRepository(),
            options.DeliveryRepo ?? new FakeDeliveryRepository(),
            new ProjectScheduleServiceDependencies(
                global::FurniSpace.Application.Tests.TestDoubles.TestUnitOfWork.ForSaveChanges(scheduleRepo.SaveChangesAsync),
                dispatcher,
                new ProjectWorkflowSettings()));
    }

    private static CreateProjectScheduleRequestDto ValidMeasurementCreateRequest(Guid designerId) => new()
    {
        ScheduleType = ProjectScheduleType.MEASUREMENT,
        Title = "First measurement",
        AssignedStaffId = designerId,
        ScheduledStart = VietnamLocalAsUtc(hour: 8),
        ScheduledEnd = VietnamLocalAsUtc(hour: 10),
        Location = "123 Test St"
    };

    private static ProjectScheduleService BuildDeliveryScheduleService(ProjectDetailReadModel project)
    {
        return BuildService(new()
        {
            Role = "PRODUCTION",
            ProjectDetail = project,
            OrderRepo = new FakeOrderRepository(hasProjectOrderInStatuses: true),
            ProductionRequestRepo = new FakeProductionRequestRepository
            {
                HasAssignedCompletedProduction = true
            }
        });
    }

    private static DateTime VietnamLocalAsUtc(int dayOffset = 1, int hour = 8, int minute = 0)
    {
        var now = DateTime.UtcNow.AddDays(dayOffset);
        var local = new DateTime(now.Year, now.Month, now.Day, hour, minute, 0, DateTimeKind.Unspecified);
        return DateTime.SpecifyKind(local.AddHours(-7), DateTimeKind.Utc);
    }

    private static CreateProjectScheduleRequestDto ValidCreateRequest() => new()
    {
        ScheduleType = ProjectScheduleType.CONSULTATION,
        Title = "Consultation visit",
        AssignedStaffId = Guid.NewGuid(),
        ScheduledStart = DateTime.UtcNow.AddDays(1),
        ScheduledEnd = DateTime.UtcNow.AddDays(1).AddHours(2),
        Location = "123 Test St"
    };

    private static CreateProjectScheduleRequestDto ValidProductionCreateRequest(
        ProjectScheduleType scheduleType,
        Guid assignedStaffId) => new()
    {
        ScheduleType = scheduleType,
        Title = "Production schedule",
        AssignedStaffId = assignedStaffId,
        ScheduledStart = VietnamLocalAsUtc(hour: 8),
        ScheduledEnd = VietnamLocalAsUtc(hour: 10),
        Location = "Factory"
    };

    private static CreateProjectScheduleRequestDto ValidDeliveryCreateRequest() => new()
    {
        ScheduleType = ProjectScheduleType.DELIVERY,
        Title = "First delivery round",
        AssignedStaffId = Guid.NewGuid(),
        ScheduledStart = VietnamLocalAsUtc(hour: 8),
        ScheduledEnd = VietnamLocalAsUtc(hour: 12),
        Location = "Customer project address",
        Description = "Deliver completed tables and chairs."
    };

    private static ProjectDetailReadModel CreateProject(
        Guid? customerId = null,
        Guid? assignedSalesId = null,
        Guid? assignedDesignerId = null,
        ProjectStatus? status = null)
    {
        var id = Guid.NewGuid();
        return new ProjectDetailReadModel
        {
            ProjectId = id,
            CustomerId = customerId ?? Guid.NewGuid(),
            AssignedSalesId = assignedSalesId,
            AssignedDesignerId = assignedDesignerId,
            Status = status,
            ProjectName = "Test Project",
            TargetCompletionDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1))
        };
    }

    private static ProjectScheduleDetailReadModel CreateScheduleDetail(
        Guid? scheduleId = null,
        Guid? customerId = null,
        Guid? assignedSalesId = null,
        Guid? assignedDesignerId = null,
        Guid? assignedStaffId = null,
        ProjectScheduleStatus status = ProjectScheduleStatus.PENDING_CONFIRMATION,
        ProjectScheduleType scheduleType = ProjectScheduleType.MEASUREMENT,
        DateTime? scheduledStart = null,
        DateTime? scheduledEnd = null)
    {
        var effectiveStart = scheduledStart ?? VietnamLocalAsUtc(hour: 8);
        return new ProjectScheduleDetailReadModel
        {
            ScheduleId = scheduleId ?? Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            ProjectName = "Test Project",
            CustomerId = customerId ?? Guid.NewGuid(),
            AssignedSalesId = assignedSalesId,
            AssignedDesignerId = assignedDesignerId,
            AssignedStaffId = assignedStaffId,
            ScheduleType = scheduleType,
            Title = "Test Schedule",
            ScheduledStart = effectiveStart,
            ScheduledEnd = scheduledEnd ?? effectiveStart.AddHours(2),
            Status = status
        };
    }

    private static ProjectSchedule CreateScheduleEntity(
        Guid projectId,
        Guid staffId,
        ProjectScheduleStatus status,
        DateTime scheduledStart,
        DateTime? scheduledEnd,
        Guid? scheduleId = null,
        ProjectScheduleType scheduleType = ProjectScheduleType.CONSULTATION)
    {
        return new ProjectSchedule
        {
            ScheduleId = scheduleId ?? Guid.NewGuid(),
            ProjectId = projectId,
            ScheduleType = scheduleType,
            Title = "Existing schedule",
            AssignedStaffId = staffId,
            ScheduledStart = scheduledStart,
            ScheduledEnd = scheduledEnd,
            Status = status,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static ProjectSchedule CreateScheduleEntity(
        ProjectScheduleStatus status = ProjectScheduleStatus.PENDING_CONFIRMATION,
        DateTime? scheduledStart = null,
        ProjectScheduleType scheduleType = ProjectScheduleType.MEASUREMENT)
    {
        var effectiveStart = scheduledStart ?? VietnamLocalAsUtc(hour: 8);
        return new ProjectSchedule
        {
            ScheduleId = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            ScheduleType = scheduleType,
            Title = "Test",
            ScheduledStart = effectiveStart,
            ScheduledEnd = effectiveStart.AddHours(2),
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
        public int RemoveCallCount { get; private set; }
        public int SaveChangesCallCount { get; private set; }
        public Guid? LastMyAssignedStaffId { get; private set; }
        public Guid? LastAssignedScheduleProjectId { get; private set; }
        public Guid? LastAssignedScheduleStaffId { get; private set; }
        public bool HasCompletedMeasurement { get; set; }
        public bool HasAssignedSchedule { get; set; }
        public bool HasConfirmedDeliverySchedule { get; set; }

        public void AddExistingSchedule(ProjectSchedule schedule)
        {
            _entities.Add(schedule);
        }

        public Task<bool> HasCompletedMeasurementScheduleAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(HasCompletedMeasurement);

        public Task<bool> ExistsMeasurementScheduleAsync(
            Guid projectId,
            ProjectScheduleStatus? status,
            CancellationToken cancellationToken = default)
            => Task.FromResult(HasCompletedMeasurement && status == ProjectScheduleStatus.COMPLETED);

        public Task<bool> HasAssignedScheduleAsync(
            Guid projectId,
            Guid staffId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastAssignedScheduleProjectId = projectId;
            LastAssignedScheduleStaffId = staffId;
            return Task.FromResult(HasAssignedSchedule ||
                _entities.Any(schedule => schedule.ProjectId == projectId && schedule.AssignedStaffId == staffId));
        }

        public Task<bool> HasConfirmedDeliveryScheduleAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(HasConfirmedDeliverySchedule ||
                _entities.Any(schedule =>
                    schedule.ProjectId == projectId &&
                    schedule.ScheduleType == ProjectScheduleType.DELIVERY &&
                    schedule.Status == ProjectScheduleStatus.CONFIRMED));
        }

        public Task<bool> HasActiveDeliveryScheduleAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_entities.Any(schedule =>
                schedule.ProjectId == projectId &&
                schedule.ScheduleType == ProjectScheduleType.DELIVERY &&
                schedule.Status is ProjectScheduleStatus.PENDING_CONFIRMATION
                    or ProjectScheduleStatus.CONFIRMED));
        }

        public Task<bool> HasActiveStaffOverlapAsync(
            Guid assignedStaffId,
            DateTime scheduledStart,
            DateTime? scheduledEnd,
            Guid? excludedScheduleId = null,
            CancellationToken cancellationToken = default)
        {
            var newEnd = scheduledEnd ?? scheduledStart;
            var hasOverlap = _entities.Any(schedule =>
                schedule.AssignedStaffId == assignedStaffId &&
                schedule.ScheduleId != excludedScheduleId &&
                schedule.Status is ProjectScheduleStatus.PENDING_CONFIRMATION or ProjectScheduleStatus.CONFIRMED &&
                scheduledStart < (schedule.ScheduledEnd ?? schedule.ScheduledStart) &&
                newEnd > schedule.ScheduledStart);

            return Task.FromResult(hasOverlap);
        }

        public Task<StaffScheduleConflictKind> GetStaffScheduleConflictAsync(
            Guid assignedStaffId,
            DateTime scheduledStart,
            DateTime scheduledEnd,
            Guid? excludedScheduleId = null,
            CancellationToken cancellationToken = default)
        {
            var schedules = GetAppointmentConflictCandidates(excludedScheduleId)
                .Where(schedule => schedule.AssignedStaffId == assignedStaffId);

            return Task.FromResult(EvaluateConflict(schedules, scheduledStart, scheduledEnd));
        }

        public Task<StaffScheduleConflictKind> GetCustomerScheduleConflictAsync(
            Guid customerId,
            DateTime scheduledStart,
            DateTime scheduledEnd,
            Guid? excludedScheduleId = null,
            CancellationToken cancellationToken = default)
        {
            var schedules = GetAppointmentConflictCandidates(excludedScheduleId)
                .Where(schedule => IsProjectOwnedByCustomer(schedule.ProjectId, customerId));

            return Task.FromResult(EvaluateConflict(schedules, scheduledStart, scheduledEnd));
        }

        private IEnumerable<ProjectSchedule> GetAppointmentConflictCandidates(Guid? excludedScheduleId)
        {
            return _entities.Where(schedule =>
                schedule.ScheduleId != excludedScheduleId &&
                schedule.Status != ProjectScheduleStatus.CANCELLED &&
                schedule.ScheduleType is ProjectScheduleType.MEASUREMENT or ProjectScheduleType.DELIVERY &&
                (schedule.Status == ProjectScheduleStatus.PENDING_CONFIRMATION ||
                 schedule.Status == ProjectScheduleStatus.CONFIRMED ||
                 (schedule.Status == ProjectScheduleStatus.COMPLETED && schedule.CompletedAt.HasValue)) &&
                (schedule.Status == ProjectScheduleStatus.COMPLETED || schedule.ScheduledEnd.HasValue));
        }

        private bool IsProjectOwnedByCustomer(Guid projectId, Guid customerId)
        {
            if (ProjectCustomerIds.TryGetValue(projectId, out var mappedCustomerId))
            {
                return mappedCustomerId == customerId;
            }

            return ProjectCustomerIds.Count == 0;
        }

        private static StaffScheduleConflictKind EvaluateConflict(
            IEnumerable<ProjectSchedule> schedules,
            DateTime scheduledStart,
            DateTime scheduledEnd)
        {
            foreach (var schedule in schedules)
            {
                var existingEnd = schedule.Status == ProjectScheduleStatus.COMPLETED && schedule.CompletedAt.HasValue
                    ? schedule.CompletedAt.Value
                    : schedule.ScheduledEnd ?? schedule.ScheduledStart;
                if (scheduledStart < existingEnd && scheduledEnd > schedule.ScheduledStart)
                {
                    return StaffScheduleConflictKind.Overlap;
                }

                if (scheduledStart < existingEnd.AddHours(2) &&
                    scheduledEnd.AddHours(2) > schedule.ScheduledStart)
                {
                    return StaffScheduleConflictKind.MinimumGapNotMet;
                }
            }

            return StaffScheduleConflictKind.None;
        }

        public Dictionary<Guid, Guid> ProjectCustomerIds { get; } = new();

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
        public void Remove(ProjectSchedule entity)
        {
            RemoveCallCount++;
            _entities.Remove(entity);
        }

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

    private sealed class FakeProductionRequestRepository : IProductionRequestRepository
    {
        public bool HasViewableAssignedRequest { get; set; }
        public bool HasAssignedCompletedProduction { get; set; }
        public Guid? LastProjectId { get; private set; }
        public Guid? LastProductionAccountId { get; private set; }

        public Task<bool> HasViewableAssignedRequestAsync(
            Guid projectId,
            Guid productionAccountId,
            CancellationToken cancellationToken = default)
        {
            LastProjectId = projectId;
            LastProductionAccountId = productionAccountId;
            return Task.FromResult(HasViewableAssignedRequest);
        }

        public Task<bool> HasAssignedCompletedProductionForProjectAsync(
            Guid projectId,
            Guid productionAccountId,
            CancellationToken cancellationToken = default)
        {
            LastProjectId = projectId;
            LastProductionAccountId = productionAccountId;
            return Task.FromResult(HasAssignedCompletedProduction);
        }

        public Task<bool> HasActiveRequestForOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<int> CountCreatedOnAsync(DateOnly date, CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<List<OrderItem>> GetProductOrderItemsAsync(Guid orderId, CancellationToken cancellationToken = default)
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

        public void UpdateItem(ProductionItem item) { }

        public IQueryable<ProductionRequest> Query() => Enumerable.Empty<ProductionRequest>().AsQueryable();
        public Task<ProductionRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<ProductionRequest?>(null);
        public Task<IReadOnlyList<ProductionRequest>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProductionRequest>>([]);
        public Task AddAsync(ProductionRequest entity, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public Task AddRangeAsync(IEnumerable<ProductionRequest> entities, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public void Update(ProductionRequest entity) { }
        public void Remove(ProductionRequest entity) { }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(0);
    }

    private sealed class FakeProjectRepository : IProjectRepository
    {
        private readonly string? _role;
        private readonly ProjectDetailReadModel? _detail;
        private readonly Project? _entity;

        public FakeProjectRepository(
            string? role = null,
            ProjectDetailReadModel? detail = null,
            Project? entity = null)
        {
            _role = role;
            _detail = detail;
            _entity = entity;
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
            => Task.FromResult(_entity?.ProjectId == id ? _entity : null);
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

    private sealed class FakeOrderRepository(
        bool hasProjectOrderInStatuses = false,
        bool hasCompletedDeliveryFlow = false,
        int remainingQuantity = 10) : IOrderRepository
    {
        public Guid LastProjectId { get; private set; }

        public Task<bool> HasProjectOrderInStatusesAsync(
            Guid projectId,
            IReadOnlyCollection<OrderStatus> statuses,
            CancellationToken cancellationToken = default)
        {
            LastProjectId = projectId;
            return Task.FromResult(hasProjectOrderInStatuses);
        }

        public Task<Order?> GetLatestByProjectInStatusesAsync(
            Guid projectId,
            IReadOnlyCollection<OrderStatus> statuses,
            CancellationToken cancellationToken = default)
        {
            LastProjectId = projectId;
            return Task.FromResult(hasProjectOrderInStatuses
                ? new Order
                {
                    OrderId = Guid.NewGuid(),
                    ProjectId = projectId,
                    Status = OrderStatus.READY_FOR_DELIVERY
                }
                : null);
        }

        public Task<int> GetTotalRemainingDeliverableQuantityAsync(
            Guid orderId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(hasCompletedDeliveryFlow ? 0 : remainingQuantity);
        }

        public Task<bool> HasCompletedDeliveryFlowAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(hasCompletedDeliveryFlow);
        }

        public IQueryable<Order> Query() => Enumerable.Empty<Order>().AsQueryable();
        public Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<Order?>(null);
        public Task<IReadOnlyList<Order>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Order>>([]);
        public Task AddAsync(Order entity, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public Task AddRangeAsync(IEnumerable<Order> entities, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public void Update(Order entity) { }
        public void Remove(Order entity) { }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(0);
        public Task<IReadOnlyList<OrderListItemReadModel>> GetByProjectAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<OrderListItemReadModel>>([]);
        public Task<OrderDetailReadModel?> GetDetailAsync(Guid orderId, CancellationToken cancellationToken = default)
            => Task.FromResult<OrderDetailReadModel?>(null);
        public Task<bool> ExistsForQuotationAsync(Guid quotationId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
        public Task AddItemAsync(OrderItem item, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeNotificationDispatcher : INotificationDispatcher
    {
        public int DispatchCallCount { get; private set; }
        public NotificationType LastType { get; private set; }

        public Task DispatchAsync(
            NotificationType type,
            IReadOnlyDictionary<string, string> parameters,
            IEnumerable<Guid> receiverIds,
            NotificationDispatchRequest? request = null,
            CancellationToken cancellationToken = default)
        {
            DispatchCallCount++;
            LastType = type;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeDeliveryRepository : IDeliveryRepository
    {
        public Delivery? LinkedDelivery { get; set; }
        public bool HasLinkedDelivery { get; set; }

        public Task AddAsync(Delivery delivery, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddItemAsync(DeliveryItem item, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<DeliveryDetailReadModel?> GetDetailAsync(Guid orderId, Guid deliveryId, CancellationToken cancellationToken = default)
            => Task.FromResult<DeliveryDetailReadModel?>(null);
        public Task<IReadOnlyList<DeliveryListItemReadModel>> GetByOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DeliveryListItemReadModel>>([]);
        public Task<Delivery?> GetByIdAsync(Guid deliveryId, CancellationToken cancellationToken = default)
            => Task.FromResult<Delivery?>(null);
        public Task<Delivery?> GetByProjectScheduleIdAsync(Guid projectScheduleId, CancellationToken cancellationToken = default)
            => Task.FromResult(LinkedDelivery?.ProjectScheduleId == projectScheduleId ? LinkedDelivery : null);
        public Task<bool> ExistsByProjectScheduleIdAsync(Guid projectScheduleId, CancellationToken cancellationToken = default)
            => Task.FromResult(HasLinkedDelivery ||
                (LinkedDelivery is not null && LinkedDelivery.ProjectScheduleId == projectScheduleId));
        public Task<IReadOnlyList<DeliveryItem>> GetItemsByDeliveryAsync(Guid deliveryId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DeliveryItem>>([]);
        public Task<DeliveryItem?> GetItemByIdAsync(Guid deliveryItemId, CancellationToken cancellationToken = default)
            => Task.FromResult<DeliveryItem?>(null);
        public void Update(Delivery delivery) { }
    }
}
