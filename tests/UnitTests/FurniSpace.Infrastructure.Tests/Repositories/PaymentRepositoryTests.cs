#nullable enable

using System;
using System.Threading.Tasks;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.ReadModels.Payments;
using FurniSpace.Infrastructure.Repositories.Repository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FurniSpace.Infrastructure.Tests.Repositories;

public sealed class PaymentRepositoryTests
{
    [Fact]
    public async Task GetDetailByPaymentCodeAsync_ReturnsPaymentWithAssignmentMetadata()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new PaymentRepository(context);

        var detail = await repository.GetDetailByPaymentCodeAsync(data.PaymentCode);

        Assert.NotNull(detail);
        Assert.Equal(data.PaymentId, detail!.PaymentId);
        Assert.Equal(data.CustomerId, detail.CustomerId);
        Assert.Equal(data.SalesId, detail.AssignedSalesId);
    }

    [Fact]
    public async Task GetByOrderAndTypeAsync_ReturnsLatestPayment()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new PaymentRepository(context);

        var payment = await repository.GetByOrderAndTypeAsync(data.OrderId, PaymentType.DEPOSIT);

        Assert.NotNull(payment);
        Assert.Equal(data.PaymentId, payment!.PaymentId);
    }

    [Fact]
    public async Task SumOrderScopedPaidAmountAsync_SumsPaidPaymentsOnly()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        context.PaymentSet.Add(new Payment
        {
            PaymentId = Guid.NewGuid(),
            ProjectId = data.ProjectId,
            OrderId = data.OrderId,
            PaymentCode = "FS87654321",
            PaymentType = PaymentType.REMAINING_PAYMENT,
            Amount = 70m,
            Status = PaymentStatus.PAID,
            CreatedAt = DateTime.UtcNow.AddMinutes(1)
        });
        context.PaymentSet.Add(new Payment
        {
            PaymentId = Guid.NewGuid(),
            ProjectId = data.ProjectId,
            OrderId = data.OrderId,
            PaymentCode = "FS87654322",
            PaymentType = PaymentType.REMAINING_PAYMENT,
            Amount = 20m,
            Status = PaymentStatus.PROCESSING,
            CreatedAt = DateTime.UtcNow.AddMinutes(2)
        });
        await context.SaveChangesAsync();
        var repository = new PaymentRepository(context);

        var sum = await repository.SumOrderScopedPaidAmountAsync(data.OrderId);

        Assert.Equal(100m, sum);
    }

    [Fact]
    public async Task GetListAsync_FiltersByProject()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new PaymentRepository(context);

        var items = await repository.GetListAsync(new PaymentQueryReadModel
        {
            ProjectId = data.ProjectId,
            AccessRole = "ADMIN"
        });

        Assert.Single(items);
        Assert.Equal(data.PaymentId, items[0].PaymentId);
    }

    [Fact]
    public async Task GetDetailAsync_ReturnsPaymentDetail()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new PaymentRepository(context);

        var detail = await repository.GetDetailAsync(data.PaymentId);

        Assert.NotNull(detail);
        Assert.Equal(data.PaymentCode, detail!.PaymentCode);
    }

    [Fact]
    public async Task GetStatusByPaymentCodeAsync_ReturnsStatus()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new PaymentRepository(context);

        var status = await repository.GetStatusByPaymentCodeAsync(data.PaymentCode);

        Assert.NotNull(status);
        Assert.Equal(PaymentStatus.PAID, status!.Status);
    }

    [Fact]
    public async Task PaymentCodeExistsAsync_WhenCodeExists_ReturnsTrue()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new PaymentRepository(context);

        var exists = await repository.PaymentCodeExistsAsync(data.PaymentCode);

        Assert.True(exists);
    }

    [Fact]
    public async Task GetByProjectAndTypeAsync_ReturnsProjectStartFeePayment()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var paymentId = Guid.NewGuid();
        context.PaymentSet.Add(new Payment
        {
            PaymentId = paymentId,
            ProjectId = data.ProjectId,
            PaymentCode = "FS99999999",
            PaymentType = PaymentType.PROJECT_START_FEE,
            Amount = 500000m,
            Status = PaymentStatus.PENDING,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        var repository = new PaymentRepository(context);

        var payment = await repository.GetByProjectAndTypeAsync(
            data.ProjectId,
            PaymentType.PROJECT_START_FEE);

        Assert.NotNull(payment);
        Assert.Equal(paymentId, payment!.PaymentId);
    }

    [Fact]
    public async Task GetListByOrderIdAsync_ReturnsPaymentsOrderedByCreatedAtDesc()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var olderPaymentId = Guid.NewGuid();
        context.PaymentSet.Add(new Payment
        {
            PaymentId = olderPaymentId,
            ProjectId = data.ProjectId,
            OrderId = data.OrderId,
            PaymentCode = "FS11111111",
            PaymentType = PaymentType.REMAINING_PAYMENT,
            Amount = 70m,
            Status = PaymentStatus.PENDING,
            CreatedAt = DateTime.UtcNow.AddHours(-2)
        });
        await context.SaveChangesAsync();
        var repository = new PaymentRepository(context);

        var payments = await repository.GetListByOrderIdAsync(data.OrderId);

        Assert.Equal(2, payments.Count);
        Assert.Equal(data.PaymentId, payments[0].PaymentId);
        Assert.Equal(olderPaymentId, payments[1].PaymentId);
    }

    [Fact]
    public async Task GetListByOrderIdAsync_AppliesStatusAndTypeFilters()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new PaymentRepository(context);

        var payments = await repository.GetListByOrderIdAsync(
            data.OrderId,
            PaymentStatus.PAID,
            PaymentType.DEPOSIT);

        var payment = Assert.Single(payments);
        Assert.Equal(data.PaymentId, payment.PaymentId);
        Assert.Equal(PaymentType.DEPOSIT, payment.PaymentType);
    }

    [Fact]
    public async Task GetTransactionsByPaymentIdsAsync_ReturnsTransactionsForMultiplePayments()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var secondPaymentId = Guid.NewGuid();
        context.PaymentSet.Add(new Payment
        {
            PaymentId = secondPaymentId,
            ProjectId = data.ProjectId,
            OrderId = data.OrderId,
            PaymentCode = "FS22222222",
            PaymentType = PaymentType.REMAINING_PAYMENT,
            Amount = 70m,
            Status = PaymentStatus.PENDING,
            CreatedAt = DateTime.UtcNow
        });
        context.PaymentTransactionSet.AddRange(
            new PaymentTransaction
            {
                PaymentTransactionId = Guid.NewGuid(),
                PaymentId = data.PaymentId,
                ProjectId = data.ProjectId,
                TransactionCode = "TXN-001",
                TransactionType = PaymentTransactionType.CHARGE,
                Amount = 30m,
                Status = PaymentTransactionStatus.SUCCESS,
                CreatedAt = DateTime.UtcNow
            },
            new PaymentTransaction
            {
                PaymentTransactionId = Guid.NewGuid(),
                PaymentId = secondPaymentId,
                ProjectId = data.ProjectId,
                TransactionCode = "TXN-002",
                TransactionType = PaymentTransactionType.CHARGE,
                Amount = 70m,
                Status = PaymentTransactionStatus.FAILED,
                FailureReason = "Declined",
                CreatedAt = DateTime.UtcNow
            });
        await context.SaveChangesAsync();
        var repository = new PaymentRepository(context);

        var transactions = await repository.GetTransactionsByPaymentIdsAsync(
            [data.PaymentId, secondPaymentId]);

        Assert.Equal(2, transactions.Count);
        Assert.Contains(transactions, transaction => transaction.TransactionCode == "TXN-001");
        Assert.Contains(transactions, transaction => transaction.TransactionCode == "TXN-002");
    }

    [Fact]
    public async Task GetTransactionsByPaymentIdAsync_ReturnsTransactions()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        context.PaymentTransactionSet.Add(new PaymentTransaction
        {
            PaymentTransactionId = Guid.NewGuid(),
            PaymentId = data.PaymentId,
            ProjectId = data.ProjectId,
            TransactionCode = "TXN-001",
            TransactionType = PaymentTransactionType.CHARGE,
            Amount = 30m,
            Currency = "VND",
            Status = PaymentTransactionStatus.SUCCESS,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        var repository = new PaymentRepository(context);

        var transactions = await repository.GetTransactionsByPaymentIdAsync(data.PaymentId);

        Assert.Single(transactions);
        Assert.Equal("TXN-001", transactions[0].TransactionCode);
    }

    [Fact]
    public async Task GetLatestPendingTransactionAsync_ReturnsMatchingPendingTransaction()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var transactionId = Guid.NewGuid();
        context.PaymentTransactionSet.Add(new PaymentTransaction
        {
            PaymentTransactionId = transactionId,
            PaymentId = data.PaymentId,
            ProjectId = data.ProjectId,
            TransactionCode = "TXN-PENDING",
            TransactionType = PaymentTransactionType.CHARGE,
            Amount = 30m,
            Currency = "VND",
            PaymentProvider = PaymentProvider.SEPAY,
            PaymentMethod = PaymentMethod.QR_CODE,
            Status = PaymentTransactionStatus.PENDING,
            PaymentUrl = "https://vietqr.test",
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        var repository = new PaymentRepository(context);

        var transaction = await repository.GetLatestPendingTransactionAsync(
            data.PaymentId,
            PaymentProvider.SEPAY,
            PaymentMethod.QR_CODE);

        Assert.NotNull(transaction);
        Assert.Equal(transactionId, transaction!.PaymentTransactionId);
        Assert.Equal("TXN-PENDING", transaction.TransactionCode);
    }

    [Fact]
    public async Task HasSuccessfulTransactionAsync_WhenSuccessExists_ReturnsTrue()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        context.PaymentTransactionSet.Add(new PaymentTransaction
        {
            PaymentTransactionId = Guid.NewGuid(),
            PaymentId = data.PaymentId,
            ProjectId = data.ProjectId,
            TransactionCode = "TXN-SUCCESS",
            TransactionType = PaymentTransactionType.CHARGE,
            Amount = 30m,
            Currency = "VND",
            Status = PaymentTransactionStatus.SUCCESS,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        var repository = new PaymentRepository(context);

        var hasSuccess = await repository.HasSuccessfulTransactionAsync(data.PaymentId);

        Assert.True(hasSuccess);
    }

    [Fact]
    public async Task GetSummaryAsync_CountsPayablePaymentsExcludingSuccessfulTransactions()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var pendingPaymentId = Guid.NewGuid();
        context.PaymentSet.Add(new Payment
        {
            PaymentId = pendingPaymentId,
            ProjectId = data.ProjectId,
            OrderId = data.OrderId,
            PaymentCode = "FS87654321",
            PaymentType = PaymentType.REMAINING_PAYMENT,
            Amount = 70m,
            Status = PaymentStatus.PENDING,
            CreatedAt = DateTime.UtcNow
        });
        context.PaymentTransactionSet.Add(new PaymentTransaction
        {
            PaymentTransactionId = Guid.NewGuid(),
            PaymentId = pendingPaymentId,
            ProjectId = data.ProjectId,
            TransactionCode = "TXN-SUCCESS",
            TransactionType = PaymentTransactionType.CHARGE,
            Amount = 70m,
            Currency = "VND",
            Status = PaymentTransactionStatus.SUCCESS,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        var repository = new PaymentRepository(context);

        var summary = await repository.GetSummaryAsync(
            new PaymentQueryReadModel { AccessRole = "ADMIN" },
            DateTime.UtcNow);

        Assert.Equal(1, summary.PaidCount);
        Assert.Equal(0, summary.PayableCount);
    }

    [Fact]
    public async Task GetExpiredPaymentsForSyncAsync_ReturnsExpiredPendingPayments()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var expiredPaymentId = Guid.NewGuid();
        context.PaymentSet.Add(new Payment
        {
            PaymentId = expiredPaymentId,
            ProjectId = data.ProjectId,
            OrderId = data.OrderId,
            PaymentCode = "FS87654323",
            PaymentType = PaymentType.REMAINING_PAYMENT,
            Amount = 70m,
            Status = PaymentStatus.PENDING,
            ExpiredAt = DateTime.UtcNow.AddMinutes(-10),
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        var repository = new PaymentRepository(context);

        var expiredPayments = await repository.GetExpiredPaymentsForSyncAsync(
            new PaymentQueryReadModel { AccessRole = "ADMIN" },
            DateTime.UtcNow);

        Assert.Single(expiredPayments);
        Assert.Equal(expiredPaymentId, expiredPayments[0].PaymentId);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<SeededData> SeedAsync(AppDbContext context)
    {
        var customerRole = new Role { RoleId = Guid.NewGuid(), RoleName = "CUSTOMER" };
        var salesRole = new Role { RoleId = Guid.NewGuid(), RoleName = "SALES" };
        var customerId = Guid.NewGuid();
        var salesId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        const string paymentCode = "FS12345678";

        context.RoleSet.AddRange(customerRole, salesRole);
        context.AccountSet.AddRange(
            CreateAccount(customerId, customerRole.RoleId, "customer@example.com"),
            CreateAccount(salesId, salesRole.RoleId, "sales@example.com"));
        context.ProjectSet.Add(new Project
        {
            ProjectId = projectId,
            CustomerId = customerId,
            AssignedSalesId = salesId,
            ProjectCode = "PRJ-001",
            ProjectName = "Cafe",
            BusinessType = "Cafe",
            Status = ProjectStatus.ORDER_CONFIRMED,
            CreatedAt = DateTime.UtcNow
        });
        context.OrderSet.Add(new Order
        {
            OrderId = orderId,
            ProjectId = projectId,
            QuotationId = Guid.NewGuid(),
            OrderCode = "ORD-001",
            CustomerId = customerId,
            SalesId = salesId,
            FinalTotalAmount = 100m,
            DepositAmount = 30m,
            PaidAmount = 0m,
            RemainingAmount = 100m,
            Status = OrderStatus.DEPOSIT_PENDING,
            CreatedAt = DateTime.UtcNow
        });
        context.PaymentSet.Add(new Payment
        {
            PaymentId = paymentId,
            ProjectId = projectId,
            OrderId = orderId,
            PaymentCode = paymentCode,
            PaymentType = PaymentType.DEPOSIT,
            Amount = 30m,
            Status = PaymentStatus.PAID,
            CreatedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();
        return new SeededData(projectId, orderId, paymentId, paymentCode, customerId, salesId);
    }

    private static Account CreateAccount(Guid accountId, Guid roleId, string email)
    {
        return new Account
        {
            AccountId = accountId,
            RoleId = roleId,
            Email = email,
            PasswordHash = "hash",
            FullName = "Test User",
            Phone = "0900000001",
            Status = AccountStatus.ACTIVE
        };
    }

    private sealed record SeededData(
        Guid ProjectId,
        Guid OrderId,
        Guid PaymentId,
        string PaymentCode,
        Guid CustomerId,
        Guid SalesId);
}
