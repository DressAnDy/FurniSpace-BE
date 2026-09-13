namespace FurniSpace.Application.Common.Realtime;

public static class PaymentRealtimeConstants
{
    public const string HubPath = "/hubs/payments";
    public const string PaymentUpdatedEvent = "payment.updated";

    public static string Payment(Guid paymentId) => $"payment:{paymentId:D}";
}
