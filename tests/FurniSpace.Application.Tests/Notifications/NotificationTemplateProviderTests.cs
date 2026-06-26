#nullable enable

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
        Assert.Equal("proposal.final.selected", template.SignalREventName);
        Assert.Equal("Final proposal selected", template.TitleTemplate);
    }
}
