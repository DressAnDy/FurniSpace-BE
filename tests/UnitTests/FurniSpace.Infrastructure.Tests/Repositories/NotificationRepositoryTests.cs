#nullable enable

using System;
using System.Threading.Tasks;
using FurniSpace.Domain.Entities;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.Repositories.Repository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FurniSpace.Infrastructure.Tests.Repositories;

public sealed class NotificationRepositoryTests
{
    [Fact]
    public async Task ExistsActiveDuplicateAsync_WhenMatchingActiveRowExists_ReturnsTrue()
    {
        await using var context = CreateContext();
        var receiverId = Guid.NewGuid();
        var referenceId = Guid.NewGuid();
        context.NotificationSet.Add(new Notification
        {
            NotificationId = Guid.NewGuid(),
            ReceiverId = receiverId,
            Title = "Payment completed",
            NotificationType = "PaymentPaid",
            ReferenceType = "PAYMENT",
            ReferenceId = referenceId,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        var repository = new NotificationRepository(context);

        var exists = await repository.ExistsActiveDuplicateAsync(
            receiverId,
            "PaymentPaid",
            "PAYMENT",
            referenceId);

        Assert.True(exists);
    }

    [Fact]
    public async Task ExistsActiveDuplicateAsync_WhenDeletedOrDifferentKey_ReturnsFalse()
    {
        await using var context = CreateContext();
        var receiverId = Guid.NewGuid();
        var referenceId = Guid.NewGuid();
        context.NotificationSet.AddRange(
            new Notification
            {
                NotificationId = Guid.NewGuid(),
                ReceiverId = receiverId,
                Title = "Deleted",
                NotificationType = "PaymentPaid",
                ReferenceType = "PAYMENT",
                ReferenceId = referenceId,
                DeletedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            },
            new Notification
            {
                NotificationId = Guid.NewGuid(),
                ReceiverId = receiverId,
                Title = "Other type",
                NotificationType = "PaymentCreated",
                ReferenceType = "PAYMENT",
                ReferenceId = referenceId,
                CreatedAt = DateTime.UtcNow
            });
        await context.SaveChangesAsync();
        var repository = new NotificationRepository(context);

        var exists = await repository.ExistsActiveDuplicateAsync(
            receiverId,
            "PaymentPaid",
            "PAYMENT",
            referenceId);

        Assert.False(exists);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }
}
