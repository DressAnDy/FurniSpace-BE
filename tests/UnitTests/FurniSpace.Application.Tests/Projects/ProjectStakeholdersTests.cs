#nullable enable

using System;
using System.Linq;
using FurniSpace.Application.Common.Projects;
using Xunit;

namespace FurniSpace.Application.Tests.Projects;

public sealed class ProjectStakeholdersTests
{
    [Fact]
    public void AllParticipantIds_IncludesCustomerSalesAndDesigner()
    {
        var customerId = Guid.NewGuid();
        var salesId = Guid.NewGuid();
        var designerId = Guid.NewGuid();
        var stakeholders = new ProjectStakeholders
        {
            CustomerId = customerId,
            AssignedSalesId = salesId,
            AssignedDesignerId = designerId
        };

        var ids = stakeholders.AllParticipantIds();

        Assert.Equal(3, ids.Count);
        Assert.Contains(customerId, ids);
        Assert.Contains(salesId, ids);
        Assert.Contains(designerId, ids);
    }

    [Fact]
    public void AllParticipantIds_WhenStaffMissing_ReturnsCustomerOnly()
    {
        var customerId = Guid.NewGuid();
        var stakeholders = new ProjectStakeholders { CustomerId = customerId };

        var ids = stakeholders.AllParticipantIds();

        Assert.Equal(customerId, Assert.Single(ids));
    }

    [Fact]
    public void AllParticipantIds_DeduplicatesWhenStaffMatchesCustomer()
    {
        var customerId = Guid.NewGuid();
        var stakeholders = new ProjectStakeholders
        {
            CustomerId = customerId,
            AssignedSalesId = customerId,
            AssignedDesignerId = customerId
        };

        Assert.Equal(customerId, Assert.Single(stakeholders.AllParticipantIds()));
    }

    [Fact]
    public void CustomerAndSalesIds_IncludesSalesWhenDifferentFromCustomer()
    {
        var customerId = Guid.NewGuid();
        var salesId = Guid.NewGuid();
        var stakeholders = new ProjectStakeholders
        {
            CustomerId = customerId,
            AssignedSalesId = salesId,
            AssignedDesignerId = Guid.NewGuid()
        };

        var ids = stakeholders.CustomerAndSalesIds();

        Assert.Equal([customerId, salesId], ids.ToArray());
    }

    [Fact]
    public void CustomerAndSalesIds_SkipsSalesWhenSameAsCustomerOrMissing()
    {
        var customerId = Guid.NewGuid();
        var sameAsCustomer = new ProjectStakeholders
        {
            CustomerId = customerId,
            AssignedSalesId = customerId
        };
        var missingSales = new ProjectStakeholders { CustomerId = customerId };

        Assert.Equal(customerId, Assert.Single(sameAsCustomer.CustomerAndSalesIds()));
        Assert.Equal(customerId, Assert.Single(missingSales.CustomerAndSalesIds()));
    }
}
