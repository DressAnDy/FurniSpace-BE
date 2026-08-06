using System;
using FurniSpace.Application.Common.Realtime;
using Xunit;

namespace FurniSpace.Application.Tests.Payments;

public sealed class PaymentRealtimeConstantsTests
{
    [Fact]
    public void Payment_GroupName_UsesStableFormat()
    {
        var paymentId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        Assert.Equal("payment:11111111-1111-1111-1111-111111111111", PaymentRealtimeConstants.Payment(paymentId));
        Assert.Equal("payment.updated", PaymentRealtimeConstants.PaymentUpdatedEvent);
        Assert.Equal("/hubs/payments", PaymentRealtimeConstants.HubPath);
    }
}
