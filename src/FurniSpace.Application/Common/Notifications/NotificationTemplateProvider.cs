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
                NotificationDeliveryLevel.InAppRealtime,
                "project.more_information.requested"),

            NotificationType.ProjectBasicInformationUpdated => new NotificationTemplate(
                "Project information updated",
                "Customer has updated basic information for project \"{ProjectName}\".",
                NotificationDeliveryLevel.InAppRealtime,
                "project.basic_information.updated"),

            NotificationType.ProjectStatusChanged => new NotificationTemplate(
                "Project status changed",
                "Project \"{ProjectName}\" status changed to {Status}.",
                NotificationDeliveryLevel.RealtimeOnly,
                "project.status.changed"),

            NotificationType.ProjectRequestRejected => new NotificationTemplate(
                "Project request rejected",
                "Your project request \"{ProjectName}\" was rejected. Reason: {Reason}",
                NotificationDeliveryLevel.InAppRealtime),

            NotificationType.ProjectDesignerAssigned => new NotificationTemplate(
                "You have been assigned to a project",
                "You have been assigned as Designer for project \"{ProjectName}\".",
                NotificationDeliveryLevel.InAppRealtime,
                "project.designer.assigned"),

            NotificationType.ProposalPublished => new NotificationTemplate(
                "New proposal ready for review",
                "A design proposal \"{ProposalName}\" is ready for your review.",
                NotificationDeliveryLevel.InAppRealtime,
                "proposal.published"),

            NotificationType.ProposalRevisionRequested => new NotificationTemplate(
                "Proposal revision requested",
                "Customer requested revisions for proposal \"{ProposalName}\".",
                NotificationDeliveryLevel.InAppRealtime,
                "proposal.revision.requested"),

            NotificationType.ProposalFinalSelected => new NotificationTemplate(
                "Final proposal selected",
                "Customer selected proposal \"{ProposalName}\" as the final design proposal.",
                NotificationDeliveryLevel.InAppRealtime,
                "proposal.final.selected"),

            NotificationType.QuotationSent => new NotificationTemplate(
                "Quotation ready for review",
                "Quotation \"{QuotationCode}\" is ready for your review.",
                NotificationDeliveryLevel.InAppRealtime,
                "quotation.sent"),

            NotificationType.QuotationAccepted => new NotificationTemplate(
                "Quotation accepted",
                "Customer accepted quotation \"{QuotationCode}\".",
                NotificationDeliveryLevel.InAppRealtime,
                "quotation.accepted"),

            NotificationType.CustomizationRequestSubmitted => new NotificationTemplate(
                "Customization request submitted",
                "Customer submitted customization request \"{RequestTitle}\" for proposal \"{ProposalName}\".",
                NotificationDeliveryLevel.InAppRealtime,
                "customization_request.submitted"),

            NotificationType.CustomizationDesignerReviewed => new NotificationTemplate(
                "Customization request ready for production review",
                "Designer reviewed customization request \"{RequestTitle}\" for project \"{ProjectName}\".",
                NotificationDeliveryLevel.InAppRealtime,
                "customization_request.designer_reviewed"),

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
