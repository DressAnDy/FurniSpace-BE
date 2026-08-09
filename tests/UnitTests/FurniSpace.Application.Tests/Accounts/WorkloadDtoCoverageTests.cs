#nullable enable

using System;
using FurniSpace.Application.DTOs.Accounts;
using FurniSpace.Infrastructure.ReadModels.Accounts;
using Xunit;

namespace FurniSpace.Application.Tests.Accounts;

/// <summary>
/// Touches DTO/read-model property surfaces so Sonar coverage sees auto-property accessors.
/// </summary>
public sealed class WorkloadDtoCoverageTests
{
    [Fact]
    public void DesignerAndSalesWorkloadDtos_ExposeAssignedProperties()
    {
        var designerQuery = new DesignerWorkloadQueryDto
        {
            Page = 1,
            PageSize = 20,
            Search = "a",
            CapacityState = "AVAILABLE",
            SortBy = "AvailableSlotDesc"
        };
        Assert.Equal(1, designerQuery.Page);
        Assert.Equal(20, designerQuery.PageSize);
        Assert.Equal("a", designerQuery.Search);
        Assert.Equal("AVAILABLE", designerQuery.CapacityState);
        Assert.Equal("AvailableSlotDesc", designerQuery.SortBy);

        var designerSummary = new DesignerWorkloadSummaryDto
        {
            TotalActiveDesigners = 3,
            AvailableCount = 1,
            FullCount = 1,
            OverCount = 1,
            TotalDesignActiveProjects = 4,
            MaxActiveProjects = 2
        };
        Assert.Equal(3, designerSummary.TotalActiveDesigners);
        Assert.Equal(1, designerSummary.AvailableCount);
        Assert.Equal(1, designerSummary.FullCount);
        Assert.Equal(1, designerSummary.OverCount);
        Assert.Equal(4, designerSummary.TotalDesignActiveProjects);
        Assert.Equal(2, designerSummary.MaxActiveProjects);

        var assignedQuery = new DesignerAssignedProjectQueryDto { Page = 2, PageSize = 10, Bucket = "DESIGN_ACTIVE" };
        Assert.Equal(2, assignedQuery.Page);
        Assert.Equal(10, assignedQuery.PageSize);
        Assert.Equal("DESIGN_ACTIVE", assignedQuery.Bucket);

        var assigned = new DesignerAssignedProjectDto
        {
            ProjectId = Guid.NewGuid(),
            ProjectCode = "PRJ-1",
            ProjectName = "Cafe",
            Status = "PROPOSAL_CONSULTING",
            DesignerAssignedAt = DateTime.UtcNow,
            CustomerId = Guid.NewGuid(),
            CustomerName = "Cust",
            AssignedSalesId = Guid.NewGuid(),
            SalesName = "Sales",
            Bucket = "DESIGN_ACTIVE"
        };
        Assert.Equal("PRJ-1", assigned.ProjectCode);
        Assert.Equal("DESIGN_ACTIVE", assigned.Bucket);
        Assert.Equal("Cust", assigned.CustomerName);
        Assert.Equal("Sales", assigned.SalesName);

        var salesQuery = new SalesWorkloadQueryDto
        {
            Page = 1,
            PageSize = 20,
            Search = "s",
            CapacityState = "AVAILABLE_NOW",
            FuturePressureState = "HIGH",
            SortBy = "FuturePressureScoreDesc"
        };
        Assert.Equal("HIGH", salesQuery.FuturePressureState);
        Assert.Equal("AVAILABLE_NOW", salesQuery.CapacityState);

        var salesSummary = new SalesWorkloadSummaryDto
        {
            TotalActiveSales = 5,
            AvailableNowCount = 2,
            FullNowCount = 2,
            OverNowCount = 1,
            HighFuturePressureCount = 3,
            TotalSalesActiveProjects = 11,
            UnassignedIntakeCount = 4,
            MaxActiveProjects = 5
        };
        Assert.Equal(5, salesSummary.TotalActiveSales);
        Assert.Equal(4, salesSummary.UnassignedIntakeCount);

        var salesItem = new SalesWorkloadItemDto
        {
            AccountId = Guid.NewGuid(),
            Email = "s@example.com",
            FullName = "Sales",
            Phone = "1",
            AvatarUrl = "u",
            Status = "ACTIVE",
            IntakeCount = 1,
            CommercialCount = 1,
            DesignMonitorCount = 1,
            FulfillmentCount = 1,
            SalesActiveCount = 2,
            LifecycleAssignedCount = 4,
            MaxActiveProjects = 5,
            AvailableSlot = 3,
            CapacityState = "AVAILABLE_NOW",
            FuturePressureScore = 2.5m,
            FuturePressureState = "MEDIUM",
            ApproachingCommercialCount = 1,
            ProductionAttentionCount = 0,
            DeliveryAttentionCount = 2,
            FuturePressureBreakdown = new SalesFuturePressureBreakdownDto
            {
                MeasurementRequiredCount = 1,
                SpaceVerifiedCount = 1,
                ProposalConsultingCount = 1,
                InProductionCount = 1,
                ProductionBlockedCount = 0,
                ReadyForDeliveryCount = 1,
                DeliveringCount = 0,
                DeliveredCount = 1
            },
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        Assert.Equal(2.5m, salesItem.FuturePressureScore);
        Assert.Equal(1, salesItem.FuturePressureBreakdown.ProposalConsultingCount);

        var salesAssignedQuery = new SalesAssignedProjectQueryDto { Page = 1, PageSize = 20, Bucket = "INTAKE" };
        Assert.Equal("INTAKE", salesAssignedQuery.Bucket);

        var salesAssigned = new SalesAssignedProjectDto
        {
            ProjectId = Guid.NewGuid(),
            ProjectCode = "PRJ-S",
            ProjectName = "Office",
            Status = "IN_CONSULTATION",
            SalesAssignedAt = DateTime.UtcNow,
            CustomerId = Guid.NewGuid(),
            CustomerName = "C",
            AssignedDesignerId = Guid.NewGuid(),
            DesignerName = "D",
            Bucket = "INTAKE",
            PressureWeight = 0m
        };
        Assert.Equal("INTAKE", salesAssigned.Bucket);
        Assert.Equal(0m, salesAssigned.PressureWeight);

        var unassignedQuery = new UnassignedIntakeProjectQueryDto { Page = 1, PageSize = 20 };
        Assert.Equal(1, unassignedQuery.Page);

        var unassigned = new UnassignedIntakeProjectDto
        {
            ProjectId = Guid.NewGuid(),
            ProjectCode = "PRJ-U",
            ProjectName = "Lead",
            BusinessType = "Cafe",
            SubmittedAt = DateTime.UtcNow,
            CustomerId = Guid.NewGuid(),
            CustomerName = "Lead Cust"
        };
        Assert.Equal("Cafe", unassigned.BusinessType);

        var salesAssignedRead = new SalesAssignedProjectReadModel
        {
            ProjectId = Guid.NewGuid(),
            ProjectCode = "PRJ-R",
            ProjectName = "Read",
            Status = Domain.Enums.ProjectStatus.QUOTATION_SENT,
            SalesAssignedAt = DateTime.UtcNow,
            CustomerId = Guid.NewGuid(),
            CustomerName = "Cust",
            AssignedDesignerId = Guid.NewGuid(),
            DesignerName = "Designer"
        };
        Assert.Equal("PRJ-R", salesAssignedRead.ProjectCode);
        Assert.Equal("Designer", salesAssignedRead.DesignerName);
    }
}
