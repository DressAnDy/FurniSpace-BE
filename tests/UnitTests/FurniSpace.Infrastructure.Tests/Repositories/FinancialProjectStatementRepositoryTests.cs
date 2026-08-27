#nullable enable

using System;
using System.Linq;
using System.Threading.Tasks;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.ReadModels.Financial;
using FurniSpace.Infrastructure.Repositories.Repository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FurniSpace.Infrastructure.Tests.Repositories;

public sealed class FinancialProjectStatementRepositoryTests
{
    [Fact]
    public async Task GetProjectStatementAsync_ComputesBalancesAndCollectionEntries()
    {
        await using var context = CreateContext();
        var from = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        var projectId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        context.RoleSet.Add(new Role { RoleId = Guid.NewGuid(), RoleName = "CUSTOMER", Description = "c" });
        var roleId = context.RoleSet.Local.First().RoleId;
        context.AccountSet.Add(new Account
        {
            AccountId = customerId,
            RoleId = roleId,
            Email = "stmt@test.com",
            PasswordHash = "x",
            FullName = "Statement Customer",
            Status = AccountStatus.ACTIVE,
            CreatedAt = from
        });
        context.ProjectSet.Add(new Project
        {
            ProjectId = projectId,
            CustomerId = customerId,
            ProjectCode = "PRJ-STMT",
            ProjectName = "Statement Project",
            FurnitureRequirement = "Tables",
            Status = ProjectStatus.IN_PRODUCTION,
            CreatedAt = from
        });
        context.OrderSet.Add(new Order
        {
            OrderId = orderId,
            ProjectId = projectId,
            QuotationId = Guid.NewGuid(),
            OrderCode = "ORD-STMT",
            CustomerId = customerId,
            VatRate = 0.1m,
            VatAmount = 0,
            OriginalTotalAmount = 1000,
            FinalTotalAmount = 1000,
            PaidAmount = 300,
            RemainingAmount = 700,
            Status = OrderStatus.IN_PRODUCTION,
            ConfirmedAt = from.AddDays(1),
            CreatedAt = from.AddDays(1)
        });
        context.PaymentSet.AddRange(
            new Payment
            {
                PaymentId = Guid.NewGuid(),
                ProjectId = projectId,
                PaymentCode = "PAY-BEFORE",
                PaymentType = PaymentType.PROJECT_START_FEE,
                Amount = 50m,
                Currency = "VND",
                Status = PaymentStatus.PAID,
                PaidAt = from.AddDays(-5),
                CreatedAt = from.AddDays(-5)
            },
            new Payment
            {
                PaymentId = Guid.NewGuid(),
                ProjectId = projectId,
                OrderId = orderId,
                PaymentCode = "PAY-DEP",
                PaymentType = PaymentType.DEPOSIT,
                Amount = 200m,
                Currency = "VND",
                Status = PaymentStatus.PAID,
                PaidAt = from.AddDays(2),
                CreatedAt = from.AddDays(2)
            },
            new Payment
            {
                PaymentId = Guid.NewGuid(),
                ProjectId = projectId,
                OrderId = orderId,
                PaymentCode = "PAY-REF",
                PaymentType = PaymentType.REFUND,
                Amount = 20m,
                Currency = "VND",
                Status = PaymentStatus.PAID,
                PaidAt = from.AddDays(3),
                CreatedAt = from.AddDays(3)
            });
        await context.SaveChangesAsync();

        var repository = new FinancialReadRepository(context);
        var statement = await repository.GetProjectStatementAsync(
            new AdminFinancialProjectStatementQueryReadModel
            {
                ProjectId = projectId,
                FromUtc = from,
                ToUtcExclusive = to,
                Page = 1,
                PageSize = 10,
                SortDirection = "asc"
            });

        Assert.NotNull(statement);
        Assert.Equal(50m, statement!.OpeningBalance);
        Assert.Equal(200m, statement.TotalCollected);
        Assert.Equal(20m, statement.TotalRefunded);
        Assert.Equal(180m, statement.NetCollected);
        Assert.Equal(230m, statement.ClosingBalance);
        Assert.Equal(2, statement.TotalItems);
        Assert.Equal("Statement Customer", statement.CustomerName);
        Assert.Contains(statement.Items, i => i.EntryType == "COLLECTION" && i.Amount == 200m);
        Assert.Contains(statement.Items, i => i.EntryType == "REFUND" && i.Amount == 20m);
        Assert.Equal(230m, statement.Items.Last().RunningBalance);
    }

    [Fact]
    public async Task GetProjectStatementAsync_WhenProjectMissing_ReturnsNull()
    {
        await using var context = CreateContext();
        var repository = new FinancialReadRepository(context);
        var statement = await repository.GetProjectStatementAsync(
            new AdminFinancialProjectStatementQueryReadModel
            {
                ProjectId = Guid.NewGuid(),
                FromUtc = DateTime.UtcNow.AddDays(-30),
                ToUtcExclusive = DateTime.UtcNow,
                Page = 1,
                PageSize = 10
            });
        Assert.Null(statement);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }
}
