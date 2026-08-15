#nullable enable

using System.Collections.Generic;
using FurniSpace.Application.Common.Notifications;
using Xunit;

namespace FurniSpace.Application.Tests.Notifications;

public sealed class NotificationTemplateProviderTests
{
    [Fact]
    public void Get_ProjectRequestAccepted_ReturnsWorkflowEventName()
    {
        var template = NotificationTemplateProvider.Get(NotificationType.ProjectRequestAccepted);

        Assert.Equal(NotificationDeliveryLevel.InAppRealtime, template.DeliveryLevel);
        Assert.Equal("project.request.accepted", template.SignalREventName);
        Assert.Equal("Project request accepted", template.TitleTemplate);
    }

    [Fact]
    public void Get_ProjectMoreInformationRequested_ReturnsWorkflowEventName()
    {
        var template = NotificationTemplateProvider.Get(NotificationType.ProjectMoreInformationRequested);

        Assert.Equal(NotificationDeliveryLevel.InAppRealtime, template.DeliveryLevel);
        Assert.Equal("project.more_information.requested", template.SignalREventName);
        Assert.Equal("More information required", template.TitleTemplate);
    }

    [Fact]
    public void Get_ProjectBasicInformationUpdated_ReturnsWorkflowEventName()
    {
        var template = NotificationTemplateProvider.Get(NotificationType.ProjectBasicInformationUpdated);

        Assert.Equal(NotificationDeliveryLevel.InAppRealtime, template.DeliveryLevel);
        Assert.Equal("project.basic_information.updated", template.SignalREventName);
        Assert.Equal("Project information updated", template.TitleTemplate);
    }

    [Fact]
    public void Get_ProjectStatusChanged_ReturnsRealtimeOnlyEventName()
    {
        var template = NotificationTemplateProvider.Get(NotificationType.ProjectStatusChanged);

        Assert.Equal(NotificationDeliveryLevel.RealtimeOnly, template.DeliveryLevel);
        Assert.Equal("project.status.changed", template.SignalREventName);
        Assert.Equal("Project status changed", template.TitleTemplate);
    }

    [Fact]
    public void Get_ProjectDesignerAssigned_ReturnsWorkflowEventName()
    {
        var template = NotificationTemplateProvider.Get(NotificationType.ProjectDesignerAssigned);

        Assert.Equal(NotificationDeliveryLevel.InAppRealtime, template.DeliveryLevel);
        Assert.Equal("project.designer.assigned", template.SignalREventName);
        Assert.Equal("You have been assigned to a project", template.TitleTemplate);
    }

    [Fact]
    public void Get_ProposalFinalSelected_ReturnsWorkflowEventName()
    {
        var template = NotificationTemplateProvider.Get(NotificationType.ProposalFinalSelected);

        Assert.Equal(NotificationDeliveryLevel.InAppRealtime, template.DeliveryLevel);
        Assert.Equal("proposal.selected", template.SignalREventName);
        Assert.Equal("Final proposal selected", template.TitleTemplate);
    }

    [Fact]
    public void Get_ProposalPublished_ReturnsWorkflowEventName()
    {
        var template = NotificationTemplateProvider.Get(NotificationType.ProposalPublished);

        Assert.Equal(NotificationDeliveryLevel.InAppRealtime, template.DeliveryLevel);
        Assert.Equal("proposal.published", template.SignalREventName);
        Assert.Equal("New proposal is available", template.TitleTemplate);
        Assert.Equal(
            "A new proposal has been published for your project. Please review it.",
            template.MessageTemplate);
    }

    [Fact]
    public void Get_ProposalRevisionRequested_ReturnsWorkflowEventName()
    {
        var template = NotificationTemplateProvider.Get(NotificationType.ProposalRevisionRequested);

        Assert.Equal(NotificationDeliveryLevel.InAppRealtime, template.DeliveryLevel);
        Assert.Equal("proposal.revision.requested", template.SignalREventName);
        Assert.Equal("Proposal revision requested", template.TitleTemplate);
    }

    [Fact]
    public void Get_QuotationRevisionRequested_ReturnsWorkflowEventName()
    {
        var template = NotificationTemplateProvider.Get(NotificationType.QuotationRevisionRequested);

        Assert.Equal(NotificationDeliveryLevel.InAppRealtime, template.DeliveryLevel);
        Assert.Equal("quotation.revision_requested", template.SignalREventName);
        Assert.Equal("Quotation revision requested", template.TitleTemplate);
    }

    [Fact]
    public void Get_QuotationRevised_ReturnsWorkflowEventName()
    {
        var template = NotificationTemplateProvider.Get(NotificationType.QuotationRevised);

        Assert.Equal(NotificationDeliveryLevel.InAppRealtime, template.DeliveryLevel);
        Assert.Equal("quotation.revised", template.SignalREventName);
        Assert.Equal("Quotation revised", template.TitleTemplate);
    }

    [Fact]
    public void Get_QuotationRejected_ReturnsWorkflowEventName()
    {
        var template = NotificationTemplateProvider.Get(NotificationType.QuotationRejected);

        Assert.Equal(NotificationDeliveryLevel.InAppRealtime, template.DeliveryLevel);
        Assert.Equal("quotation.rejected", template.SignalREventName);
        Assert.Equal("Quotation rejected", template.TitleTemplate);
    }

    [Fact]
    public void Get_ProductionRequestAssigned_ReturnsWorkflowEventName()
    {
        var template = NotificationTemplateProvider.Get(NotificationType.ProductionRequestAssigned);

        Assert.Equal(NotificationDeliveryLevel.InAppRealtime, template.DeliveryLevel);
        Assert.Equal("production.request.assigned", template.SignalREventName);
        Assert.Equal("Production request assigned", template.TitleTemplate);
    }

    [Fact]
    public void Get_PaymentPaid_ReturnsPaymentUpdatedEventName()
    {
        var template = NotificationTemplateProvider.Get(NotificationType.PaymentPaid);

        Assert.Equal(NotificationDeliveryLevel.InAppRealtime, template.DeliveryLevel);
        Assert.Equal("payment.updated", template.SignalREventName);
    }

    [Fact]
    public void Get_ProjectScheduleCreated_ReturnsRealtimeOnlyEventName()
    {
        var template = NotificationTemplateProvider.Get(NotificationType.ProjectScheduleCreated);

        Assert.Equal(NotificationDeliveryLevel.RealtimeOnly, template.DeliveryLevel);
        Assert.Equal("project_schedule.created", template.SignalREventName);
    }

    [Fact]
    public void Get_ProductionRequestCreated_ReturnsWorkflowEventName()
    {
        var template = NotificationTemplateProvider.Get(NotificationType.ProductionRequestCreated);

        Assert.Equal("production.request.created", template.SignalREventName);
    }

    [Fact]
    public void Get_ProductionRequestCompleted_ReturnsWorkflowEventName()
    {
        var template = NotificationTemplateProvider.Get(NotificationType.ProductionRequestCompleted);

        Assert.Equal("production.request.completed", template.SignalREventName);
    }

    [Fact]
    public void Get_OrderUpdated_ReturnsWorkflowEventName()
    {
        var template = NotificationTemplateProvider.Get(NotificationType.OrderUpdated);

        Assert.Equal("order.updated", template.SignalREventName);
    }

    [Fact]
    public void Get_ProjectChatMessageSent_ReturnsWorkflowEventName()
    {
        var template = NotificationTemplateProvider.Get(NotificationType.ProjectChatMessageSent);

        Assert.Equal(NotificationDeliveryLevel.InAppRealtime, template.DeliveryLevel);
        Assert.Equal("project_chat.message_sent", template.SignalREventName);
        Assert.Equal("New chat message", template.TitleTemplate);
    }

    [Fact]
    public void Get_FeCatalogEvents_MatchFrontendContract()
    {
        var catalog = new Dictionary<NotificationType, string>
        {
            [NotificationType.ProjectRequestSubmitted] = "project.request.submitted",
            [NotificationType.ProjectRequestAccepted] = "project.request.accepted",
            [NotificationType.ProjectMoreInformationRequested] = "project.more_information.requested",
            [NotificationType.ProjectBasicInformationUpdated] = "project.basic_information.updated",
            [NotificationType.ProjectDesignerAssigned] = "project.designer.assigned",
            [NotificationType.ProposalPublished] = "proposal.published",
            [NotificationType.ProposalFinalSelected] = "proposal.selected",
            [NotificationType.QuotationSent] = "quotation.sent",
            [NotificationType.QuotationRevised] = "quotation.revised",
            [NotificationType.QuotationRevisionRequested] = "quotation.revision_requested",
            [NotificationType.QuotationRejected] = "quotation.rejected",
            [NotificationType.QuotationAccepted] = "quotation.accepted",
            [NotificationType.PaymentCreated] = "payment.created",
            [NotificationType.PaymentPaid] = "payment.updated",
            [NotificationType.OrderUpdated] = "order.updated",
            [NotificationType.OrderDelivered] = "order.delivered",
            [NotificationType.OrderCompleted] = "order.completed",
            [NotificationType.ProductionRequestCreated] = "production.request.created",
            [NotificationType.ProductionRequestAssigned] = "production.request.assigned",
            [NotificationType.ProductionRequestCompleted] = "production.request.completed",
            [NotificationType.ProjectStatusChanged] = "project.status.changed",
            [NotificationType.ProjectScheduleCreated] = "project_schedule.created",
            [NotificationType.ProjectScheduleUpdated] = "project_schedule.updated",
            [NotificationType.ProjectScheduleConfirmed] = "project_schedule.confirmed",
            [NotificationType.ProjectScheduleCompleted] = "project_schedule.completed",
            [NotificationType.OrderItemDeliveryUpdated] = "order.item.delivery_updated",
            [NotificationType.OrderItemDeliveryConfirmed] = "order.item.delivery_confirmed",
            [NotificationType.ProjectChatMessageSent] = "project_chat.message_sent"
        };

        foreach (var (type, expectedEvent) in catalog)
        {
            Assert.Equal(expectedEvent, NotificationTemplateProvider.Get(type).SignalREventName);
        }
    }
}
