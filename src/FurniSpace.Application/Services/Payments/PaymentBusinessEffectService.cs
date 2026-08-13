using FurniSpace.Application.Common.Notifications;
using FurniSpace.Application.Common.Orders;
using FurniSpace.Application.Common.Payments;
using FurniSpace.Application.Interfaces.Notifications;
using FurniSpace.Application.Interfaces.Payments;
using FurniSpace.Application.Interfaces.Projects;
using static FurniSpace.Application.Constants.Payments.PaymentBusinessEffectServiceConstants;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Microsoft.Extensions.Logging;

namespace FurniSpace.Application.Services.Payments;

public sealed class PaymentBusinessEffectService : IPaymentBusinessEffectService
{
    private readonly IPaymentRepository _payments;
    private readonly IOrderRepository _orders;
    private readonly IProjectRepository _projects;
    private readonly INotificationDispatcher? _notifications;
    private readonly IProjectStakeholderResolver? _stakeholders;
    private readonly ILogger<PaymentBusinessEffectService>? _logger;

    public PaymentBusinessEffectService(
        IPaymentRepository payments,
        IOrderRepository orders,
        IProjectRepository projects,
        INotificationDispatcher? notifications = null,
        IProjectStakeholderResolver? stakeholders = null,
        ILogger<PaymentBusinessEffectService>? logger = null)
    {
        _payments = payments;
        _orders = orders;
        _projects = projects;
        _notifications = notifications;
        _stakeholders = stakeholders;
        _logger = logger;
    }

    public async Task ApplyAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        if (payment.OrderId.HasValue && IsOrderScopedPayment(payment.PaymentType))
        {
            await ApplyOrderCollectionEffectsAsync(payment.OrderId.Value, cancellationToken);
        }

        if (payment.Status != PaymentStatus.PAID)
        {
            return;
        }

        await PaymentNotificationSupport.TryDispatchUpdatedAsync(
            _notifications,
            _stakeholders,
            _logger,
            payment,
            cancellationToken);

        switch (payment.PaymentType)
        {
            case PaymentType.PROJECT_START_FEE:
                await ApplyProjectStartFeePaidAsync(payment, cancellationToken);
                break;
            case PaymentType.DEPOSIT:
                await ApplyDepositPaidAsync(payment, cancellationToken);
                break;
            case PaymentType.REMAINING_PAYMENT:
                await ApplyRemainingPaymentPaidAsync(payment, cancellationToken);
                break;
        }
    }

    private async Task ApplyOrderCollectionEffectsAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await _orders.GetByIdAsync(orderId, cancellationToken);
        if (order is null)
        {
            return;
        }

        var summedPaidAmount = await _payments.SumOrderScopedPaidAmountAsync(orderId, cancellationToken);
        var (paidAmount, remainingAmount) = OrderPaidAmountRecalculator.Calculate(
            order.FinalTotalAmount,
            summedPaidAmount);

        order.PaidAmount = paidAmount;
        order.RemainingAmount = remainingAmount;
        order.UpdatedAt = DateTime.UtcNow;
        _orders.Update(order);
    }

    private async Task ApplyProjectStartFeePaidAsync(Payment payment, CancellationToken cancellationToken)
    {
        var project = await _projects.GetByIdAsync(payment.ProjectId, cancellationToken);
        if (project is null)
        {
            return;
        }

        if (project.AssignedDesignerId is not null ||
            !project.Status.HasValue ||
            !ProjectStartFeeEligibleStatuses.Contains(project.Status.Value))
        {
            return;
        }

        if (project.Status == ProjectStatus.WAITING_FOR_DESIGNER_ASSIGNMENT)
        {
            return;
        }

        project.Status = ProjectStatus.WAITING_FOR_DESIGNER_ASSIGNMENT;
        project.UpdatedAt = DateTime.UtcNow;
        _projects.Update(project);
        await DispatchProjectStartFeePaidNotificationAsync(payment, project, cancellationToken);
    }

    private async Task ApplyDepositPaidAsync(Payment payment, CancellationToken cancellationToken)
    {
        if (!payment.OrderId.HasValue)
        {
            return;
        }

        var order = await _orders.GetByIdAsync(payment.OrderId.Value, cancellationToken);
        if (order is null)
        {
            return;
        }

        if (order.Status != OrderStatus.DEPOSIT_PENDING)
        {
            return;
        }

        order.Status = OrderStatus.DEPOSIT_PAID;
        order.UpdatedAt = DateTime.UtcNow;
        _orders.Update(order);
    }

    private async Task ApplyRemainingPaymentPaidAsync(Payment payment, CancellationToken cancellationToken)
    {
        if (!payment.OrderId.HasValue)
        {
            return;
        }

        var order = await _orders.GetByIdAsync(payment.OrderId.Value, cancellationToken);
        if (order is null || order.Status != OrderStatus.FINAL_PAYMENT_PENDING)
        {
            return;
        }

        // Remaining payment PAID only updates order financial summary via ApplyOrderCollectionEffectsAsync.
        // Order/project completion is explicit in a later workflow step.
        await Task.CompletedTask;
    }

    private async Task DispatchProjectStartFeePaidNotificationAsync(
        Payment payment,
        Project project,
        CancellationToken cancellationToken)
    {
        if (_notifications is null)
        {
            return;
        }

        var receivers = new HashSet<Guid>();
        if (project.CustomerId != Guid.Empty)
        {
            receivers.Add(project.CustomerId);
        }

        if (project.AssignedSalesId.HasValue)
        {
            receivers.Add(project.AssignedSalesId.Value);
        }

        if (receivers.Count == 0)
        {
            return;
        }

        try
        {
            await _notifications.DispatchAsync(
                NotificationType.ProjectStatusChanged,
                new Dictionary<string, string>
                {
                    ["ProjectName"] = project.ProjectName,
                    ["Status"] = project.Status?.ToString() ?? string.Empty,
                    ["Message"] = "Project start fee has been paid. Designer assignment is now allowed."
                },
                receivers,
                projectId: project.ProjectId,
                referenceType: "PROJECT",
                referenceId: project.ProjectId,
                cancellationToken,
                metadata: new Dictionary<string, object?>
                {
                    ["paymentType"] = payment.PaymentType?.ToString(),
                    ["newProjectStatus"] = project.Status?.ToString()
                });
        }
        catch (Exception exception)
        {
            _logger?.LogWarning(
                exception,
                "Failed to dispatch project start fee paid notification for payment {PaymentId}",
                payment.PaymentId);
        }
    }

    private static bool IsOrderScopedPayment(PaymentType? paymentType)
    {
        return paymentType.HasValue &&
            OrderPaidAmountRecalculator.OrderScopedPaymentTypes.Contains(paymentType.Value);
    }
}
