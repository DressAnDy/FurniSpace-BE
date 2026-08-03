using System.Net;
using System.Net.Http.Json;
using FurniSpace.API.IntegrationTests.Fixtures;
using FurniSpace.API.IntegrationTests.Support;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Payments;
using FurniSpace.Application.DTOs.Projects;
using FurniSpace.Domain.Enums;
using FurniSpace.Testing.Seeding;
using Microsoft.EntityFrameworkCore;

namespace FurniSpace.API.IntegrationTests.Projects;

/// <summary>
/// Suite A — Project Intake (docs/FurniSpace_System_Flow_Integration_Test_Context_Updated.md §28).
/// Covers submit → receive → info request → update → approve → start fee → designer assignment.
/// </summary>
[Collection(ApiIntegrationCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "Core")]
public sealed class ProjectIntakeApiIntegrationTests : IAsyncLifetime
{
    private readonly ApiIntegrationFixture _fixture;

    public ProjectIntakeApiIntegrationTests(ApiIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.Database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task IntakeHappyPath_FromSubmitThroughDesignerAssignment_PersistsConsistentState()
    {
        SeededAccount customer;
        SeededAccount sales;
        SeededAccount designer;
        await using (var context = _fixture.Database.CreateDbContext())
        {
            customer = await CoreAccountSeeder.SeedAccountAsync(
                context,
                CoreRoles.Customer,
                "intake-customer@integration.test");
            sales = await CoreAccountSeeder.SeedAccountAsync(
                context,
                CoreRoles.Sales,
                "intake-sales@integration.test");
            designer = await CoreAccountSeeder.SeedAccountAsync(
                context,
                CoreRoles.Designer,
                "intake-designer@integration.test");
        }

        using var createRequest = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Post,
            "/projects",
            customer.AccountId,
            CoreRoles.Customer,
            new CreateProjectRequestDto
            {
                ProjectName = "Retail Flagship",
                BusinessType = "Retail",
                FurnitureRequirement = "Shelving and counters",
                BudgetMin = 50_000_000,
                BudgetMax = 120_000_000
            });
        var createResponse = await _fixture.Client.SendAsync(createRequest);
        var created = await createResponse.Content
            .ReadFromJsonAsync<ServiceResult<ProjectDto>>(IntegrationHttp.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(created?.Data);
        var projectId = created.Data.ProjectId;

        using var assignSalesRequest = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Patch,
            $"/projects/{projectId}/sales-assignment",
            sales.AccountId,
            CoreRoles.Sales,
            new AssignProjectSalesRequestDto { Note = "Taking ownership" });
        var assignSalesResponse = await _fixture.Client.SendAsync(assignSalesRequest);
        var assignedSales = await assignSalesResponse.Content
            .ReadFromJsonAsync<ServiceResult<ProjectSalesAssignmentDto>>(IntegrationHttp.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, assignSalesResponse.StatusCode);
        Assert.Equal(ProjectStatus.IN_CONSULTATION, assignedSales?.Data?.Status);
        Assert.Equal(sales.AccountId, assignedSales?.Data?.AssignedSalesId);
        Assert.NotNull(assignedSales?.Data?.SalesChat);

        using var requestInfo = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Post,
            $"/projects/{projectId}/information-requests",
            sales.AccountId,
            CoreRoles.Sales,
            new RequestProjectInformationRequestDto { Message = "Need store layout sketch" });
        var requestInfoResponse = await _fixture.Client.SendAsync(requestInfo);
        Assert.Equal(HttpStatusCode.OK, requestInfoResponse.StatusCode);

        using var updateInfo = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Patch,
            $"/projects/{projectId}/basic-information",
            customer.AccountId,
            CoreRoles.Customer,
            new UpdateProjectBasicInformationRequestDto
            {
                ProjectName = "Retail Flagship",
                BusinessType = "Retail",
                FurnitureRequirement = "Shelving, counters, and seating",
                ProjectAddress = "12 Nguyen Hue",
                BudgetMin = 50_000_000,
                BudgetMax = 120_000_000
            });
        var updateInfoResponse = await _fixture.Client.SendAsync(updateInfo);
        Assert.Equal(HttpStatusCode.OK, updateInfoResponse.StatusCode);

        using var reAccept = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Patch,
            $"/projects/{projectId}/sales-assignment",
            sales.AccountId,
            CoreRoles.Sales,
            new AssignProjectSalesRequestDto());
        var reAcceptResponse = await _fixture.Client.SendAsync(reAccept);
        Assert.Equal(HttpStatusCode.OK, reAcceptResponse.StatusCode);

        using var createFee = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Post,
            $"/api/projects/{projectId}/payments/project-start-fee",
            sales.AccountId,
            CoreRoles.Sales,
            new CreateProjectStartFeePaymentRequestDto { Note = "Start fee" });
        var createFeeResponse = await _fixture.Client.SendAsync(createFee);
        var fee = await createFeeResponse.Content
            .ReadFromJsonAsync<ServiceResult<PaymentDetailDto>>(IntegrationHttp.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, createFeeResponse.StatusCode);
        Assert.NotNull(fee?.Data);
        Assert.Equal(PaymentType.PROJECT_START_FEE, fee.Data.PaymentType);
        Assert.Equal(PaymentStatus.PENDING, fee.Data.Status);

        await using (var context = _fixture.Database.CreateDbContext())
        {
            var payment = await context.PaymentSet.SingleAsync(p => p.PaymentId == fee.Data.PaymentId);
            payment.Status = PaymentStatus.PAID;
            payment.PaidAt = CoreAccountSeeder.FixedTimestamp;
            payment.UpdatedAt = CoreAccountSeeder.FixedTimestamp;
            await context.SaveChangesAsync();
        }

        using var approve = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Patch,
            $"/projects/{projectId}/status",
            sales.AccountId,
            CoreRoles.Sales,
            new UpdateProjectStatusRequestDto
            {
                Status = ProjectStatus.WAITING_FOR_DESIGNER_ASSIGNMENT,
                Note = "Ready for designer"
            });
        var approveResponse = await _fixture.Client.SendAsync(approve);
        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);

        using var assignDesigner = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Patch,
            $"/projects/{projectId}/designer-assignment",
            sales.AccountId,
            CoreRoles.Sales,
            new AssignProjectDesignerRequestDto
            {
                DesignerId = designer.AccountId,
                SpaceDataStatus = ProjectSpaceDataStatus.SUFFICIENT,
                Note = "Space verified from photos"
            });
        var assignDesignerResponse = await _fixture.Client.SendAsync(assignDesigner);
        var assignedDesigner = await assignDesignerResponse.Content
            .ReadFromJsonAsync<ServiceResult<ProjectDesignerAssignmentDto>>(IntegrationHttp.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, assignDesignerResponse.StatusCode);
        Assert.Equal(ProjectStatus.SPACE_VERIFIED, assignedDesigner?.Data?.Status);
        Assert.Equal(designer.AccountId, assignedDesigner?.Data?.AssignedDesigner.AccountId);

        await using var verification = _fixture.Database.CreateDbContext();
        var project = await verification.ProjectSet.SingleAsync(p => p.ProjectId == projectId);
        Assert.Equal(ProjectStatus.SPACE_VERIFIED, project.Status);
        Assert.Equal(sales.AccountId, project.AssignedSalesId);
        Assert.Equal(designer.AccountId, project.AssignedDesignerId);
        Assert.Equal("Shelving, counters, and seating", project.FurnitureRequirement);
        Assert.Equal("12 Nguyen Hue", project.ProjectAddress);

        Assert.Equal(2, await verification.ProjectChatSet.CountAsync(c => c.ProjectId == projectId));
        Assert.Equal(
            1,
            await verification.PaymentSet.CountAsync(p =>
                p.ProjectId == projectId &&
                p.PaymentType == PaymentType.PROJECT_START_FEE &&
                p.Status == PaymentStatus.PAID));
    }

    [Fact]
    public async Task AssignSales_FromSubmitted_MovesToInConsultationAndCreatesSalesChat()
    {
        ProjectSubmittedScenario scenario;
        await using (var context = _fixture.Database.CreateDbContext())
        {
            scenario = await ProjectScenarioSeeder.SeedSubmittedAsync(context);
        }

        using var request = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Patch,
            $"/projects/{scenario.ProjectId}/sales-assignment",
            scenario.SalesAccountId,
            CoreRoles.Sales,
            new AssignProjectSalesRequestDto { Note = "Accepted" });

        var response = await _fixture.Client.SendAsync(request);
        var result = await response.Content
            .ReadFromJsonAsync<ServiceResult<ProjectSalesAssignmentDto>>(IntegrationHttp.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(ProjectStatus.IN_CONSULTATION, result?.Data?.Status);
        Assert.Equal(scenario.SalesAccountId, result?.Data?.AssignedSalesId);
        Assert.NotNull(result?.Data?.SalesChat);

        await using var verification = _fixture.Database.CreateDbContext();
        var project = await verification.ProjectSet.SingleAsync();
        Assert.Equal(ProjectStatus.IN_CONSULTATION, project.Status);
        Assert.Equal(scenario.SalesAccountId, project.AssignedSalesId);
        Assert.NotNull(project.SalesAssignedAt);

        var chat = await verification.ProjectChatSet.SingleAsync();
        Assert.Equal(ProjectChatType.SALES, chat.ChatType);
        Assert.Equal(scenario.SalesAccountId, chat.StaffId);
        Assert.Equal(ProjectChatStatus.OPEN, chat.Status);
    }

    [Fact]
    public async Task AssignSales_AsCustomer_ReturnsForbidden()
    {
        ProjectSubmittedScenario scenario;
        await using (var context = _fixture.Database.CreateDbContext())
        {
            scenario = await ProjectScenarioSeeder.SeedSubmittedAsync(context);
        }

        using var request = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Patch,
            $"/projects/{scenario.ProjectId}/sales-assignment",
            scenario.CustomerAccountId,
            CoreRoles.Customer,
            new AssignProjectSalesRequestDto());

        var response = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await using var verification = _fixture.Database.CreateDbContext();
        var project = await verification.ProjectSet.SingleAsync();
        Assert.Equal(ProjectStatus.SUBMITTED, project.Status);
        Assert.Null(project.AssignedSalesId);
    }

    [Fact]
    public async Task RequestInformation_AsAssignedSales_MovesToNeedBasicInformation()
    {
        ProjectConsultationScenario scenario;
        await using (var context = _fixture.Database.CreateDbContext())
        {
            scenario = await ProjectScenarioSeeder.SeedInConsultationAsync(context);
        }

        using var request = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Post,
            $"/projects/{scenario.ProjectId}/information-requests",
            scenario.SalesAccountId,
            CoreRoles.Sales,
            new RequestProjectInformationRequestDto { Message = "Please add floor plan" });

        var response = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var verification = _fixture.Database.CreateDbContext();
        var project = await verification.ProjectSet.SingleAsync();
        Assert.Equal(ProjectStatus.NEED_BASIC_INFORMATION, project.Status);
    }

    [Fact]
    public async Task UpdateBasicInformation_AsOwnerCustomer_PersistsFields()
    {
        ProjectConsultationScenario scenario;
        await using (var context = _fixture.Database.CreateDbContext())
        {
            scenario = await ProjectScenarioSeeder.SeedNeedBasicInformationAsync(context);
        }

        using var request = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Patch,
            $"/projects/{scenario.ProjectId}/basic-information",
            scenario.CustomerAccountId,
            CoreRoles.Customer,
            new UpdateProjectBasicInformationRequestDto
            {
                ProjectName = "Updated Name",
                BusinessType = "Cafe",
                FurnitureRequirement = "Tables and chairs",
                ProjectAddress = "1 Le Loi"
            });

        var response = await _fixture.Client.SendAsync(request);
        var result = await response.Content
            .ReadFromJsonAsync<ServiceResult<ProjectBasicInformationDto>>(IntegrationHttp.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Updated Name", result?.Data?.ProjectName);

        await using var verification = _fixture.Database.CreateDbContext();
        var project = await verification.ProjectSet.SingleAsync();
        Assert.Equal("Updated Name", project.ProjectName);
        Assert.Equal("Cafe", project.BusinessType);
        Assert.Equal("1 Le Loi", project.ProjectAddress);
        Assert.Equal(ProjectStatus.NEED_BASIC_INFORMATION, project.Status);
    }

    [Fact]
    public async Task Reject_AsAssignedSales_SetsRejectedStatusAndReason()
    {
        ProjectConsultationScenario scenario;
        await using (var context = _fixture.Database.CreateDbContext())
        {
            scenario = await ProjectScenarioSeeder.SeedInConsultationAsync(context);
        }

        using var request = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Patch,
            $"/projects/{scenario.ProjectId}/rejection",
            scenario.SalesAccountId,
            CoreRoles.Sales,
            new RejectProjectRequestDto { RejectionReason = "Out of service area" });

        var response = await _fixture.Client.SendAsync(request);
        var result = await response.Content
            .ReadFromJsonAsync<ServiceResult<ProjectRejectionDto>>(IntegrationHttp.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(ProjectStatus.REJECTED, result?.Data?.Status);
        Assert.Equal("Out of service area", result?.Data?.RejectionReason);

        await using var verification = _fixture.Database.CreateDbContext();
        var project = await verification.ProjectSet.SingleAsync();
        Assert.Equal(ProjectStatus.REJECTED, project.Status);
        Assert.Equal("Out of service area", project.RejectionReason);
        Assert.NotNull(project.RejectedAt);
    }

    [Fact]
    public async Task AssignDesigner_WithoutPaidStartFee_ReturnsBadRequest()
    {
        ProjectDesignerReadyScenario scenario;
        await using (var context = _fixture.Database.CreateDbContext())
        {
            scenario = await ProjectScenarioSeeder.SeedWaitingForDesignerAsync(
                context,
                includePaidStartFee: false);
        }

        using var request = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Patch,
            $"/projects/{scenario.ProjectId}/designer-assignment",
            scenario.SalesAccountId,
            CoreRoles.Sales,
            new AssignProjectDesignerRequestDto
            {
                DesignerId = scenario.DesignerAccountId,
                SpaceDataStatus = ProjectSpaceDataStatus.SUFFICIENT
            });

        var response = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var verification = _fixture.Database.CreateDbContext();
        var project = await verification.ProjectSet.SingleAsync();
        Assert.Null(project.AssignedDesignerId);
        Assert.Equal(ProjectStatus.WAITING_FOR_DESIGNER_ASSIGNMENT, project.Status);
    }

    [Fact]
    public async Task AssignDesigner_WithPaidStartFee_MovesToSpaceVerifiedAndCreatesDesignerChat()
    {
        ProjectDesignerReadyScenario scenario;
        await using (var context = _fixture.Database.CreateDbContext())
        {
            scenario = await ProjectScenarioSeeder.SeedWaitingForDesignerAsync(context);
        }

        using var request = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Patch,
            $"/projects/{scenario.ProjectId}/designer-assignment",
            scenario.SalesAccountId,
            CoreRoles.Sales,
            new AssignProjectDesignerRequestDto
            {
                DesignerId = scenario.DesignerAccountId,
                SpaceDataStatus = ProjectSpaceDataStatus.SUFFICIENT
            });

        var response = await _fixture.Client.SendAsync(request);
        var result = await response.Content
            .ReadFromJsonAsync<ServiceResult<ProjectDesignerAssignmentDto>>(IntegrationHttp.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(ProjectStatus.SPACE_VERIFIED, result?.Data?.Status);
        Assert.Equal(scenario.DesignerAccountId, result?.Data?.AssignedDesigner.AccountId);

        await using var verification = _fixture.Database.CreateDbContext();
        var project = await verification.ProjectSet.SingleAsync();
        Assert.Equal(ProjectStatus.SPACE_VERIFIED, project.Status);
        Assert.Equal(scenario.DesignerAccountId, project.AssignedDesignerId);
        Assert.NotNull(project.DesignerAssignedAt);

        var chat = await verification.ProjectChatSet.SingleAsync();
        Assert.Equal(ProjectChatType.DESIGNER, chat.ChatType);
        Assert.Equal(scenario.DesignerAccountId, chat.StaffId);
    }

    [Fact]
    public async Task AssignDesigner_WithInsufficientSpaceData_MovesToMeasurementRequired()
    {
        ProjectDesignerReadyScenario scenario;
        await using (var context = _fixture.Database.CreateDbContext())
        {
            scenario = await ProjectScenarioSeeder.SeedWaitingForDesignerAsync(context);
        }

        using var request = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Patch,
            $"/projects/{scenario.ProjectId}/designer-assignment",
            scenario.SalesAccountId,
            CoreRoles.Sales,
            new AssignProjectDesignerRequestDto
            {
                DesignerId = scenario.DesignerAccountId,
                SpaceDataStatus = ProjectSpaceDataStatus.INSUFFICIENT
            });

        var response = await _fixture.Client.SendAsync(request);
        var result = await response.Content
            .ReadFromJsonAsync<ServiceResult<ProjectDesignerAssignmentDto>>(IntegrationHttp.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(ProjectStatus.MEASUREMENT_REQUIRED, result?.Data?.Status);

        await using var verification = _fixture.Database.CreateDbContext();
        var project = await verification.ProjectSet.SingleAsync();
        Assert.Equal(ProjectStatus.MEASUREMENT_REQUIRED, project.Status);
    }

    [Fact]
    public async Task UpdateStatus_SkippingMandatoryPhase_ReturnsBadRequest()
    {
        ProjectConsultationScenario scenario;
        await using (var context = _fixture.Database.CreateDbContext())
        {
            scenario = await ProjectScenarioSeeder.SeedInConsultationAsync(context);
        }

        using var request = IntegrationHttp.AuthenticatedJson(
            HttpMethod.Patch,
            $"/projects/{scenario.ProjectId}/status",
            scenario.SalesAccountId,
            CoreRoles.Sales,
            new UpdateProjectStatusRequestDto
            {
                Status = ProjectStatus.PROPOSAL_CONSULTING
            });

        var response = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var verification = _fixture.Database.CreateDbContext();
        var project = await verification.ProjectSet.SingleAsync();
        Assert.Equal(ProjectStatus.IN_CONSULTATION, project.Status);
    }
}
