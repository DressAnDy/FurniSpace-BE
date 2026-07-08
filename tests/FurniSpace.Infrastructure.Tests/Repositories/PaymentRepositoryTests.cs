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
    public async Task SumOrderScopedPaidAmountAsync_SumsPaidAndPartiallyPaidPayments()
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
            PaidAmount = 20m,
            RemainingAmount = 50m,
            Status = PaymentStatus.PARTIALLY_PAID,
            CreatedAt = DateTime.UtcNow.AddMinutes(1)
        });
        await context.SaveChangesAsync();
        var repository = new PaymentRepository(context);

        var sum = await repository.SumOrderScopedPaidAmountAsync(data.OrderId);

        Assert.Equal(50m, sum);
    }

    [Fact]
    public async Task GetListAsync_FiltersByProject()
    {
        await using var context = CreateContext();
        var data = await SeedAsync(context);
        var repository = new PaymentRepository(context);

        var items = await repository.GetListAsync(new PaymentQueryReadModel
        {
            ProjectId = data.ProjectId
        });

        Assert.Single(items);
        Assert.Equal(data.PaymentId, items[0].PaymentId);
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
            OriginalTotalAmount = 100m,
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
            PaidAmount = 30m,
            RemainingAmount = 0m,
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
