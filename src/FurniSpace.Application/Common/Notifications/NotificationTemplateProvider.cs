namespace FurniSpace.Application.Common.Notifications;

public static class NotificationTemplateProvider
{
    public static NotificationTemplate Get(NotificationType type)
    {
        return type switch
        {
            NotificationType.ProjectRequestSubmitted => new NotificationTemplate(
                "New project request",
                "Customer {CustomerName} submitted a new project request \"{ProjectName}\".",
                NotificationDeliveryLevel.InAppRealtime,
                "project.request.submitted"),

            NotificationType.ProjectRequestAccepted => new NotificationTemplate(
                "Project request accepted",
                "Your project request \"{ProjectName}\" has been accepted by FurniSpace.",
                NotificationDeliveryLevel.InAppRealtime,
                "project.request.accepted"),

            NotificationType.ProjectMoreInformationRequested => new NotificationTemplate(
                "More information required",
                "FurniSpace needs more information for your project \"{ProjectName}\". Please check the request details and update the required information.",
                NotificationDeliveryLevel.InAppRealtime),

            NotificationType.ProjectBasicInformationUpdated => new NotificationTemplate(
                "Project information updated",
                "Customer has updated basic information for project \"{ProjectName}\".",
                NotificationDeliveryLevel.InAppRealtime),

            NotificationType.ProjectRequestRejected => new NotificationTemplate(
                "Project request rejected",
                "Your project request \"{ProjectName}\" was rejected. Reason: {Reason}",
                NotificationDeliveryLevel.InAppRealtime),

            NotificationType.ProjectDesignerAssigned => new NotificationTemplate(
                "Project assigned to you",
                "You have been assigned as Designer for project \"{ProjectName}\".",
                NotificationDeliveryLevel.InAppRealtime),

            NotificationType.ProjectFileUploaded => new NotificationTemplate(
                "Project file uploaded",
                "A new file has been uploaded to project \"{ProjectName}\".",
                NotificationDeliveryLevel.InAppRealtime),

            NotificationType.ProjectScheduleCreated => new NotificationTemplate(
                "New project schedule created",
                "A {ScheduleType} schedule has been created for project \"{ProjectName}\" at {ScheduledStart}.",
                NotificationDeliveryLevel.InAppRealtime),

            NotificationType.ProjectScheduleUpdated => new NotificationTemplate(
                "Project schedule updated",
                "The {ScheduleType} schedule for project \"{ProjectName}\" has been updated.",
                NotificationDeliveryLevel.InAppRealtime),

            NotificationType.ProjectScheduleConfirmed => new NotificationTemplate(
                "Project schedule confirmed",
                "The {ScheduleType} schedule for project \"{ProjectName}\" has been confirmed.",
                NotificationDeliveryLevel.InAppRealtime),

            NotificationType.ProjectScheduleCompleted => new NotificationTemplate(
                "Project schedule completed",
                "The {ScheduleType} schedule for project \"{ProjectName}\" has been completed.",
                NotificationDeliveryLevel.RealtimeOnly,
                "project_schedule.completed"),

            NotificationType.ProjectScheduleCancelled => new NotificationTemplate(
                "Project schedule cancelled",
                "The {ScheduleType} schedule for project \"{ProjectName}\" has been cancelled.",
                NotificationDeliveryLevel.InAppRealtime),

            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown notification type.")
        };
    }

    public static string RenderTitle(NotificationTemplate template, IReadOnlyDictionary<string, string> parameters)
    {
        return Render(template.TitleTemplate, parameters);
    }

    public static string RenderMessage(NotificationTemplate template, IReadOnlyDictionary<string, string> parameters)
    {
        return Render(template.MessageTemplate, parameters);
    }

    private static string Render(string text, IReadOnlyDictionary<string, string> parameters)
    {
        return parameters.Aggregate(
            text,
            (current, kvp) => current.Replace($"{{{kvp.Key}}}", kvp.Value, StringComparison.Ordinal));
    }
}
