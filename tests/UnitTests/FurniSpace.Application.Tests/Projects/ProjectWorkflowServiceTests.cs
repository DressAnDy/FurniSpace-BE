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
    private static readonly string[] ExpectedStageKeys =
    [
        "INTAKE",
        "DESIGNER_ASSIGNMENT",
        "DESIGN_REVIEW",
        "QUOTATION_ORDER",
        "PRODUCTION",
        "DELIVERY"
    ];

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
        Assert.Equal(ExpectedStageKeys, result.Data.Stages.Select(s => s.Key).ToArray());
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

    [Fact]
    public void Compose_DesignerAssignment_IncludesMeasurementLinkAndMetric()
    {
        var designerId = Guid.NewGuid();
        var scheduleId = Guid.NewGuid();
        var snapshot = CreateSnapshot(Guid.NewGuid(), ProjectStatus.WAITING_FOR_DESIGNER_ASSIGNMENT) with
        {
            AssignedDesignerId = designerId,
            DesignerName = "Designer A",
            DesignerAssignedAt = DateTime.UtcNow.AddDays(-1),
            Schedules =
            [
                new ProjectWorkflowScheduleReadModel
                {
                    ScheduleId = scheduleId,
                    Title = null,
                    ScheduleType = ProjectScheduleType.MEASUREMENT,
                    Status = ProjectScheduleStatus.CONFIRMED,
                    ScheduledStart = DateTime.UtcNow.AddDays(-1),
                    ScheduledEnd = DateTime.UtcNow.AddDays(-1).AddHours(2)
                }
            ]
        };

        var dto = ProjectWorkflowComposer.Compose(snapshot);
        var stage = dto.Stages.Single(s => s.Key == "DESIGNER_ASSIGNMENT");

        Assert.Equal("ACTIVE", stage.State);
        Assert.Equal(1, stage.Metrics.Single(m => m.Key == "measurementSchedules").Value);
        Assert.Contains(stage.Links, l => l.Type == "DESIGNER" && l.Id == designerId);
        Assert.Contains(stage.Links, l => l.Type == "SCHEDULE" && l.Id == scheduleId && l.Label == "Measurement");
        Assert.Equal(snapshot.DesignerAssignedAt, stage.Facts["designerAssignedAt"]);
    }

    [Fact]
    public void Compose_DesignReview_SelectedProposalFactsAndLink()
    {
        var selectedId = Guid.NewGuid();
        var draftId = Guid.NewGuid();
        var snapshot = CreateSnapshot(Guid.NewGuid(), ProjectStatus.PROPOSAL_SELECTED) with
        {
            AssignedDesignerId = Guid.NewGuid(),
            DesignerName = "Designer B",
            DesignerAssignedAt = DateTime.UtcNow.AddDays(-2),
            Proposals =
            [
                new ProjectWorkflowProposalReadModel
                {
                    ProposalId = draftId,
                    ProposalName = "Draft",
                    Status = ProposalStatus.DRAFT,
                    VersionNo = 1,
                    UpdatedAt = DateTime.UtcNow.AddDays(-3)
                },
                new ProjectWorkflowProposalReadModel
                {
                    ProposalId = selectedId,
                    ProposalName = "Final",
                    Status = ProposalStatus.SELECTED,
                    VersionNo = 2,
                    SelectedAt = DateTime.UtcNow.AddHours(-1),
                    UpdatedAt = DateTime.UtcNow
                }
            ]
        };

        var dto = ProjectWorkflowComposer.Compose(snapshot);
        var stage = dto.Stages.Single(s => s.Key == "DESIGN_REVIEW");

        Assert.Equal("ACTIVE", stage.State);
        Assert.Equal("SELECTED", stage.Facts["latestProposalStatus"]);
        Assert.Equal(selectedId, stage.Facts["selectedProposalId"]);
        Assert.Contains(stage.Links, l => l.Type == "PROPOSAL" && l.Id == selectedId);
    }

    [Fact]
    public void Compose_QuotationRevisionRequested_BlocksQuotationOrder()
    {
        var snapshot = CreateSnapshot(Guid.NewGuid(), ProjectStatus.QUOTATION_REVISION_REQUESTED) with
        {
            AssignedDesignerId = Guid.NewGuid(),
            DesignerAssignedAt = DateTime.UtcNow.AddDays(-2),
            Proposals =
            [
                new ProjectWorkflowProposalReadModel
                {
                    ProposalId = Guid.NewGuid(),
                    ProposalName = "Selected",
                    Status = ProposalStatus.SELECTED,
                    UpdatedAt = DateTime.UtcNow.AddDays(-1)
                }
            ],
            Quotations =
            [
                new ProjectWorkflowQuotationReadModel
                {
                    QuotationId = Guid.NewGuid(),
                    QuotationCode = "QUO-REV",
                    Status = QuotationStatus.REVISION_REQUESTED,
                    TotalAmount = 10m,
                    UpdatedAt = DateTime.UtcNow
                }
            ]
        };

        var dto = ProjectWorkflowComposer.Compose(snapshot);
        var stage = dto.Stages.Single(s => s.Key == "QUOTATION_ORDER");

        Assert.Equal("BLOCKED", stage.State);
        Assert.Equal(1, stage.Summary.BlockerCount);
        Assert.Equal("Quotation revision requested", stage.Summary.Title);
    }

    [Fact]
    public void Compose_Production_ActiveMetricsLinksAndOverdue()
    {
        var productionRequestId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var snapshot = CreateSnapshot(Guid.NewGuid(), ProjectStatus.IN_PRODUCTION) with
        {
            AssignedDesignerId = Guid.NewGuid(),
            DesignerAssignedAt = DateTime.UtcNow.AddDays(-5),
            Proposals =
            [
                new ProjectWorkflowProposalReadModel
                {
                    ProposalId = Guid.NewGuid(),
                    ProposalName = "P",
                    Status = ProposalStatus.SELECTED,
                    UpdatedAt = DateTime.UtcNow.AddDays(-4)
                }
            ],
            Quotations =
            [
                new ProjectWorkflowQuotationReadModel
                {
                    QuotationId = Guid.NewGuid(),
                    QuotationCode = "QUO",
                    Status = QuotationStatus.ACCEPTED,
                    UpdatedAt = DateTime.UtcNow.AddDays(-3)
                }
            ],
            Orders =
            [
                new ProjectWorkflowOrderReadModel
                {
                    OrderId = orderId,
                    OrderCode = "ORD-P",
                    Status = OrderStatus.IN_PRODUCTION,
                    RemainingAmount = 1m,
                    CreatedAt = DateTime.UtcNow.AddDays(-2)
                }
            ],
            ProductionRequests =
            [
                new ProjectWorkflowProductionRequestReadModel
                {
                    ProductionRequestId = productionRequestId,
                    ProductionCode = null,
                    Status = ProductionRequestStatus.IN_PRODUCTION,
                    AssignedToName = "Prod Owner",
                    CreatedAt = DateTime.UtcNow.AddDays(-1)
                }
            ],
            ProductionItems =
            [
                new ProjectWorkflowProductionItemReadModel
                {
                    ProductionRequestId = productionRequestId,
                    Status = ProductionItemStatus.IN_PRODUCTION,
                    EstimatedCompletionDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2))
                },
                new ProjectWorkflowProductionItemReadModel
                {
                    ProductionRequestId = productionRequestId,
                    Status = ProductionItemStatus.PENDING,
                    EstimatedCompletionDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3))
                }
            ]
        };

        var dto = ProjectWorkflowComposer.Compose(snapshot);
        var stage = dto.Stages.Single(s => s.Key == "PRODUCTION");

        Assert.Equal("ACTIVE", stage.State);
        Assert.Equal("Prod Owner", stage.Summary.PrimaryOwnerName);
        Assert.Equal(1, stage.Metrics.Single(m => m.Key == "openRequests").Value);
        Assert.Equal(0, stage.Metrics.Single(m => m.Key == "blockedCount").Value);
        Assert.Equal(1, stage.Metrics.Single(m => m.Key == "overdueCount").Value);
        Assert.Equal(2, stage.Facts["openItemCount"]);
        Assert.Contains(stage.Links, l =>
            l.Type == "PRODUCTION_REQUEST" &&
            l.Id == productionRequestId &&
            l.Label == "Production request");
        Assert.Contains(stage.Links, l => l.Type == "ORDER" && l.Id == orderId);
    }

    [Fact]
    public void Compose_Delivery_DeliveredAtOnlyAndEmptyProgressNull()
    {
        var orderId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var scheduleId = Guid.NewGuid();
        var snapshot = CreateSnapshot(Guid.NewGuid(), ProjectStatus.DELIVERING) with
        {
            Orders =
            [
                new ProjectWorkflowOrderReadModel
                {
                    OrderId = orderId,
                    OrderCode = "ORD-D",
                    Status = OrderStatus.DELIVERING,
                    RemainingAmount = 5m,
                    CreatedAt = DateTime.UtcNow
                }
            ],
            OrderItems =
            [
                new ProjectWorkflowOrderItemReadModel
                {
                    OrderId = orderId,
                    Quantity = 2,
                    Status = OrderItemStatus.READY,
                    DeliveredAt = DateTime.UtcNow.AddHours(-2)
                },
                new ProjectWorkflowOrderItemReadModel
                {
                    OrderId = orderId,
                    Quantity = 0,
                    Status = OrderItemStatus.CANCELLED
                }
            ],
            Schedules =
            [
                new ProjectWorkflowScheduleReadModel
                {
                    ScheduleId = scheduleId,
                    Title = null,
                    ScheduleType = ProjectScheduleType.HANDOVER,
                    Status = ProjectScheduleStatus.CONFIRMED,
                    ScheduledStart = DateTime.UtcNow.AddHours(5),
                    ScheduledEnd = DateTime.UtcNow.AddHours(7)
                }
            ],
            Payments =
            [
                new ProjectWorkflowPaymentReadModel
                {
                    PaymentId = paymentId,
                    PaymentCode = "PAY-F",
                    PaymentType = PaymentType.REMAINING_PAYMENT,
                    Status = PaymentStatus.PENDING,
                    CreatedAt = DateTime.UtcNow
                }
            ]
        };

        var dto = ProjectWorkflowComposer.Compose(snapshot);
        var stage = dto.Stages.Single(s => s.Key == "DELIVERY");

        Assert.Equal("ACTIVE", stage.State);
        Assert.Equal(100, stage.Facts["deliveredItemProgressPercent"]);
        Assert.Equal(0, stage.Metrics.Single(m => m.Key == "partialDeliveryItems").Value);
        Assert.Contains(stage.Links, l => l.Type == "SCHEDULE" && l.Id == scheduleId && l.Label == "Delivery");
        Assert.Contains(stage.Links, l => l.Type == "ORDER" && l.Id == orderId);
        Assert.Contains(stage.Links, l => l.Type == "PAYMENT" && l.Id == paymentId);

        var emptyItems = snapshot with { OrderItems = [] };
        var emptyProgress = ProjectWorkflowComposer.Compose(emptyItems)
            .Stages.Single(s => s.Key == "DELIVERY");
        Assert.Null(emptyProgress.Facts["deliveredItemProgressPercent"]);
    }

    [Fact]
    public void Compose_Rejected_UsesDeliveryEvidenceForCompletedDeliveryStage()
    {
        var snapshot = CreateSnapshot(Guid.NewGuid(), ProjectStatus.REJECTED) with
        {
            SalesAssignedAt = DateTime.UtcNow.AddDays(-5),
            DesignerAssignedAt = DateTime.UtcNow.AddDays(-4),
            AssignedDesignerId = Guid.NewGuid(),
            Proposals =
            [
                new ProjectWorkflowProposalReadModel
                {
                    ProposalId = Guid.NewGuid(),
                    ProposalName = "P",
                    Status = ProposalStatus.DRAFT
                }
            ],
            Quotations =
            [
                new ProjectWorkflowQuotationReadModel
                {
                    QuotationId = Guid.NewGuid(),
                    QuotationCode = "Q",
                    Status = QuotationStatus.SENT,
                    UpdatedAt = DateTime.UtcNow
                }
            ],
            ProductionRequests =
            [
                new ProjectWorkflowProductionRequestReadModel
                {
                    ProductionRequestId = Guid.NewGuid(),
                    Status = ProductionRequestStatus.CANCELLED,
                    CreatedAt = DateTime.UtcNow
                }
            ],
            OrderItems =
            [
                new ProjectWorkflowOrderItemReadModel
                {
                    OrderId = Guid.NewGuid(),
                    Quantity = 1,
                    Status = OrderItemStatus.READY,
                    DeliveredAt = DateTime.UtcNow.AddDays(-1)
                }
            ]
        };

        var dto = ProjectWorkflowComposer.Compose(snapshot);

        Assert.True(dto.IsRejected);
        Assert.Null(dto.CurrentStage);
        Assert.Equal("COMPLETED", dto.Stages[3].State);
        Assert.Equal("COMPLETED", dto.Stages[4].State);
        Assert.Equal("COMPLETED", dto.Stages[5].State);
        Assert.Equal(100, dto.Stages[5].Facts["deliveredItemProgressPercent"]);
    }

    [Fact]
    public void Compose_UnknownStatus_AllStagesNotStarted()
    {
        var snapshot = CreateSnapshot(Guid.NewGuid(), ProjectStatus.SUBMITTED) with
        {
            Status = null
        };

        var dto = ProjectWorkflowComposer.Compose(snapshot);

        Assert.Null(dto.CurrentStage);
        Assert.All(dto.Stages, s => Assert.Equal("NOT_STARTED", s.State));
        Assert.Null(dto.Stages[0].Facts["submittedAt"]);
        Assert.Null(dto.Stages[5].Facts["remainingAmount"]);
    }

    [Fact]
    public void Compose_IntakeActive_IncludesSalesLink()
    {
        var salesId = Guid.NewGuid();
        var snapshot = CreateSnapshot(Guid.NewGuid(), ProjectStatus.IN_CONSULTATION) with
        {
            AssignedSalesId = salesId,
            SalesName = "Sales Link"
        };

        var dto = ProjectWorkflowComposer.Compose(snapshot);
        var stage = dto.Stages.Single(s => s.Key == "INTAKE");

        Assert.Equal("ACTIVE", stage.State);
        Assert.Contains(stage.Links, l => l.Type == "SALES" && l.Id == salesId && l.Label == "Sales Link");
        Assert.Equal("Cafe", stage.Facts["businessType"]);
    }

    [Fact]
    public void Compose_ReadyForDelivery_MarksProductionActiveWithCompletedDisplayStatus()
    {
        var snapshot = CreateSnapshot(Guid.NewGuid(), ProjectStatus.READY_FOR_DELIVERY) with
        {
            ProductionRequests =
            [
                new ProjectWorkflowProductionRequestReadModel
                {
                    ProductionRequestId = Guid.NewGuid(),
                    ProductionCode = "PR-DONE",
                    Status = ProductionRequestStatus.COMPLETED,
                    CreatedAt = DateTime.UtcNow
                }
            ]
        };

        var dto = ProjectWorkflowComposer.Compose(snapshot);
        var stage = dto.Stages.Single(s => s.Key == "PRODUCTION");

        Assert.Equal("PRODUCTION", dto.CurrentStage);
        Assert.Equal("ACTIVE", stage.State);
        Assert.Equal("READY_FOR_DELIVERY", stage.StatusInStage);
        Assert.Equal("Production in progress", stage.Summary.Title);
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
