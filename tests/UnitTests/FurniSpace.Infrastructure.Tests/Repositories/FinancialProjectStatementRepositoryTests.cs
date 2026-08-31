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
        Assert.Equal(230m, statement.Items[statement.Items.Count - 1].RunningBalance);
    }

    [Fact]
    public async Task GetProjectStatementAsync_FiltersAndIncludesAdjustmentAndRefundTxn()
    {
        await using var context = CreateContext();
        var from = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        var projectId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var depositPaymentId = Guid.NewGuid();
        var orphanRefundPaymentId = Guid.NewGuid();

        context.RoleSet.Add(new Role { RoleId = Guid.NewGuid(), RoleName = "CUSTOMER", Description = "c" });
        var roleId = context.RoleSet.Local.First().RoleId;
        context.AccountSet.Add(new Account
        {
            AccountId = customerId,
            RoleId = roleId,
            Email = "stmt2@test.com",
            PasswordHash = "x",
            FullName = "Customer 2",
            Status = AccountStatus.ACTIVE,
            CreatedAt = from
        });
        context.ProjectSet.Add(new Project
        {
            ProjectId = projectId,
            CustomerId = customerId,
            ProjectCode = "PRJ-STMT2",
            ProjectName = "Statement 2",
            FurnitureRequirement = "Chairs",
            Status = ProjectStatus.IN_PRODUCTION,
            CreatedAt = from
        });
        context.OrderSet.Add(new Order
        {
            OrderId = orderId,
            ProjectId = projectId,
            QuotationId = Guid.NewGuid(),
            OrderCode = "ORD-STMT2",
            CustomerId = customerId,
            VatRate = 0.1m,
            VatAmount = 0,
            FinalTotalAmount = 1000,
            PaidAmount = 200,
            RemainingAmount = 800,
            Status = OrderStatus.IN_PRODUCTION,
            ConfirmedAt = from.AddDays(1),
            CreatedAt = from.AddDays(1)
        });
        context.PaymentSet.AddRange(
            new Payment
            {
                PaymentId = depositPaymentId,
                ProjectId = projectId,
                OrderId = orderId,
                PaymentCode = "PAY-DEP2",
                PaymentType = PaymentType.DEPOSIT,
                Amount = 200m,
                Currency = "VND",
                Status = PaymentStatus.PAID,
                PaidAt = from.AddDays(2),
                CreatedAt = from.AddDays(2)
            },
            new Payment
            {
                PaymentId = orphanRefundPaymentId,
                ProjectId = projectId,
                OrderId = orderId,
                PaymentCode = "PAY-ORPHAN-REF",
                PaymentType = PaymentType.REFUND,
                Amount = 15m,
                Currency = "VND",
                Status = PaymentStatus.PENDING,
                CreatedAt = from.AddDays(4)
            });
        context.PaymentTransactionSet.AddRange(
            new PaymentTransaction
            {
                PaymentTransactionId = Guid.NewGuid(),
                PaymentId = depositPaymentId,
                ProjectId = projectId,
                OrderId = orderId,
                TransactionCode = "TX-DEP-OK",
                TransactionType = PaymentTransactionType.CHARGE,
                Amount = 200m,
                Currency = "VND",
                PaymentProvider = PaymentProvider.PAYOS,
                Status = PaymentTransactionStatus.SUCCESS,
                ConfirmedAt = from.AddDays(2),
                CreatedAt = from.AddDays(2)
            },
            new PaymentTransaction
            {
                PaymentTransactionId = Guid.NewGuid(),
                PaymentId = depositPaymentId,
                ProjectId = projectId,
                OrderId = orderId,
                TransactionCode = "TX-ADJ",
                TransactionType = PaymentTransactionType.ADJUSTMENT,
                Amount = -10m,
                Currency = "VND",
                PaymentProvider = PaymentProvider.PAYOS,
                Status = PaymentTransactionStatus.SUCCESS,
                ConfirmedAt = from.AddDays(3),
                CreatedAt = from.AddDays(3)
            },
            new PaymentTransaction
            {
                PaymentTransactionId = Guid.NewGuid(),
                PaymentId = orphanRefundPaymentId,
                ProjectId = projectId,
                OrderId = orderId,
                TransactionCode = "TX-REF-TXN",
                TransactionType = PaymentTransactionType.REFUND,
                Amount = 15m,
                Currency = "VND",
                PaymentProvider = PaymentProvider.SEPAY,
                Status = PaymentTransactionStatus.SUCCESS,
                ConfirmedAt = from.AddDays(4),
                CreatedAt = from.AddDays(4)
            });
        await context.SaveChangesAsync();

        var repository = new FinancialReadRepository(context);

        var all = await repository.GetProjectStatementAsync(
            new AdminFinancialProjectStatementQueryReadModel
            {
                ProjectId = projectId,
                FromUtc = from,
                ToUtcExclusive = to,
                Page = 1,
                PageSize = 20,
                SortDirection = "desc"
            });
        Assert.NotNull(all);
        Assert.Contains(all!.Items, i => i.EntryType == "ADJUSTMENT" && i.Amount == 10m);
        Assert.Contains(all.Items, i => i.EntryType == "REFUND" && i.Amount == 15m && i.ReferenceCode == "TX-REF-TXN");
        Assert.Equal(3, all.TotalItems);

        var filtered = await repository.GetProjectStatementAsync(
            new AdminFinancialProjectStatementQueryReadModel
            {
                ProjectId = projectId,
                FromUtc = from,
                ToUtcExclusive = to,
                EntryType = "COLLECTION",
                PaymentType = PaymentType.DEPOSIT,
                Status = "PAID",
                Provider = PaymentProvider.PAYOS,
                Page = 1,
                PageSize = 10,
                SortDirection = "asc"
            });
        Assert.NotNull(filtered);
        Assert.Single(filtered!.Items);
        Assert.Equal("COLLECTION", filtered.Items[0].EntryType);
        Assert.Equal("PAYOS", filtered.Items[0].Provider);
    }

    [Fact]
    public async Task GetProjectStatementAsync_CoversPaymentTypeDescriptionsAndEmptyProviders()
    {
        await using var context = CreateContext();
        var from = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        var projectId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        context.RoleSet.Add(new Role { RoleId = Guid.NewGuid(), RoleName = "CUSTOMER", Description = "c" });
        var roleId = context.RoleSet.Local.First().RoleId;
        context.AccountSet.Add(new Account
        {
            AccountId = customerId,
            RoleId = roleId,
            Email = "stmt3@test.com",
            PasswordHash = "x",
            FullName = "Customer 3",
            Status = AccountStatus.ACTIVE,
            CreatedAt = from
        });
        context.ProjectSet.Add(new Project
        {
            ProjectId = projectId,
            CustomerId = customerId,
            ProjectCode = "PRJ-STMT3",
            ProjectName = "Statement 3",
            FurnitureRequirement = "Desks",
            Status = ProjectStatus.IN_PRODUCTION,
            CreatedAt = from
        });
        context.PaymentSet.AddRange(
            new Payment
            {
                PaymentId = Guid.NewGuid(),
                ProjectId = projectId,
                PaymentCode = "PAY-START",
                PaymentType = PaymentType.PROJECT_START_FEE,
                Amount = 50m,
                Currency = "VND",
                Status = PaymentStatus.PAID,
                PaidAt = from.AddDays(1),
                CreatedAt = from.AddDays(1)
            },
            new Payment
            {
                PaymentId = Guid.NewGuid(),
                ProjectId = projectId,
                PaymentCode = "PAY-FULL",
                PaymentType = PaymentType.FULL_PAYMENT,
                Amount = 500m,
                Currency = "VND",
                Status = PaymentStatus.PAID,
                PaidAt = from.AddDays(2),
                CreatedAt = from.AddDays(2)
            },
            new Payment
            {
                PaymentId = Guid.NewGuid(),
                ProjectId = projectId,
                PaymentCode = "PAY-REM",
                PaymentType = PaymentType.REMAINING_PAYMENT,
                Amount = 100m,
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
        Assert.Equal(3, statement!.TotalItems);
        Assert.Contains(statement.Items, i => i.Description == "Project start fee collected");
        Assert.Contains(statement.Items, i => i.Description == "Full payment collected");
        Assert.Contains(statement.Items, i => i.Description == "Remaining payment collected");
        Assert.All(statement.Items, i => Assert.Null(i.Provider));
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
