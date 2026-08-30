namespace FurniSpace.Application.Constants.Dashboard;

internal static class DashboardActionPaths
{
    internal static string Project(Guid projectId) => $"/projects/{projectId:D}";

    internal static string ProjectQuotations(Guid projectId) => $"/projects/{projectId:D}/quotations";

    internal static string ProjectProposals(Guid projectId) => $"/projects/{projectId:D}/proposals";

    internal static string Order(Guid orderId) => $"/orders/{orderId:D}";

    internal static string ProductionRequest(Guid productionRequestId) =>
        $"/production/requests/{productionRequestId:D}";

    internal static string ProductionCustomizationReview(Guid versionId) =>
        $"/production/customization-reviews?versionId={versionId:D}";

    internal static string ProductionReadyForDelivery(Guid orderId) =>
        $"/production/ready-for-delivery?orderId={orderId:D}";
}
