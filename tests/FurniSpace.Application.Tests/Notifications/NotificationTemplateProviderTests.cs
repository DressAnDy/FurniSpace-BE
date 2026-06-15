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
}
