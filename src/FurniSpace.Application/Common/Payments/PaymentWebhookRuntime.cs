using FurniSpace.Application.Interfaces.Notifications;
using FurniSpace.Application.Interfaces.Payments;
using FurniSpace.Application.Interfaces.Projects;

namespace FurniSpace.Application.Common.Payments;

public sealed class PaymentWebhookRuntime
{
    public PaymentWebhookRuntime(
        IPaymentRealtimeService paymentRealtime,
        IPaymentBusinessEffectService paymentBusinessEffects,
        INotificationDispatcher? notifications = null,
        IProjectStakeholderResolver? stakeholders = null)
    {
        PaymentRealtime = paymentRealtime;
        PaymentBusinessEffects = paymentBusinessEffects;
        Notifications = notifications;
        Stakeholders = stakeholders;
    }

    public IPaymentRealtimeService PaymentRealtime { get; }

    public IPaymentBusinessEffectService PaymentBusinessEffects { get; }

    public INotificationDispatcher? Notifications { get; }

    public IProjectStakeholderResolver? Stakeholders { get; }
}
