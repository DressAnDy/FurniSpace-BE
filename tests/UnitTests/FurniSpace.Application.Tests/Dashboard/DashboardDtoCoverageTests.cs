using System;
using System.Collections.Generic;
using FurniSpace.Application.DTOs.Dashboard;
using Xunit;

namespace FurniSpace.Application.Tests.Dashboard;

public sealed class DashboardDtoCoverageTests
{
    [Fact]
    public void DtoProperties_AreAssignable()
    {
        var item = new DashboardQueueItemDto
        {
            Id = "1",
            ProjectId = Guid.NewGuid(),
            ProjectCode = "P",
            ProjectName = "N",
            CustomerName = "C",
            AssigneeName = "A",
            Group = "Intake",
            Phase = "SUBMITTED",
            Status = "SUBMITTED",
            Priority = "HIGH",
            Action = "Review",
            ActionPath = "/projects/1",
            DueAt = DateTime.UtcNow,
            DueBucket = "TODAY",
            Warning = "w",
            LastUpdatedAt = DateTime.UtcNow
        };
        var queue = new DashboardQueueResponseDto
        {
            Items = [item],
            CountsByGroup = new Dictionary<string, int> { ["Intake"] = 1 },
            Page = 1,
            Limit = 20,
            Total = 1
        };
        var query = new DashboardQueueQueryDto
        {
            Scope = "mine",
            Group = "Intake",
            DateRange = "today",
            Priority = "HIGH",
            Search = "x",
            Page = 2,
            Limit = 10
        };
        var salesKpis = new SalesDashboardKpisDto
        {
            NewRequests = 1,
            WaitingCustomer = 2,
            PaymentFollowUp = 3,
            OverdueTasks = 4,
            ActiveProjects = 5
        };
        var designerKpis = new DesignerDashboardKpisDto
        {
            MeasurementDue = 1,
            ProposalsInProgress = 2,
            RevisionRequested = 3,
            OverdueTasks = 4
        };
        var productionKpis = new ProductionDashboardKpisDto
        {
            PendingReview = 1,
            InProduction = 2,
            ReadyToComplete = 3,
            OverdueTasks = 4
        };

        Assert.Equal(1, queue.Total);
        Assert.Equal("mine", query.Scope);
        Assert.Equal(5, salesKpis.ActiveProjects);
        Assert.Equal(4, designerKpis.OverdueTasks);
        Assert.Equal(3, productionKpis.ReadyToComplete);
        Assert.Equal("HIGH", item.Priority);
    }
}
