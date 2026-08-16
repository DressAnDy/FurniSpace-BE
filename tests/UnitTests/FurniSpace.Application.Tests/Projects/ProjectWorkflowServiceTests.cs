#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.Common.Projects;
using FurniSpace.Application.Services.Projects;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.ReadModels.Projects;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Xunit;

namespace FurniSpace.Application.Tests.Projects;

public sealed class ProjectWorkflowServiceTests
{
    [Fact]
    public async Task GetWorkflowAsync_EmptyProjectId_ReturnsBadRequest()
    {
        var service = new ProjectWorkflowService(new FakeWorkflowRepository(null));

        var result = await service.GetWorkflowAsync(Guid.Empty);

        Assert.Equal(400, result.Status);
        Assert.Equal("Project id is required.", result.Message);
    }

    [Fact]
    public async Task GetWorkflowAsync_MissingProject_ReturnsNotFound()
    {
        var service = new ProjectWorkflowService(new FakeWorkflowRepository(null));

        var result = await service.GetWorkflowAsync(Guid.NewGuid());

        Assert.Equal(404, result.Status);
        Assert.Equal("Project not found.", result.Message);
    }

    [Fact]
    public async Task GetWorkflowAsync_ReturnsSixStagesInOrder()
    {
        var projectId = Guid.NewGuid();
        var snapshot = CreateSnapshot(projectId, ProjectStatus.PROPOSAL_CONSULTING);
        var service = new ProjectWorkflowService(new FakeWorkflowRepository(snapshot));

        var result = await service.GetWorkflowAsync(projectId);

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(
            new[]
            {
                "INTAKE",
                "DESIGNER_ASSIGNMENT",
                "DESIGN_REVIEW",
                "QUOTATION_ORDER",
                "PRODUCTION",
                "DELIVERY"
            },
            result.Data.Stages.Select(s => s.Key).ToArray());
        Assert.Equal("DESIGN_REVIEW", result.Data.CurrentStage);
        Assert.Equal("ACTIVE", result.Data.Stages[2].State);
        Assert.Equal("COMPLETED", result.Data.Stages[0].State);
        Assert.Equal("NOT_STARTED", result.Data.Stages[5].State);
        Assert.Null(result.Data.Stages[5].StatusInStage);
    }

    [Fact]
    public void Compose_NeedBasicInformation_BlocksIntake()
    {
        var snapshot = CreateSnapshot(Guid.NewGuid(), ProjectStatus.NEED_BASIC_INFORMATION);
        var dto = ProjectWorkflowComposer.Compose(snapshot);

        Assert.Equal("INTAKE", dto.CurrentStage);
        Assert.Equal("BLOCKED", dto.Stages[0].State);
        Assert.Equal(1, dto.Stages[0].Summary.BlockerCount);
        Assert.Equal("NEED_BASIC_INFORMATION", dto.Stages[0].StatusInStage);
    }

    [Fact]
    public void Compose_MeasurementRequired_BlocksDesignerAssignment()
    {
        var snapshot = CreateSnapshot(Guid.NewGuid(), ProjectStatus.MEASUREMENT_REQUIRED);
        var dto = ProjectWorkflowComposer.Compose(snapshot);

        Assert.Equal("DESIGNER_ASSIGNMENT", dto.CurrentStage);
        Assert.Equal("BLOCKED", dto.Stages[1].State);
    }

    [Fact]
    public void Compose_RevisionRequestedProposal_BlocksDesignReview()
    {
        var snapshot = CreateSnapshot(Guid.NewGuid(), ProjectStatus.PROPOSAL_CONSULTING);
        snapshot = CloneWithProposals(snapshot,
        [
            new ProjectWorkflowProposalReadModel
            {
                ProposalId = Guid.NewGuid(),
                ProposalName = "V1",
                Status = ProposalStatus.REVISION_REQUESTED,
                UpdatedAt = DateTime.UtcNow
            }
        ]);

        var dto = ProjectWorkflowComposer.Compose(snapshot);

        Assert.Equal("BLOCKED", dto.Stages[2].State);
        Assert.Equal(1, dto.Stages[2].Metrics.Single(m => m.Key == "revisionRequestedCount").Value);
    }

    [Fact]
    public void Compose_ProductionBlockedItems_BlocksProduction()
    {
        var snapshot = CreateSnapshot(Guid.NewGuid(), ProjectStatus.IN_PRODUCTION);
        snapshot = CloneWithProduction(
            snapshot,
            [
                new ProjectWorkflowProductionRequestReadModel
                {
                    ProductionRequestId = Guid.NewGuid(),
                    ProductionCode = "PR-1",
                    Status = ProductionRequestStatus.IN_PRODUCTION,
                    CreatedAt = DateTime.UtcNow
                }
            ],
            [
                new ProjectWorkflowProductionItemReadModel
                {
                    ProductionRequestId = Guid.NewGuid(),
                    Status = ProductionItemStatus.CANCELLED
                }
            ]);

        var dto = ProjectWorkflowComposer.Compose(snapshot);

        Assert.Equal("PRODUCTION", dto.CurrentStage);
        Assert.Equal("BLOCKED", dto.Stages[4].State);
        Assert.Equal(1, dto.Stages[4].Facts["blockedItemCount"]);
    }

    [Fact]
    public void Compose_OverdueDeliverySchedule_BlocksDelivery()
    {
        var snapshot = CreateSnapshot(Guid.NewGuid(), ProjectStatus.DELIVERING);
        snapshot = CloneWithSchedules(snapshot,
        [
            new ProjectWorkflowScheduleReadModel
            {
                ScheduleId = Guid.NewGuid(),
                Title = "Delivery #1",
                ScheduleType = ProjectScheduleType.DELIVERY,
                Status = ProjectScheduleStatus.CONFIRMED,
                ScheduledStart = DateTime.UtcNow.AddDays(-2),
                ScheduledEnd = DateTime.UtcNow.AddDays(-1)
            }
        ]);

        var dto = ProjectWorkflowComposer.Compose(snapshot);

        Assert.Equal("DELIVERY", dto.CurrentStage);
        Assert.Equal("BLOCKED", dto.Stages[5].State);
    }

    [Fact]
    public void Compose_Rejected_UsesEvidenceForCompletedStages()
    {
        var projectId = Guid.NewGuid();
        var snapshot = CreateSnapshot(projectId, ProjectStatus.REJECTED) with
        {
            SalesAssignedAt = DateTime.UtcNow.AddDays(-3),
            DesignerAssignedAt = DateTime.UtcNow.AddDays(-2),
            AssignedDesignerId = Guid.NewGuid(),
            Proposals =
            [
                new ProjectWorkflowProposalReadModel
                {
                    ProposalId = Guid.NewGuid(),
                    ProposalName = "Draft",
                    Status = ProposalStatus.DRAFT
                }
            ]
        };

        var dto = ProjectWorkflowComposer.Compose(snapshot);

        Assert.True(dto.IsRejected);
        Assert.Null(dto.CurrentStage);
        Assert.Equal("COMPLETED", dto.Stages[0].State);
        Assert.Equal("COMPLETED", dto.Stages[1].State);
        Assert.Equal("COMPLETED", dto.Stages[2].State);
        Assert.Equal("NOT_STARTED", dto.Stages[3].State);
        Assert.DoesNotContain(dto.Stages, s => s.State is "ACTIVE" or "BLOCKED");
    }

    [Fact]
    public void Compose_ProjectCompleted_MarksDeliveryCompleted()
    {
        var snapshot = CreateSnapshot(Guid.NewGuid(), ProjectStatus.COMPLETED);
        var dto = ProjectWorkflowComposer.Compose(snapshot);

        Assert.Equal("DELIVERY", dto.CurrentStage);
        Assert.Equal("COMPLETED", dto.Stages[5].State);
        Assert.All(dto.Stages.Take(5), s => Assert.Equal("COMPLETED", s.State));
        Assert.DoesNotContain(dto.Stages, s => s.State is "ACTIVE" or "BLOCKED");
    }

    [Fact]
    public void Compose_QuotationOrder_ComputesMoneyAndLinks()
    {
        var orderId = Guid.NewGuid();
        var quotationId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var snapshot = CreateSnapshot(Guid.NewGuid(), ProjectStatus.ORDER_CONFIRMED) with
        {
            Quotations =
            [
                new ProjectWorkflowQuotationReadModel
                {
                    QuotationId = quotationId,
                    QuotationCode = "QUO-1",
                    Status = QuotationStatus.ACCEPTED,
                    TotalAmount = 99_000_000m,
                    SentAt = DateTime.UtcNow.AddDays(-1),
                    UpdatedAt = DateTime.UtcNow
                }
            ],
            Orders =
            [
                new ProjectWorkflowOrderReadModel
                {
                    OrderId = orderId,
                    OrderCode = "ORD-1",
                    Status = OrderStatus.DEPOSIT_PAID,
                    RemainingAmount = 45_000_000m,
                    CreatedAt = DateTime.UtcNow
                }
            ],
            Payments =
            [
                new ProjectWorkflowPaymentReadModel
                {
                    PaymentId = paymentId,
                    PaymentCode = "PAY-DEP",
                    PaymentType = PaymentType.DEPOSIT,
                    Status = PaymentStatus.PAID,
                    CreatedAt = DateTime.UtcNow
                }
            ]
        };

        var dto = ProjectWorkflowComposer.Compose(snapshot);
        var stage = dto.Stages.Single(s => s.Key == "QUOTATION_ORDER");

        Assert.Equal("ACTIVE", stage.State);
        Assert.Equal(99_000_000m, stage.Facts["latestQuotationTotal"]);
        Assert.Equal(45_000_000m, stage.Metrics.Single(m => m.Key == "outstandingAmount").Value);
        Assert.Contains(stage.Links, l => l.Type == "QUOTATION" && l.Id == quotationId);
        Assert.Contains(stage.Links, l => l.Type == "ORDER" && l.Id == orderId);
        Assert.Contains(stage.Links, l => l.Type == "PAYMENT" && l.Id == paymentId);
    }

    [Fact]
    public void Compose_DeliveryProgress_ComputesPercent()
    {
        var snapshot = CreateSnapshot(Guid.NewGuid(), ProjectStatus.DELIVERING) with
        {
            Orders =
            [
                new ProjectWorkflowOrderReadModel
                {
                    OrderId = Guid.NewGuid(),
                    OrderCode = "ORD-1",
                    Status = OrderStatus.DELIVERING,
                    RemainingAmount = 10m,
                    CreatedAt = DateTime.UtcNow
                }
            ],
            OrderItems =
            [
                new ProjectWorkflowOrderItemReadModel
                {
                    OrderId = Guid.NewGuid(),
                    Quantity = 4,
                    Status = OrderItemStatus.DELIVERED,
                    DeliveredAt = DateTime.UtcNow.AddHours(-1)
                },
                new ProjectWorkflowOrderItemReadModel
                {
                    OrderId = Guid.NewGuid(),
                    Quantity = 6,
                    Status = OrderItemStatus.READY
                }
            ],
            Schedules =
            [
                new ProjectWorkflowScheduleReadModel
                {
                    ScheduleId = Guid.NewGuid(),
                    Title = "Delivery",
                    ScheduleType = ProjectScheduleType.DELIVERY,
                    Status = ProjectScheduleStatus.CONFIRMED,
                    ScheduledStart = DateTime.UtcNow.AddHours(2),
                    ScheduledEnd = DateTime.UtcNow.AddHours(4)
                }
            ]
        };

        var dto = ProjectWorkflowComposer.Compose(snapshot);
        var stage = dto.Stages.Single(s => s.Key == "DELIVERY");

        Assert.Equal("ACTIVE", stage.State);
        Assert.Equal(40, stage.Facts["deliveredItemProgressPercent"]);
        Assert.Equal(1, stage.Metrics.Single(m => m.Key == "upcomingSchedules").Value);
    }

    private static ProjectWorkflowSnapshotReadModel CreateSnapshot(Guid projectId, ProjectStatus status) =>
        new()
        {
            ProjectId = projectId,
            ProjectCode = "PRJ-001",
            ProjectName = "Cafe ABC",
            Status = status,
            BusinessType = "Cafe",
            SubmittedAt = DateTime.UtcNow.AddDays(-10),
            SalesAssignedAt = DateTime.UtcNow.AddDays(-9),
            CustomerId = Guid.NewGuid(),
            CustomerName = "Customer A",
            AssignedSalesId = Guid.NewGuid(),
            SalesName = "Sales A"
        };

    private static ProjectWorkflowSnapshotReadModel CloneWithProposals(
        ProjectWorkflowSnapshotReadModel source,
        IReadOnlyList<ProjectWorkflowProposalReadModel> proposals) =>
        source with { Proposals = proposals };

    private static ProjectWorkflowSnapshotReadModel CloneWithProduction(
        ProjectWorkflowSnapshotReadModel source,
        IReadOnlyList<ProjectWorkflowProductionRequestReadModel> requests,
        IReadOnlyList<ProjectWorkflowProductionItemReadModel> items) =>
        source with
        {
            ProductionRequests = requests,
            ProductionItems = items
        };

    private static ProjectWorkflowSnapshotReadModel CloneWithSchedules(
        ProjectWorkflowSnapshotReadModel source,
        IReadOnlyList<ProjectWorkflowScheduleReadModel> schedules) =>
        source with { Schedules = schedules };

    private sealed class FakeWorkflowRepository : IProjectWorkflowRepository
    {
        private readonly ProjectWorkflowSnapshotReadModel? _snapshot;

        public FakeWorkflowRepository(ProjectWorkflowSnapshotReadModel? snapshot)
        {
            _snapshot = snapshot;
        }

        public Task<ProjectWorkflowSnapshotReadModel?> GetSnapshotAsync(
            Guid projectId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_snapshot);
    }
}
