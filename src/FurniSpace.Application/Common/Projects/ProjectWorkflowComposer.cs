#nullable enable

using FurniSpace.Application.DTOs.Projects;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.ReadModels.Projects;

namespace FurniSpace.Application.Common.Projects;

internal static class ProjectWorkflowComposer
{
    public static ProjectWorkflowDto Compose(ProjectWorkflowSnapshotReadModel snapshot)
    {
        var isRejected = snapshot.Status == ProjectStatus.REJECTED;
        var currentStageIndex = ProjectWorkflowStageCatalog.ResolveStageIndex(snapshot.Status);
        var currentStageKey = currentStageIndex.HasValue
            ? ProjectWorkflowStageCatalog.Stages[currentStageIndex.Value].Key
            : null;

        var stages = new List<ProjectWorkflowStageDto>(ProjectWorkflowStageCatalog.Stages.Count);
        for (var i = 0; i < ProjectWorkflowStageCatalog.Stages.Count; i++)
        {
            var definition = ProjectWorkflowStageCatalog.Stages[i];
            stages.Add(BuildStage(snapshot, definition, i, currentStageIndex, isRejected));
        }

        return new ProjectWorkflowDto
        {
            ProjectId = snapshot.ProjectId,
            ProjectCode = snapshot.ProjectCode,
            ProjectName = snapshot.ProjectName,
            CurrentStatus = snapshot.Status?.ToString(),
            CurrentStage = isRejected ? null : currentStageKey,
            IsRejected = isRejected,
            Owners = new ProjectWorkflowOwnersDto
            {
                CustomerId = snapshot.CustomerId,
                CustomerName = snapshot.CustomerName,
                AssignedSalesId = snapshot.AssignedSalesId,
                SalesName = snapshot.SalesName,
                AssignedDesignerId = snapshot.AssignedDesignerId,
                DesignerName = snapshot.DesignerName
            },
            Stages = stages
        };
    }

    private static ProjectWorkflowStageDto BuildStage(
        ProjectWorkflowSnapshotReadModel snapshot,
        ProjectWorkflowStageCatalog.StageDefinition definition,
        int stageIndex,
        int? currentStageIndex,
        bool isRejected)
    {
        var state = ResolveState(snapshot, definition, stageIndex, currentStageIndex, isRejected);
        var isBlocked = state == ProjectWorkflowStageCatalog.StateBlocked;
        var blockerCount = isBlocked ? 1 : 0;

        var statusInStage = state switch
        {
            ProjectWorkflowStageCatalog.StateNotStarted => null,
            ProjectWorkflowStageCatalog.StateActive or ProjectWorkflowStageCatalog.StateBlocked
                => snapshot.Status?.ToString(),
            _ => definition.CompletedDisplayStatus.ToString()
        };

        return new ProjectWorkflowStageDto
        {
            Key = definition.Key,
            Label = definition.Label,
            State = state,
            StatusInStage = statusInStage,
            Summary = BuildSummary(definition.Key, state, blockerCount, snapshot),
            Metrics = BuildMetrics(definition.Key, state, snapshot),
            Links = BuildLinks(definition.Key, state, snapshot),
            Facts = BuildFacts(definition.Key, state, snapshot)
        };
    }

    private static string ResolveState(
        ProjectWorkflowSnapshotReadModel snapshot,
        ProjectWorkflowStageCatalog.StageDefinition definition,
        int stageIndex,
        int? currentStageIndex,
        bool isRejected)
    {
        if (isRejected)
        {
            return WasReachedBeforeRejection(snapshot, definition.Key)
                ? ProjectWorkflowStageCatalog.StateCompleted
                : ProjectWorkflowStageCatalog.StateNotStarted;
        }

        if (!currentStageIndex.HasValue)
        {
            return ProjectWorkflowStageCatalog.StateNotStarted;
        }

        if (stageIndex < currentStageIndex.Value)
        {
            return ProjectWorkflowStageCatalog.StateCompleted;
        }

        if (stageIndex > currentStageIndex.Value)
        {
            return ProjectWorkflowStageCatalog.StateNotStarted;
        }

        if (snapshot.Status == ProjectStatus.COMPLETED &&
            definition.Key == ProjectWorkflowStageCatalog.StageDelivery)
        {
            return ProjectWorkflowStageCatalog.StateCompleted;
        }

        return IsBlocked(definition.Key, snapshot)
            ? ProjectWorkflowStageCatalog.StateBlocked
            : ProjectWorkflowStageCatalog.StateActive;
    }

    private static bool WasReachedBeforeRejection(
        ProjectWorkflowSnapshotReadModel snapshot,
        string stageKey)
    {
        return stageKey switch
        {
            ProjectWorkflowStageCatalog.StageIntake =>
                snapshot.SubmittedAt.HasValue || snapshot.SalesAssignedAt.HasValue,
            ProjectWorkflowStageCatalog.StageDesignerAssignment =>
                snapshot.DesignerAssignedAt.HasValue ||
                snapshot.AssignedDesignerId.HasValue ||
                snapshot.Schedules.Any(s =>
                    s.ScheduleType == ProjectScheduleType.MEASUREMENT &&
                    s.Status != ProjectScheduleStatus.CANCELLED),
            ProjectWorkflowStageCatalog.StageDesignReview =>
                snapshot.Proposals.Count > 0,
            ProjectWorkflowStageCatalog.StageQuotationOrder =>
                snapshot.Quotations.Count > 0 || snapshot.Orders.Count > 0,
            ProjectWorkflowStageCatalog.StageProduction =>
                snapshot.ProductionRequests.Count > 0,
            ProjectWorkflowStageCatalog.StageDelivery =>
                snapshot.Schedules.Any(s =>
                    s.ScheduleType is ProjectScheduleType.DELIVERY or ProjectScheduleType.HANDOVER) ||
                snapshot.OrderItems.Any(IsOrderItemDelivered),
            _ => false
        };
    }

    private static bool IsBlocked(string stageKey, ProjectWorkflowSnapshotReadModel snapshot)
    {
        return stageKey switch
        {
            ProjectWorkflowStageCatalog.StageIntake =>
                snapshot.Status == ProjectStatus.NEED_BASIC_INFORMATION,
            ProjectWorkflowStageCatalog.StageDesignerAssignment =>
                snapshot.Status == ProjectStatus.MEASUREMENT_REQUIRED,
            ProjectWorkflowStageCatalog.StageDesignReview =>
                snapshot.Proposals.Any(p => p.Status == ProposalStatus.REVISION_REQUESTED),
            ProjectWorkflowStageCatalog.StageQuotationOrder =>
                snapshot.Status == ProjectStatus.QUOTATION_REVISION_REQUESTED,
            ProjectWorkflowStageCatalog.StageProduction =>
                snapshot.ProductionItems.Any(i => i.Status == ProductionItemStatus.CANCELLED),
            ProjectWorkflowStageCatalog.StageDelivery =>
                HasOverdueDeliverySchedule(snapshot),
            _ => false
        };
    }

    private static bool HasOverdueDeliverySchedule(ProjectWorkflowSnapshotReadModel snapshot)
    {
        var now = DateTime.UtcNow;
        return snapshot.Schedules.Any(s =>
            s.ScheduleType is ProjectScheduleType.DELIVERY or ProjectScheduleType.HANDOVER &&
            s.Status is not (ProjectScheduleStatus.COMPLETED or ProjectScheduleStatus.CANCELLED) &&
            s.ScheduledEnd.HasValue &&
            s.ScheduledEnd.Value < now);
    }

    private static ProjectWorkflowStageSummaryDto BuildSummary(
        string stageKey,
        string state,
        int blockerCount,
        ProjectWorkflowSnapshotReadModel snapshot)
    {
        var (title, description, owner) = (stageKey, state) switch
        {
            (ProjectWorkflowStageCatalog.StageIntake, ProjectWorkflowStageCatalog.StateBlocked) =>
                ("Waiting for basic information", "Sales requested more project information.", snapshot.SalesName),
            (ProjectWorkflowStageCatalog.StageIntake, ProjectWorkflowStageCatalog.StateActive) =>
                ("Intake in progress", "Project is in consultation.", snapshot.SalesName),
            (ProjectWorkflowStageCatalog.StageIntake, ProjectWorkflowStageCatalog.StateCompleted) =>
                ("Consultation completed", "Project accepted and ready for designer assignment.", snapshot.SalesName),
            (ProjectWorkflowStageCatalog.StageDesignerAssignment, ProjectWorkflowStageCatalog.StateBlocked) =>
                ("Measurement required", "Designer assignment is blocked until measurement is completed.", snapshot.DesignerName ?? snapshot.SalesName),
            (ProjectWorkflowStageCatalog.StageDesignerAssignment, ProjectWorkflowStageCatalog.StateActive) =>
                ("Designer assignment in progress", "Waiting for designer assignment or space verification.", snapshot.SalesName),
            (ProjectWorkflowStageCatalog.StageDesignerAssignment, ProjectWorkflowStageCatalog.StateCompleted) =>
                ("Designer assigned", "Space verified and ready for design review.", snapshot.DesignerName),
            (ProjectWorkflowStageCatalog.StageDesignReview, ProjectWorkflowStageCatalog.StateBlocked) =>
                ("Proposal revision requested", "Customer requested proposal revisions.", snapshot.DesignerName),
            (ProjectWorkflowStageCatalog.StageDesignReview, ProjectWorkflowStageCatalog.StateActive) =>
                ("Proposal consulting", "Customer is reviewing proposals.", snapshot.DesignerName),
            (ProjectWorkflowStageCatalog.StageDesignReview, ProjectWorkflowStageCatalog.StateCompleted) =>
                ("Proposal selected", "Final proposal selected.", snapshot.DesignerName),
            (ProjectWorkflowStageCatalog.StageQuotationOrder, ProjectWorkflowStageCatalog.StateBlocked) =>
                ("Quotation revision requested", "Customer requested quotation revisions.", snapshot.SalesName),
            (ProjectWorkflowStageCatalog.StageQuotationOrder, ProjectWorkflowStageCatalog.StateActive) =>
                ("Quotation in progress", "Quotation or order confirmation is in progress.", snapshot.SalesName),
            (ProjectWorkflowStageCatalog.StageQuotationOrder, ProjectWorkflowStageCatalog.StateCompleted) =>
                ("Order confirmed", "Quotation accepted and order created.", snapshot.SalesName),
            (ProjectWorkflowStageCatalog.StageProduction, ProjectWorkflowStageCatalog.StateBlocked) =>
                ("Production blocked", "One or more production items are blocked.", ResolveProductionOwner(snapshot)),
            (ProjectWorkflowStageCatalog.StageProduction, ProjectWorkflowStageCatalog.StateActive) =>
                ("Production in progress", "Production request is active.", ResolveProductionOwner(snapshot)),
            (ProjectWorkflowStageCatalog.StageProduction, ProjectWorkflowStageCatalog.StateCompleted) =>
                ("Ready for delivery", "Production completed.", ResolveProductionOwner(snapshot)),
            (ProjectWorkflowStageCatalog.StageDelivery, ProjectWorkflowStageCatalog.StateBlocked) =>
                ("Delivery overdue", "A delivery or handover schedule is overdue.", snapshot.SalesName),
            (ProjectWorkflowStageCatalog.StageDelivery, ProjectWorkflowStageCatalog.StateActive) =>
                ("Delivery in progress", "Order is being delivered.", snapshot.SalesName),
            (ProjectWorkflowStageCatalog.StageDelivery, ProjectWorkflowStageCatalog.StateCompleted) =>
                ("Delivery completed", "Project delivery is complete.", snapshot.SalesName),
            _ => ("Not started", $"{stageKey.Replace('_', ' ').ToLowerInvariant()} has not started yet.", null)
        };

        return new ProjectWorkflowStageSummaryDto
        {
            Title = title,
            Description = description,
            BlockerCount = blockerCount,
            PrimaryOwnerName = owner
        };
    }

    private static string? ResolveProductionOwner(ProjectWorkflowSnapshotReadModel snapshot)
    {
        return snapshot.ProductionRequests
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => r.AssignedToName)
            .FirstOrDefault(n => !string.IsNullOrWhiteSpace(n))
            ?? snapshot.SalesName;
    }

    private static IReadOnlyList<ProjectWorkflowMetricDto> BuildMetrics(
        string stageKey,
        string state,
        ProjectWorkflowSnapshotReadModel snapshot)
    {
        if (state == ProjectWorkflowStageCatalog.StateNotStarted)
        {
            return [];
        }

        return stageKey switch
        {
            ProjectWorkflowStageCatalog.StageDesignerAssignment =>
            [
                Metric("measurementSchedules", "Measurement schedules", CountMeasurementSchedules(snapshot), "count")
            ],
            ProjectWorkflowStageCatalog.StageDesignReview =>
            [
                Metric("proposalCount", "Proposals", snapshot.Proposals.Count, "count"),
                Metric(
                    "revisionRequestedCount",
                    "Revision requested",
                    snapshot.Proposals.Count(p => p.Status == ProposalStatus.REVISION_REQUESTED),
                    "count")
            ],
            ProjectWorkflowStageCatalog.StageQuotationOrder =>
            [
                Metric(
                    "quotationsSent",
                    "Quotations sent",
                    snapshot.Quotations.Count(q =>
                        q.SentAt.HasValue ||
                        q.Status is QuotationStatus.SENT or QuotationStatus.REVISED or QuotationStatus.ACCEPTED
                            or QuotationStatus.REVISION_REQUESTED),
                    "count"),
                Metric(
                    "outstandingAmount",
                    "Outstanding amount",
                    ResolvePrimaryOrder(snapshot)?.RemainingAmount,
                    "money")
            ],
            ProjectWorkflowStageCatalog.StageProduction => BuildProductionMetrics(snapshot),
            ProjectWorkflowStageCatalog.StageDelivery => BuildDeliveryMetrics(snapshot),
            _ => []
        };
    }

    private static IReadOnlyList<ProjectWorkflowMetricDto> BuildProductionMetrics(
        ProjectWorkflowSnapshotReadModel snapshot)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var openStatuses = new HashSet<ProductionRequestStatus>
        {
            ProductionRequestStatus.PENDING_REVIEW,
            ProductionRequestStatus.FEASIBLE,
            ProductionRequestStatus.IN_PRODUCTION
        };

        var openRequests = snapshot.ProductionRequests.Count(r =>
            r.Status.HasValue && openStatuses.Contains(r.Status.Value));
        var blockedCount = snapshot.ProductionItems.Count(i => i.Status == ProductionItemStatus.CANCELLED);
        var overdueCount = snapshot.ProductionItems.Count(i =>
            i.Status is not (ProductionItemStatus.COMPLETED or ProductionItemStatus.CANCELLED) &&
            i.EstimatedCompletionDate.HasValue &&
            i.EstimatedCompletionDate.Value < today);

        return
        [
            Metric("openRequests", "Open requests", openRequests, "count"),
            Metric("blockedCount", "Blocked items", blockedCount, "count"),
            Metric("overdueCount", "Overdue items", overdueCount, "count")
        ];
    }

    private static IReadOnlyList<ProjectWorkflowMetricDto> BuildDeliveryMetrics(
        ProjectWorkflowSnapshotReadModel snapshot)
    {
        var now = DateTime.UtcNow;
        var upcoming = snapshot.Schedules.Count(s =>
            s.ScheduleType is ProjectScheduleType.DELIVERY or ProjectScheduleType.HANDOVER &&
            s.Status is not (ProjectScheduleStatus.COMPLETED or ProjectScheduleStatus.CANCELLED) &&
            s.ScheduledStart >= now);
        // Single full-delivery model: no partial quantities; count non-terminal undelivered items.
        var pendingDeliveryItems = snapshot.OrderItems.Count(i =>
            !IsOrderItemDelivered(i) &&
            i.Status is not (OrderItemStatus.CANCELLED or OrderItemStatus.UNAVAILABLE) &&
            (i.Quantity ?? 0) > 0);

        return
        [
            Metric("upcomingSchedules", "Upcoming schedules", upcoming, "count"),
            Metric("partialDeliveryItems", "Pending delivery items", pendingDeliveryItems, "count")
        ];
    }

    private static IReadOnlyList<ProjectWorkflowLinkDto> BuildLinks(
        string stageKey,
        string state,
        ProjectWorkflowSnapshotReadModel snapshot)
    {
        if (state == ProjectWorkflowStageCatalog.StateNotStarted)
        {
            return [];
        }

        var links = new List<ProjectWorkflowLinkDto>();

        switch (stageKey)
        {
            case ProjectWorkflowStageCatalog.StageIntake:
                AddAccountLink(links, "SALES", snapshot.AssignedSalesId, snapshot.SalesName);
                break;
            case ProjectWorkflowStageCatalog.StageDesignerAssignment:
                AddAccountLink(links, "DESIGNER", snapshot.AssignedDesignerId, snapshot.DesignerName);
                var measurement = snapshot.Schedules
                    .Where(s =>
                        s.ScheduleType == ProjectScheduleType.MEASUREMENT &&
                        s.Status != ProjectScheduleStatus.CANCELLED)
                    .OrderByDescending(s => s.ScheduledStart)
                    .FirstOrDefault();
                if (measurement is not null)
                {
                    links.Add(Link("SCHEDULE", measurement.ScheduleId, measurement.Title ?? "Measurement"));
                }

                break;
            case ProjectWorkflowStageCatalog.StageDesignReview:
                var proposal = ResolveLatestProposal(snapshot);
                if (proposal is not null)
                {
                    links.Add(Link("PROPOSAL", proposal.ProposalId, proposal.ProposalName));
                }

                break;
            case ProjectWorkflowStageCatalog.StageQuotationOrder:
                var quotation = ResolveLatestQuotation(snapshot);
                if (quotation is not null)
                {
                    links.Add(Link("QUOTATION", quotation.QuotationId, quotation.QuotationCode));
                }

                var order = ResolvePrimaryOrder(snapshot);
                if (order is not null)
                {
                    links.Add(Link("ORDER", order.OrderId, order.OrderCode));
                }

                var payment = ResolveLatestPayment(snapshot);
                if (payment is not null)
                {
                    links.Add(Link("PAYMENT", payment.PaymentId, payment.PaymentCode));
                }

                break;
            case ProjectWorkflowStageCatalog.StageProduction:
                var production = ResolveLatestProductionRequest(snapshot);
                if (production is not null)
                {
                    links.Add(Link(
                        "PRODUCTION_REQUEST",
                        production.ProductionRequestId,
                        production.ProductionCode ?? "Production request"));
                }

                var productionOrder = ResolvePrimaryOrder(snapshot);
                if (productionOrder is not null)
                {
                    links.Add(Link("ORDER", productionOrder.OrderId, productionOrder.OrderCode));
                }

                break;
            case ProjectWorkflowStageCatalog.StageDelivery:
                var deliverySchedule = snapshot.Schedules
                    .Where(s => s.ScheduleType is ProjectScheduleType.DELIVERY or ProjectScheduleType.HANDOVER)
                    .OrderByDescending(s => s.ScheduledStart)
                    .FirstOrDefault();
                if (deliverySchedule is not null)
                {
                    links.Add(Link("SCHEDULE", deliverySchedule.ScheduleId, deliverySchedule.Title ?? "Delivery"));
                }

                var deliveryOrder = ResolvePrimaryOrder(snapshot);
                if (deliveryOrder is not null)
                {
                    links.Add(Link("ORDER", deliveryOrder.OrderId, deliveryOrder.OrderCode));
                }

                var remainingPayment = ResolveLatestPayment(snapshot);
                if (remainingPayment is not null)
                {
                    links.Add(Link("PAYMENT", remainingPayment.PaymentId, remainingPayment.PaymentCode));
                }

                break;
        }

        return links;
    }

    private static IReadOnlyDictionary<string, object?> BuildFacts(
        string stageKey,
        string state,
        ProjectWorkflowSnapshotReadModel snapshot)
    {
        if (state == ProjectWorkflowStageCatalog.StateNotStarted)
        {
            return stageKey switch
            {
                ProjectWorkflowStageCatalog.StageIntake => FactMap(
                    ("submittedAt", null),
                    ("acceptedAt", null),
                    ("businessType", null)),
                ProjectWorkflowStageCatalog.StageDesignerAssignment => FactMap(
                    ("designerAssignedAt", null)),
                ProjectWorkflowStageCatalog.StageDesignReview => FactMap(
                    ("latestProposalStatus", null),
                    ("selectedProposalId", null)),
                ProjectWorkflowStageCatalog.StageQuotationOrder => FactMap(
                    ("latestQuotationStatus", null),
                    ("latestQuotationTotal", null),
                    ("orderCode", null),
                    ("orderStatus", null)),
                ProjectWorkflowStageCatalog.StageProduction => FactMap(
                    ("productionRequestStatus", null),
                    ("openItemCount", null),
                    ("blockedItemCount", null)),
                ProjectWorkflowStageCatalog.StageDelivery => FactMap(
                    ("deliveryScheduleStatus", null),
                    ("deliveredItemProgressPercent", null),
                    ("remainingAmount", null)),
                _ => new Dictionary<string, object?>()
            };
        }

        return stageKey switch
        {
            ProjectWorkflowStageCatalog.StageIntake => FactMap(
                ("submittedAt", snapshot.SubmittedAt),
                ("acceptedAt", snapshot.SalesAssignedAt),
                ("businessType", snapshot.BusinessType)),
            ProjectWorkflowStageCatalog.StageDesignerAssignment => FactMap(
                ("designerAssignedAt", snapshot.DesignerAssignedAt)),
            ProjectWorkflowStageCatalog.StageDesignReview => BuildDesignReviewFacts(snapshot),
            ProjectWorkflowStageCatalog.StageQuotationOrder => BuildQuotationOrderFacts(snapshot),
            ProjectWorkflowStageCatalog.StageProduction => BuildProductionFacts(snapshot),
            ProjectWorkflowStageCatalog.StageDelivery => BuildDeliveryFacts(snapshot),
            _ => new Dictionary<string, object?>()
        };
    }

    private static IReadOnlyDictionary<string, object?> BuildDesignReviewFacts(
        ProjectWorkflowSnapshotReadModel snapshot)
    {
        var latest = ResolveLatestProposal(snapshot);
        var selected = snapshot.Proposals
            .Where(p => p.Status == ProposalStatus.SELECTED)
            .OrderByDescending(p => p.SelectedAt)
            .FirstOrDefault();

        return FactMap(
            ("latestProposalStatus", latest?.Status?.ToString()),
            ("selectedProposalId", selected?.ProposalId));
    }

    private static IReadOnlyDictionary<string, object?> BuildQuotationOrderFacts(
        ProjectWorkflowSnapshotReadModel snapshot)
    {
        var latest = ResolveLatestQuotation(snapshot);
        var order = ResolvePrimaryOrder(snapshot);
        return FactMap(
            ("latestQuotationStatus", latest?.Status?.ToString()),
            ("latestQuotationTotal", latest?.TotalAmount),
            ("orderCode", order?.OrderCode),
            ("orderStatus", order?.Status?.ToString()));
    }

    private static IReadOnlyDictionary<string, object?> BuildProductionFacts(
        ProjectWorkflowSnapshotReadModel snapshot)
    {
        var latest = ResolveLatestProductionRequest(snapshot);
        var openItemStatuses = new HashSet<ProductionItemStatus>
        {
            ProductionItemStatus.PENDING,
            ProductionItemStatus.IN_PRODUCTION
        };
        var openItemCount = snapshot.ProductionItems.Count(i =>
            i.Status.HasValue && openItemStatuses.Contains(i.Status.Value));
        var blockedItemCount = snapshot.ProductionItems.Count(i => i.Status == ProductionItemStatus.CANCELLED);

        return FactMap(
            ("productionRequestStatus", latest?.Status?.ToString()),
            ("openItemCount", openItemCount),
            ("blockedItemCount", blockedItemCount));
    }

    private static IReadOnlyDictionary<string, object?> BuildDeliveryFacts(
        ProjectWorkflowSnapshotReadModel snapshot)
    {
        var schedule = snapshot.Schedules
            .Where(s => s.ScheduleType is ProjectScheduleType.DELIVERY or ProjectScheduleType.HANDOVER)
            .OrderByDescending(s => s.ScheduledStart)
            .FirstOrDefault();
        var order = ResolvePrimaryOrder(snapshot);

        var countableItems = snapshot.OrderItems
            .Where(i => i.Status is not (OrderItemStatus.CANCELLED or OrderItemStatus.UNAVAILABLE))
            .ToList();
        var totalQty = countableItems.Sum(i => i.Quantity ?? 0);
        var deliveredQty = countableItems
            .Where(IsOrderItemDelivered)
            .Sum(i => i.Quantity ?? 0);
        int? progress = totalQty <= 0
            ? null
            : (int)Math.Clamp(Math.Round(deliveredQty * 100d / totalQty), 0, 100);

        return FactMap(
            ("deliveryScheduleStatus", schedule?.Status?.ToString()),
            ("deliveredItemProgressPercent", progress),
            ("remainingAmount", order?.RemainingAmount));
    }

    private static bool IsOrderItemDelivered(ProjectWorkflowOrderItemReadModel item) =>
        item.Status == OrderItemStatus.DELIVERED || item.DeliveredAt.HasValue;

    private static int CountMeasurementSchedules(ProjectWorkflowSnapshotReadModel snapshot) =>
        snapshot.Schedules.Count(s =>
            s.ScheduleType == ProjectScheduleType.MEASUREMENT &&
            s.Status != ProjectScheduleStatus.CANCELLED);

    private static ProjectWorkflowProposalReadModel? ResolveLatestProposal(
        ProjectWorkflowSnapshotReadModel snapshot) =>
        snapshot.Proposals
            .OrderByDescending(p => p.UpdatedAt)
            .ThenByDescending(p => p.VersionNo)
            .FirstOrDefault();

    private static ProjectWorkflowQuotationReadModel? ResolveLatestQuotation(
        ProjectWorkflowSnapshotReadModel snapshot) =>
        snapshot.Quotations
            .OrderByDescending(q => q.UpdatedAt)
            .ThenByDescending(q => q.SentAt)
            .FirstOrDefault();

    private static ProjectWorkflowOrderReadModel? ResolvePrimaryOrder(
        ProjectWorkflowSnapshotReadModel snapshot) =>
        snapshot.Orders
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefault();

    private static ProjectWorkflowProductionRequestReadModel? ResolveLatestProductionRequest(
        ProjectWorkflowSnapshotReadModel snapshot) =>
        snapshot.ProductionRequests
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefault();

    private static ProjectWorkflowPaymentReadModel? ResolveLatestPayment(
        ProjectWorkflowSnapshotReadModel snapshot) =>
        snapshot.Payments
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefault();

    private static void AddAccountLink(
        List<ProjectWorkflowLinkDto> links,
        string type,
        Guid? id,
        string? name)
    {
        if (!id.HasValue)
        {
            return;
        }

        links.Add(Link(type, id.Value, name ?? type));
    }

    private static ProjectWorkflowMetricDto Metric(string key, string label, object? value, string? unit) =>
        new()
        {
            Key = key,
            Label = label,
            Value = value,
            Unit = unit
        };

    private static ProjectWorkflowLinkDto Link(string type, Guid id, string label) =>
        new()
        {
            Type = type,
            Id = id,
            Label = label
        };

    private static Dictionary<string, object?> FactMap(params (string Key, object? Value)[] entries)
    {
        var map = new Dictionary<string, object?>(entries.Length);
        foreach (var (key, value) in entries)
        {
            map[key] = value;
        }

        return map;
    }
}
