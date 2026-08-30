#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.DTOs.Dashboard;
using FurniSpace.Application.Services.Dashboard;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.ReadModels.Dashboard;
using FurniSpace.Infrastructure.ReadModels.Projects;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Xunit;

namespace FurniSpace.Application.Tests.Dashboard;

public sealed class DashboardQueueServiceTests
{
    [Fact]
    public async Task GetSalesActionQueueAsync_MapsItemsAndCountsByGroup()
    {
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var dashboard = new FakeDashboardQueueReadRepository
        {
            SalesRows =
            [
                new DashboardProjectQueueRowReadModel
                {
                    ProjectId = projectId,
                    ProjectCode = "PRJ-001",
                    ProjectName = "Showroom A",
                    Status = ProjectStatus.SUBMITTED,
                    CustomerName = "Customer A",
                    AssignedSalesName = "Sales One",
                    UpdatedAt = DateTime.UtcNow
                }
            ]
        };
        var projects = new FakeProjectRepository { RoleName = "SALES" };
        var service = new DashboardQueueService(dashboard, projects);

        var result = await service.GetSalesActionQueueAsync(
            userId,
            new DashboardQueueQueryDto { Scope = "mine", Page = 1, Limit = 20 });

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data.Items);
        Assert.Equal("Review request", result.Data.Items[0].Action);
        Assert.Equal("Customer A", result.Data.Items[0].CustomerName);
        Assert.True(result.Data.CountsByGroup.ContainsKey("Intake"));
        Assert.Equal(1, result.Data.CountsByGroup["Intake"]);
        Assert.Equal(1, result.Data.Total);
    }

    [Fact]
    public async Task GetSalesActionQueueAsync_WhenForbiddenRole_ReturnsForbidden()
    {
        var service = new DashboardQueueService(
            new FakeDashboardQueueReadRepository(),
            new FakeProjectRepository { RoleName = "CUSTOMER" });

        var result = await service.GetSalesActionQueueAsync(
            Guid.NewGuid(),
            new DashboardQueueQueryDto());

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task GetSalesKpisAsync_ReturnsAggregates()
    {
        var dashboard = new FakeDashboardQueueReadRepository
        {
            SalesKpis = new SalesDashboardKpisReadModel
            {
                NewRequests = 2,
                WaitingCustomer = 3,
                PaymentFollowUp = 1,
                OverdueTasks = 4,
                ActiveProjects = 8
            }
        };
        var service = new DashboardQueueService(dashboard, new FakeProjectRepository { RoleName = "SALES" });

        var result = await service.GetSalesKpisAsync(Guid.NewGuid(), new DashboardQueueQueryDto());

        Assert.Equal(200, result.Status);
        Assert.Equal(2, result.Data!.NewRequests);
        Assert.Equal(3, result.Data.WaitingCustomer);
        Assert.Equal(1, result.Data.PaymentFollowUp);
    }

    [Fact]
    public async Task GetDesignerWorkQueueAsync_FiltersByGroup()
    {
        var dashboard = new FakeDashboardQueueReadRepository
        {
            DesignerRows =
            [
                new DashboardProjectQueueRowReadModel
                {
                    ProjectId = Guid.NewGuid(),
                    ProjectName = "Measure me",
                    Status = ProjectStatus.MEASUREMENT_REQUIRED,
                    CustomerName = "C1",
                    AssignedDesignerName = "D1",
                    UpdatedAt = DateTime.UtcNow
                },
                new DashboardProjectQueueRowReadModel
                {
                    ProjectId = Guid.NewGuid(),
                    ProjectName = "Proposal me",
                    Status = ProjectStatus.PROPOSAL_CONSULTING,
                    CustomerName = "C2",
                    AssignedDesignerName = "D1",
                    UpdatedAt = DateTime.UtcNow
                }
            ]
        };
        var service = new DashboardQueueService(dashboard, new FakeProjectRepository { RoleName = "DESIGNER" });

        var result = await service.GetDesignerWorkQueueAsync(
            Guid.NewGuid(),
            new DashboardQueueQueryDto { Group = "Design", Page = 1, Limit = 20 });

        Assert.Equal(200, result.Status);
        Assert.Single(result.Data!.Items);
        Assert.Equal("Complete measurement", result.Data.Items[0].Action);
        Assert.Equal(1, result.Data.Total);
    }

    [Fact]
    public async Task GetProductionQueueAsync_MapsProductionItems()
    {
        var productionRequestId = Guid.NewGuid();
        var dashboard = new FakeDashboardQueueReadRepository
        {
            ProductionRows =
            [
                new DashboardProductionQueueRowReadModel
                {
                    ProductionRequestId = productionRequestId,
                    ProductionCode = "PR-1",
                    ProjectId = Guid.NewGuid(),
                    ProjectName = "Factory job",
                    CustomerName = "Buyer",
                    AssignedToName = "Prod Staff",
                    Status = ProductionRequestStatus.PENDING,
                    CreatedAt = DateTime.UtcNow
                }
            ]
        };
        var service = new DashboardQueueService(dashboard, new FakeProjectRepository { RoleName = "PRODUCTION" });

        var result = await service.GetProductionQueueAsync(
            Guid.NewGuid(),
            new DashboardQueueQueryDto { Scope = "team" });

        Assert.Equal(200, result.Status);
        Assert.Equal(productionRequestId.ToString("D"), result.Data!.Items[0].Id);
        Assert.Equal("Start production", result.Data.Items[0].Action);
    }

    [Fact]
    public async Task GetSalesActionQueueAsync_EmptyUser_ReturnsUnauthorized()
    {
        var service = new DashboardQueueService(
            new FakeDashboardQueueReadRepository(),
            new FakeProjectRepository { RoleName = "SALES" });

        var result = await service.GetSalesActionQueueAsync(Guid.Empty, new DashboardQueueQueryDto());

        Assert.Equal(401, result.Status);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task GetSalesActionQueueAsync_InvalidPaging_ReturnsBadRequest(int page, int limit)
    {
        var service = new DashboardQueueService(
            new FakeDashboardQueueReadRepository(),
            new FakeProjectRepository { RoleName = "SALES" });

        var result = await service.GetSalesActionQueueAsync(
            Guid.NewGuid(),
            new DashboardQueueQueryDto { Page = page, Limit = limit });

        Assert.Equal(400, result.Status);
    }

    [Theory]
    [InlineData("scope", "invalid")]
    [InlineData("dateRange", "yesterday")]
    [InlineData("priority", "CRITICAL")]
    public async Task GetSalesActionQueueAsync_InvalidFilters_ReturnsBadRequest(string field, string value)
    {
        var query = new DashboardQueueQueryDto();
        if (field == "scope")
        {
            query.Scope = value;
        }
        else if (field == "dateRange")
        {
            query.DateRange = value;
        }
        else
        {
            query.Priority = value;
        }

        var service = new DashboardQueueService(
            new FakeDashboardQueueReadRepository(),
            new FakeProjectRepository { RoleName = "SALES" });

        var result = await service.GetSalesActionQueueAsync(Guid.NewGuid(), query);

        Assert.Equal(400, result.Status);
    }

    [Fact]
    public async Task GetSalesActionQueueAsync_AdminRole_IsAllowed()
    {
        var service = new DashboardQueueService(
            new FakeDashboardQueueReadRepository
            {
                SalesRows =
                [
                    new DashboardProjectQueueRowReadModel
                    {
                        ProjectId = Guid.NewGuid(),
                        ProjectName = "Admin view",
                        Status = ProjectStatus.SUBMITTED,
                        CustomerName = "C",
                        UpdatedAt = DateTime.UtcNow
                    }
                ]
            },
            new FakeProjectRepository { RoleName = "ADMIN" });

        var result = await service.GetSalesActionQueueAsync(
            Guid.NewGuid(),
            new DashboardQueueQueryDto { Scope = "all", Priority = "HIGH" });

        Assert.Equal(200, result.Status);
        Assert.Single(result.Data!.Items);
    }

    [Fact]
    public async Task GetSalesActionQueueAsync_FiltersByPriority()
    {
        var service = new DashboardQueueService(
            new FakeDashboardQueueReadRepository
            {
                SalesRows =
                [
                    new DashboardProjectQueueRowReadModel
                    {
                        ProjectId = Guid.NewGuid(),
                        ProjectName = "High",
                        Status = ProjectStatus.SUBMITTED,
                        CustomerName = "C",
                        UpdatedAt = DateTime.UtcNow
                    },
                    new DashboardProjectQueueRowReadModel
                    {
                        ProjectId = Guid.NewGuid(),
                        ProjectName = "Low",
                        Status = ProjectStatus.MEASUREMENT_REQUIRED,
                        CustomerName = "C",
                        UpdatedAt = DateTime.UtcNow
                    }
                ]
            },
            new FakeProjectRepository { RoleName = "SALES" });

        var result = await service.GetSalesActionQueueAsync(
            Guid.NewGuid(),
            new DashboardQueueQueryDto { Priority = "LOW" });

        Assert.Equal(200, result.Status);
        Assert.Single(result.Data!.Items);
        Assert.Equal("Follow design progress", result.Data.Items[0].Action);
    }

    [Fact]
    public async Task GetSalesKpisAsync_WhenForbidden_MapsError()
    {
        var service = new DashboardQueueService(
            new FakeDashboardQueueReadRepository(),
            new FakeProjectRepository { RoleName = "DESIGNER" });

        var result = await service.GetSalesKpisAsync(Guid.NewGuid(), new DashboardQueueQueryDto());

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task GetDesignerKpisAsync_ReturnsAggregates()
    {
        var service = new DashboardQueueService(
            new FakeDashboardQueueReadRepository
            {
                DesignerKpis = new DesignerDashboardKpisReadModel
                {
                    MeasurementDue = 1,
                    ProposalsInProgress = 2,
                    RevisionRequested = 3,
                    OverdueTasks = 4
                }
            },
            new FakeProjectRepository { RoleName = "DESIGNER" });

        var result = await service.GetDesignerKpisAsync(Guid.NewGuid(), new DashboardQueueQueryDto());

        Assert.Equal(200, result.Status);
        Assert.Equal(1, result.Data!.MeasurementDue);
        Assert.Equal(2, result.Data.ProposalsInProgress);
        Assert.Equal(3, result.Data.RevisionRequested);
        Assert.Equal(4, result.Data.OverdueTasks);
    }

    [Fact]
    public async Task GetProductionKpisAsync_ReturnsAggregates()
    {
        var service = new DashboardQueueService(
            new FakeDashboardQueueReadRepository
            {
                ProductionKpis = new ProductionDashboardKpisReadModel
                {
                    PendingCustomizationReview = 5,
                    PendingStart = 1,
                    PendingReview = 1,
                    InProduction = 2,
                    ReadyToComplete = 3,
                    OverdueTasks = 4,
                    ReadyForDelivery = 6,
                    AwaitingDeliverySchedule = 7,
                    CompletedInRange = 8
                }
            },
            new FakeProjectRepository { RoleName = "PRODUCTION" });

        var result = await service.GetProductionKpisAsync(Guid.NewGuid(), new DashboardQueueQueryDto());

        Assert.Equal(200, result.Status);
        Assert.Equal(5, result.Data!.PendingCustomizationReview);
        Assert.Equal(1, result.Data.PendingStart);
        Assert.Equal(1, result.Data.PendingReview);
        Assert.Equal(2, result.Data.InProduction);
        Assert.Equal(3, result.Data.ReadyToComplete);
        Assert.Equal(4, result.Data.OverdueTasks);
        Assert.Equal(6, result.Data.ReadyForDelivery);
        Assert.Equal(7, result.Data.AwaitingDeliverySchedule);
        Assert.Equal(8, result.Data.CompletedInRange);
    }

    [Fact]
    public async Task GetProductionQueueAsync_UsesStoredPriorityAndOverdueBoost()
    {
        var service = new DashboardQueueService(
            new FakeDashboardQueueReadRepository
            {
                ProductionRows =
                [
                    new DashboardProductionQueueRowReadModel
                    {
                        ProductionRequestId = Guid.NewGuid(),
                        ProjectId = Guid.NewGuid(),
                        ProjectName = "Overdue job",
                        CustomerName = "Buyer",
                        Status = ProductionRequestStatus.PENDING,
                        Priority = "medium",
                        ProductionDeadline = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)),
                        CreatedAt = DateTime.UtcNow
                    }
                ]
            },
            new FakeProjectRepository { RoleName = "PRODUCTION" });

        var result = await service.GetProductionQueueAsync(Guid.NewGuid(), new DashboardQueueQueryDto());

        Assert.Equal(200, result.Status);
        Assert.Equal("HIGH", result.Data!.Items[0].Priority);
        Assert.Equal("OVERDUE", result.Data.Items[0].DueBucket);
        Assert.Equal("PRODUCTION_REQUEST", result.Data.Items[0].WorkType);
        Assert.NotNull(result.Data.Items[0].Links);
    }

    [Fact]
    public async Task GetProductionQueueAsync_InvalidStoredPriority_FallsBackToResolved()
    {
        var service = new DashboardQueueService(
            new FakeDashboardQueueReadRepository
            {
                ProductionRows =
                [
                    new DashboardProductionQueueRowReadModel
                    {
                        ProductionRequestId = Guid.NewGuid(),
                        ProjectId = Guid.NewGuid(),
                        ProjectName = "Job",
                        CustomerName = "Buyer",
                        Status = ProductionRequestStatus.PENDING,
                        Priority = "weird",
                        CreatedAt = DateTime.UtcNow
                    }
                ]
            },
            new FakeProjectRepository { RoleName = "PRODUCTION" });

        var result = await service.GetProductionQueueAsync(Guid.NewGuid(), new DashboardQueueQueryDto());

        Assert.Equal("HIGH", result.Data!.Items[0].Priority);
    }

    [Fact]
    public async Task GetDesignerKpisAsync_WhenForbidden_MapsError()
    {
        var service = new DashboardQueueService(
            new FakeDashboardQueueReadRepository(),
            new FakeProjectRepository { RoleName = "SALES" });

        var result = await service.GetDesignerKpisAsync(Guid.NewGuid(), new DashboardQueueQueryDto());

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task GetProductionKpisAsync_WhenForbidden_MapsError()
    {
        var service = new DashboardQueueService(
            new FakeDashboardQueueReadRepository(),
            new FakeProjectRepository { RoleName = "SALES" });

        var result = await service.GetProductionKpisAsync(Guid.NewGuid(), new DashboardQueueQueryDto());

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task GetProjectPhaseDeadlineRisksAsync_MapsStatusesGroupsAndFilters()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var service = new DashboardQueueService(
            new FakeDashboardQueueReadRepository
            {
                DeadlineRiskRows =
                [
                    CreateDeadlineRiskRow(ProjectPhaseType.PROPOSAL, today.AddDays(-3), completedAt: null),
                    CreateDeadlineRiskRow(ProjectPhaseType.PRODUCTION, today.AddDays(-2), DateTime.UtcNow),
                    CreateDeadlineRiskRow(ProjectPhaseType.PRODUCTION, today.AddDays(5), completedAt: null)
                ]
            },
            new FakeProjectRepository { RoleName = "ADMIN" });

        var result = await service.GetProjectPhaseDeadlineRisksAsync(
            Guid.NewGuid(),
            new ProjectPhaseDeadlineRiskQueryDto { Status = "OVERDUE", Page = 1, Limit = 20 });

        Assert.Equal(200, result.Status);
        Assert.Single(result.Data!.Items);
        Assert.Equal("OVERDUE", result.Data.Items[0].Status);
        Assert.Equal("Overdue Proposal", result.Data.Items[0].Group);
        Assert.Equal(3, result.Data.Items[0].Days);
        Assert.Equal(1, result.Data.CountsByGroup["Overdue Proposal"]);
    }

    [Fact]
    public async Task GetProjectPhaseDeadlineRisksAsync_OnTrackDueSoonAndCompletedLate_AreQueryable()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var service = new DashboardQueueService(
            new FakeDashboardQueueReadRepository
            {
                DeadlineRiskRows =
                [
                    CreateDeadlineRiskRow(ProjectPhaseType.PROPOSAL, today.AddDays(4), completedAt: null),
                    CreateDeadlineRiskRow(ProjectPhaseType.PRODUCTION, today.AddDays(-1), DateTime.UtcNow.AddDays(1))
                ]
            },
            new FakeProjectRepository { RoleName = "SALES" });

        var onTrack = await service.GetProjectPhaseDeadlineRisksAsync(
            Guid.NewGuid(),
            new ProjectPhaseDeadlineRiskQueryDto { Status = "ON_TRACK" });
        var completedLate = await service.GetProjectPhaseDeadlineRisksAsync(
            Guid.NewGuid(),
            new ProjectPhaseDeadlineRiskQueryDto { Status = "COMPLETED_LATE" });

        Assert.Single(onTrack.Data!.Items);
        Assert.Equal("Due Soon", onTrack.Data.Items[0].Group);
        Assert.Single(completedLate.Data!.Items);
        Assert.Equal("Completed Late", completedLate.Data.Items[0].Group);
    }

    [Theory]
    [InlineData("INSTALLATION", null, 400)]
    [InlineData("PROPOSAL", "BAD", 400)]
    [InlineData("PROPOSAL", "OVERDUE", 403)]
    public async Task GetProjectPhaseDeadlineRisksAsync_InvalidInputOrRole_ReturnsError(
        string phase,
        string? status,
        int expectedStatus)
    {
        var role = expectedStatus == 403 ? "CUSTOMER" : "ADMIN";
        var service = new DashboardQueueService(
            new FakeDashboardQueueReadRepository(),
            new FakeProjectRepository { RoleName = role });

        var result = await service.GetProjectPhaseDeadlineRisksAsync(
            Guid.NewGuid(),
            new ProjectPhaseDeadlineRiskQueryDto { Phase = phase, Status = status });

        Assert.Equal(expectedStatus, result.Status);
    }

    [Fact]
    public async Task GetProjectPhaseDeadlineRisksAsync_InvalidPagingDateOrUser_ReturnsError()
    {
        var service = new DashboardQueueService(
            new FakeDashboardQueueReadRepository(),
            new FakeProjectRepository { RoleName = "ADMIN" });

        var unauthorized = await service.GetProjectPhaseDeadlineRisksAsync(
            Guid.Empty,
            new ProjectPhaseDeadlineRiskQueryDto());
        var invalidPaging = await service.GetProjectPhaseDeadlineRisksAsync(
            Guid.NewGuid(),
            new ProjectPhaseDeadlineRiskQueryDto { Limit = 101 });
        var invalidRange = await service.GetProjectPhaseDeadlineRisksAsync(
            Guid.NewGuid(),
            new ProjectPhaseDeadlineRiskQueryDto
            {
                From = new DateOnly(2026, 9, 10),
                To = new DateOnly(2026, 9, 1)
            });

        Assert.Equal(401, unauthorized.Status);
        Assert.Equal(400, invalidPaging.Status);
        Assert.Equal(400, invalidRange.Status);
    }

    [Fact]
    public async Task GetProductionQueueAsync_MapsCustomizationDeliveryAndReadyToComplete()
    {
        var projectId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var productionRequestId = Guid.NewGuid();
        var service = new DashboardQueueService(
            new FakeDashboardQueueReadRepository
            {
                ProductionRows =
                [
                    new DashboardProductionQueueRowReadModel
                    {
                        ProductionRequestId = productionRequestId,
                        ProjectId = projectId,
                        OrderId = orderId,
                        ProjectName = "Ready job",
                        CustomerName = "Buyer",
                        Status = ProductionRequestStatus.IN_PRODUCTION,
                        Priority = "URGENT",
                        AllItemsTerminal = true,
                        CreatedAt = DateTime.UtcNow.AddHours(-2)
                    }
                ],
                CustomizationRows =
                [
                    new DashboardProductionCustomizationQueueRowReadModel
                    {
                        VersionId = versionId,
                        CustomizationRequestId = Guid.NewGuid(),
                        ProjectId = projectId,
                        ProjectCode = "PRJ-C",
                        ProjectName = "Custom job",
                        CustomerName = "Buyer",
                        MaterialAvailable = false,
                        SubmittedForReviewAt = DateTime.UtcNow.AddHours(-1),
                        UpdatedAt = DateTime.UtcNow.AddHours(-1)
                    }
                ],
                DeliveryRows =
                [
                    new DashboardProductionDeliveryQueueRowReadModel
                    {
                        OrderId = orderId,
                        ProjectId = projectId,
                        ProjectName = "Delivery job",
                        CustomerName = "Buyer",
                        ProductionRequestId = productionRequestId,
                        AssignedToName = "Prod",
                        OrderStatus = OrderStatus.READY_FOR_DELIVERY,
                        DeliveryQueueStatus = "AWAITING_SCHEDULE",
                        UpdatedAt = DateTime.UtcNow
                    },
                    new DashboardProductionDeliveryQueueRowReadModel
                    {
                        OrderId = Guid.NewGuid(),
                        ProjectId = projectId,
                        ProjectName = "Scheduled",
                        CustomerName = "Buyer",
                        OrderStatus = OrderStatus.READY_FOR_DELIVERY,
                        DeliveryQueueStatus = "SCHEDULED",
                        ScheduledEnd = DateTime.UtcNow.AddDays(3),
                        UpdatedAt = DateTime.UtcNow
                    },
                    new DashboardProductionDeliveryQueueRowReadModel
                    {
                        OrderId = Guid.NewGuid(),
                        ProjectId = projectId,
                        ProjectName = "In progress",
                        CustomerName = "Buyer",
                        OrderStatus = OrderStatus.DELIVERING,
                        DeliveryQueueStatus = "IN_PROGRESS",
                        ScheduledEnd = DateTime.UtcNow.AddDays(-1),
                        UpdatedAt = DateTime.UtcNow
                    },
                    new DashboardProductionDeliveryQueueRowReadModel
                    {
                        OrderId = Guid.NewGuid(),
                        ProjectId = projectId,
                        ProjectName = "Await confirm",
                        CustomerName = "Buyer",
                        OrderStatus = OrderStatus.AWAITING_CUSTOMER_CONFIRMATION,
                        DeliveryQueueStatus = "AWAITING_CUSTOMER_CONFIRMATION",
                        UpdatedAt = DateTime.UtcNow
                    }
                ]
            },
            new FakeProjectRepository { RoleName = "PRODUCTION" });

        var result = await service.GetProductionQueueAsync(
            Guid.NewGuid(),
            new DashboardQueueQueryDto { Scope = "all", Page = 1, Limit = 20 });

        Assert.Equal(200, result.Status);
        Assert.Equal(6, result.Data!.Total);
        Assert.Contains(result.Data.Items, item => item.Status == "READY_TO_COMPLETE");
        Assert.Contains(result.Data.Items, item => item.WorkType == "CUSTOMIZATION_REVIEW");
        Assert.Contains(result.Data.Items, item => item.Status == "AWAITING_SCHEDULE");
        Assert.Contains(result.Data.Items, item => item.Status == "SCHEDULED");
        Assert.Contains(result.Data.Items, item => item.Status == "IN_PROGRESS" && item.DueBucket == "OVERDUE");
        Assert.Contains(result.Data.Items, item => item.Status == "AWAITING_CUSTOMER_CONFIRMATION");
        Assert.True(result.Data.CountsByWorkType["DELIVERY"] >= 4);
        Assert.True(result.Data.CountsByStatus.ContainsKey("REVIEWING"));
    }

    [Theory]
    [InlineData("BAD_TYPE", null, 400)]
    [InlineData(null, "BAD_BUCKET", 400)]
    [InlineData("DELIVERY", "OVERDUE", 200)]
    public async Task GetProductionQueueAsync_WorkTypeDueBucketValidation(
        string? workType,
        string? dueBucket,
        int expectedStatus)
    {
        var service = new DashboardQueueService(
            new FakeDashboardQueueReadRepository
            {
                DeliveryRows =
                [
                    new DashboardProductionDeliveryQueueRowReadModel
                    {
                        OrderId = Guid.NewGuid(),
                        ProjectId = Guid.NewGuid(),
                        ProjectName = "D",
                        CustomerName = "C",
                        OrderStatus = OrderStatus.READY_FOR_DELIVERY,
                        DeliveryQueueStatus = "AWAITING_SCHEDULE",
                        ScheduledEnd = DateTime.UtcNow.AddDays(-2),
                        UpdatedAt = DateTime.UtcNow
                    }
                ]
            },
            new FakeProjectRepository { RoleName = "PRODUCTION" });

        var result = await service.GetProductionQueueAsync(
            Guid.NewGuid(),
            new DashboardQueueQueryDto
            {
                Scope = "all",
                WorkType = workType,
                DueBucket = dueBucket,
                Status = workType == "DELIVERY" ? "AWAITING_SCHEDULE" : null
            });

        Assert.Equal(expectedStatus, result.Status);
        if (expectedStatus == 200)
        {
            Assert.Single(result.Data!.Items);
            Assert.Equal("DELIVERY", result.Data.Items[0].WorkType);
        }
    }

    [Fact]
    public async Task GetProductionQueueAsync_ScopeMine_SkipsCustomizationRowsFromRepo()
    {
        var service = new DashboardQueueService(
            new FakeDashboardQueueReadRepository
            {
                CustomizationRows =
                [
                    new DashboardProductionCustomizationQueueRowReadModel
                    {
                        VersionId = Guid.NewGuid(),
                        CustomizationRequestId = Guid.NewGuid(),
                        ProjectId = Guid.NewGuid(),
                        ProjectName = "Should not appear when filtered by workType only production",
                        CustomerName = "C",
                        UpdatedAt = DateTime.UtcNow
                    }
                ]
            },
            new FakeProjectRepository { RoleName = "PRODUCTION" });

        var result = await service.GetProductionQueueAsync(
            Guid.NewGuid(),
            new DashboardQueueQueryDto { WorkType = "CUSTOMIZATION_REVIEW", Scope = "mine" });

        Assert.Equal(200, result.Status);
        Assert.Empty(result.Data!.Items);
    }

    [Fact]
    public async Task GetProjectPhaseDeadlineRisksAsync_RejectsDeliveryPhase()
    {
        var service = new DashboardQueueService(
            new FakeDashboardQueueReadRepository(),
            new FakeProjectRepository { RoleName = "PRODUCTION" });

        var result = await service.GetProjectPhaseDeadlineRisksAsync(
            Guid.NewGuid(),
            new ProjectPhaseDeadlineRiskQueryDto
            {
                Phase = "DELIVERY",
                ProductionId = Guid.NewGuid()
            });

        Assert.Equal(400, result.Status);
        Assert.Contains("ProjectSchedule", result.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static ProjectPhaseDeadlineRiskRowReadModel CreateDeadlineRiskRow(
        ProjectPhaseType phase,
        DateOnly dueDate,
        DateTime? completedAt)
    {
        return new ProjectPhaseDeadlineRiskRowReadModel
        {
            ProjectId = Guid.NewGuid(),
            ProjectCode = "PRJ-RISK",
            ProjectName = "Deadline Risk",
            Phase = phase,
            DueDate = dueDate,
            CompletedAt = completedAt,
            ProjectStatus = ProjectStatus.IN_CONSULTATION,
            AssignedSalesId = Guid.NewGuid(),
            AssignedSalesName = "Sales",
            AssignedDesignerId = Guid.NewGuid(),
            AssignedDesignerName = "Designer",
            AssignedProductionId = Guid.NewGuid(),
            AssignedProductionName = "Production"
        };
    }

    private sealed class FakeDashboardQueueReadRepository : IDashboardQueueReadRepository
    {
        public IReadOnlyList<DashboardProjectQueueRowReadModel> SalesRows { get; init; } = [];
        public IReadOnlyList<DashboardProjectQueueRowReadModel> DesignerRows { get; init; } = [];
        public IReadOnlyList<DashboardProductionQueueRowReadModel> ProductionRows { get; init; } = [];
        public SalesDashboardKpisReadModel SalesKpis { get; init; } = new();
        public DesignerDashboardKpisReadModel DesignerKpis { get; init; } = new();
        public ProductionDashboardKpisReadModel ProductionKpis { get; init; } = new();
        public IReadOnlyList<ProjectPhaseDeadlineRiskRowReadModel> DeadlineRiskRows { get; init; } = [];

        public Task<IReadOnlyList<DashboardProjectQueueRowReadModel>> GetSalesQueueRowsAsync(
            DashboardQueueFilterReadModel filter,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(SalesRows);

        public Task<SalesDashboardKpisReadModel> GetSalesKpisAsync(
            DashboardQueueFilterReadModel filter,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(SalesKpis);

        public Task<IReadOnlyList<DashboardProjectQueueRowReadModel>> GetDesignerQueueRowsAsync(
            DashboardQueueFilterReadModel filter,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(DesignerRows);

        public Task<DesignerDashboardKpisReadModel> GetDesignerKpisAsync(
            DashboardQueueFilterReadModel filter,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(DesignerKpis);

        public Task<IReadOnlyList<DashboardProductionQueueRowReadModel>> GetProductionQueueRowsAsync(
            DashboardQueueFilterReadModel filter,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ProductionRows);

        public IReadOnlyList<DashboardProductionCustomizationQueueRowReadModel> CustomizationRows { get; init; } = [];

        public IReadOnlyList<DashboardProductionDeliveryQueueRowReadModel> DeliveryRows { get; init; } = [];

        public Task<IReadOnlyList<DashboardProductionCustomizationQueueRowReadModel>> GetProductionCustomizationQueueRowsAsync(
            DashboardQueueFilterReadModel filter,
            CancellationToken cancellationToken = default)
        {
            if (!string.Equals(filter.Scope, "all", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult<IReadOnlyList<DashboardProductionCustomizationQueueRowReadModel>>([]);
            }

            return Task.FromResult(CustomizationRows);
        }

        public Task<IReadOnlyList<DashboardProductionDeliveryQueueRowReadModel>> GetProductionDeliveryQueueRowsAsync(
            DashboardQueueFilterReadModel filter,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(DeliveryRows);

        public Task<ProductionDashboardKpisReadModel> GetProductionKpisAsync(
            DashboardQueueFilterReadModel filter,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ProductionKpis);

        public Task<List<ProjectPhaseDeadlineRiskRowReadModel>> GetProjectPhaseDeadlineRiskRowsAsync(
            ProjectPhaseDeadlineRiskQueryReadModel query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(DeadlineRiskRows.ToList());
    }

    private sealed class FakeProjectRepository : IProjectRepository
    {
        public string? RoleName { get; init; }

        public Task<string?> GetAccountRoleNameAsync(Guid accountId, CancellationToken cancellationToken = default) =>
            Task.FromResult(RoleName);

        public Task<string?> GetAccountFullNameAsync(Guid accountId, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<IReadOnlyList<Guid>> GetActiveAccountIdsByRoleNamesAsync(
            IReadOnlyCollection<string> roleNames,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);

        public Task<int> CountSubmittedInYearAsync(int year, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task<ProjectDetailReadModel?> GetDetailAsync(Guid projectId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ProjectDetailReadModel?>(null);

        public Task<DesignerAccountReadModel?> GetActiveDesignerAsync(Guid designerId, CancellationToken cancellationToken = default) =>
            Task.FromResult<DesignerAccountReadModel?>(null);

        public Task<IReadOnlyList<ProjectListItemReadModel>> GetListAsync(
            ProjectListQueryReadModel query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ProjectListItemReadModel>>([]);

        public Task<int> CountAsync(ProjectListQueryReadModel query, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task<IReadOnlyList<ProjectByUserItemReadModel>> GetByUserAsync(
            ProjectByUserQueryReadModel query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ProjectByUserItemReadModel>>([]);

        public Task<int> CountByUserAsync(ProjectByUserQueryReadModel query, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task<ProjectSearchIndexItemReadModel?> GetSearchIndexItemAsync(
            Guid projectId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ProjectSearchIndexItemReadModel?>(null);

        public Task<IReadOnlyList<ProjectSearchIndexItemReadModel>> GetSearchIndexPageAsync(
            int page,
            int limit,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ProjectSearchIndexItemReadModel>>([]);

        public IQueryable<Project> Query() => Array.Empty<Project>().AsQueryable();

        public Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Project?>(null);

        public Task<IReadOnlyList<Project>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Project>>([]);

        public Task AddAsync(Project entity, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task AddRangeAsync(IEnumerable<Project> entities, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public void Update(Project entity)
        {
        }

        public void Remove(Project entity)
        {
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
    }
}
