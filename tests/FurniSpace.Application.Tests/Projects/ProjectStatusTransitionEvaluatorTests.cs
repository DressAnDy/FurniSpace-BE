#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.Common.Projects;
using FurniSpace.Application.DTOs.Projects;
using FurniSpace.Application.Services.Projects;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using Microsoft.Extensions.Options;
using Xunit;

namespace FurniSpace.Application.Tests.Projects;

public sealed class ProjectStatusTransitionEvaluatorTests
{
    [Fact]
    public async Task EvaluateAsync_Customer_ReturnsForbidden()
    {
        var evaluator = CreateEvaluator();
        var project = CreateProject(status: ProjectStatus.IN_CONSULTATION);

        var error = await evaluator.EvaluateAsync(
            project,
            ProjectStatus.WAITING_FOR_DESIGNER_ASSIGNMENT,
            note: null,
            Guid.NewGuid(),
            "CUSTOMER",
            CancellationToken.None);

        Assert.NotNull(error);
        Assert.Equal(403, error.Status);
        Assert.Equal(ProjectStatusErrorCodes.Forbidden, error.Code);
    }

    [Fact]
    public async Task EvaluateAsync_DesignerMovingToNonDesignStatus_ReturnsInvalidProjectStatus()
    {
        var designerId = Guid.NewGuid();
        var evaluator = CreateEvaluator();
        var project = CreateProject(
            status: ProjectStatus.IN_CONSULTATION,
            assignedDesignerId: designerId);

        var error = await evaluator.EvaluateAsync(
            project,
            ProjectStatus.WAITING_FOR_DESIGNER_ASSIGNMENT,
            note: null,
            designerId,
            "DESIGNER",
            CancellationToken.None);

        Assert.NotNull(error);
        Assert.Equal(400, error.Status);
        Assert.Equal(ProjectStatusErrorCodes.InvalidProjectStatus, error.Code);
    }

    [Fact]
    public async Task EvaluateAsync_DesignerMovingToProposalSelected_ReturnsForbidden()
    {
        var designerId = Guid.NewGuid();
        var evaluator = CreateEvaluator();
        var project = CreateProject(
            status: ProjectStatus.WAITING_FOR_CUSTOMER_REVIEW,
            assignedDesignerId: designerId);

        var error = await evaluator.EvaluateAsync(
            project,
            ProjectStatus.PROPOSAL_SELECTED,
            note: null,
            designerId,
            "DESIGNER",
            CancellationToken.None);

        Assert.NotNull(error);
        Assert.Equal(403, error.Status);
        Assert.Equal(ProjectStatusErrorCodes.InvalidProjectStatus, error.Code);
    }

    [Fact]
    public async Task EvaluateAsync_UnassignedDesigner_ReturnsForbidden()
    {
        var evaluator = CreateEvaluator();
        var project = CreateProject(
            status: ProjectStatus.SPACE_VERIFIED,
            assignedDesignerId: Guid.NewGuid());

        var error = await evaluator.EvaluateAsync(
            project,
            ProjectStatus.PROPOSAL_DRAFTING,
            note: null,
            Guid.NewGuid(),
            "DESIGNER",
            CancellationToken.None);

        Assert.NotNull(error);
        Assert.Equal(403, error.Status);
        Assert.Equal(ProjectStatusErrorCodes.Forbidden, error.Code);
    }

    [Fact]
    public async Task EvaluateAsync_WithTooLongNote_ReturnsValidationError()
    {
        var evaluator = CreateEvaluator();
        var project = CreateProject(status: ProjectStatus.IN_CONSULTATION);

        var error = await evaluator.EvaluateAsync(
            project,
            ProjectStatus.WAITING_FOR_DESIGNER_ASSIGNMENT,
            note: new string('N', 1001),
            Guid.NewGuid(),
            "ADMIN",
            CancellationToken.None);

        Assert.NotNull(error);
        Assert.Equal(400, error.Status);
        Assert.Equal(ProjectStatusErrorCodes.InvalidProjectStatusTransition, error.Code);
    }

    [Fact]
    public async Task EvaluateAsync_WithMissingCurrentStatus_ReturnsValidationError()
    {
        var evaluator = CreateEvaluator();
        var project = CreateProject(status: null);

        var error = await evaluator.EvaluateAsync(
            project,
            ProjectStatus.WAITING_FOR_DESIGNER_ASSIGNMENT,
            note: null,
            Guid.NewGuid(),
            "ADMIN",
            CancellationToken.None);

        Assert.NotNull(error);
        Assert.Equal(ProjectStatusErrorCodes.InvalidProjectStatusTransition, error.Code);
    }

    [Fact]
    public async Task EvaluateAsync_WithSameTargetStatus_ReturnsValidationError()
    {
        var evaluator = CreateEvaluator();
        var project = CreateProject(status: ProjectStatus.IN_CONSULTATION);

        var error = await evaluator.EvaluateAsync(
            project,
            ProjectStatus.IN_CONSULTATION,
            note: null,
            Guid.NewGuid(),
            "ADMIN",
            CancellationToken.None);

        Assert.NotNull(error);
        Assert.Equal(ProjectStatusErrorCodes.InvalidProjectStatusTransition, error.Code);
    }

    [Fact]
    public async Task EvaluateAsync_InConsultationToWaitingForDesigner_WithIncompleteBasicInfo_ReturnsValidationError()
    {
        var salesId = Guid.NewGuid();
        var evaluator = CreateEvaluator();
        var project = CreateProject(status: ProjectStatus.IN_CONSULTATION, assignedSalesId: salesId);
        project.BusinessType = null;

        var error = await evaluator.EvaluateAsync(
            project,
            ProjectStatus.WAITING_FOR_DESIGNER_ASSIGNMENT,
            note: null,
            salesId,
            "SALES",
            CancellationToken.None);

        Assert.NotNull(error);
        Assert.Equal(ProjectStatusErrorCodes.InvalidProjectStatusTransition, error.Code);
    }

    [Fact]
    public async Task EvaluateAsync_InConsultationToWaitingForDesigner_WithUnassignedSales_ReturnsForbidden()
    {
        var evaluator = CreateEvaluator();
        var project = CreateProject(status: ProjectStatus.IN_CONSULTATION, assignedSalesId: Guid.NewGuid());

        var error = await evaluator.EvaluateAsync(
            project,
            ProjectStatus.WAITING_FOR_DESIGNER_ASSIGNMENT,
            note: null,
            Guid.NewGuid(),
            "SALES",
            CancellationToken.None);

        Assert.NotNull(error);
        Assert.Equal(403, error.Status);
    }

    [Fact]
    public async Task EvaluateAsync_WaitingForDesignerToSpaceVerified_WithoutDesigner_ReturnsDesignerNotAssigned()
    {
        var salesId = Guid.NewGuid();
        var evaluator = CreateEvaluator();
        var project = CreateProject(
            status: ProjectStatus.WAITING_FOR_DESIGNER_ASSIGNMENT,
            assignedSalesId: salesId);

        var error = await evaluator.EvaluateAsync(
            project,
            ProjectStatus.SPACE_VERIFIED,
            note: null,
            salesId,
            "SALES",
            CancellationToken.None);

        Assert.NotNull(error);
        Assert.Equal(ProjectStatusErrorCodes.DesignerNotAssigned, error.Code);
    }

    [Fact]
    public async Task EvaluateAsync_WaitingForDesignerToSpaceVerified_WithAssignedDesigner_ReturnsNull()
    {
        var salesId = Guid.NewGuid();
        var evaluator = CreateEvaluator();
        var project = CreateProject(
            status: ProjectStatus.WAITING_FOR_DESIGNER_ASSIGNMENT,
            assignedSalesId: salesId,
            assignedDesignerId: Guid.NewGuid());

        var error = await evaluator.EvaluateAsync(
            project,
            ProjectStatus.SPACE_VERIFIED,
            note: null,
            salesId,
            "SALES",
            CancellationToken.None);

        Assert.Null(error);
    }

    [Fact]
    public async Task EvaluateAsync_SpaceVerifiedToMeasurementRequired_WithoutNote_ReturnsNoteRequired()
    {
        var designerId = Guid.NewGuid();
        var evaluator = CreateEvaluator();
        var project = CreateProject(
            status: ProjectStatus.SPACE_VERIFIED,
            assignedDesignerId: designerId);

        var error = await evaluator.EvaluateAsync(
            project,
            ProjectStatus.MEASUREMENT_REQUIRED,
            note: null,
            designerId,
            "DESIGNER",
            CancellationToken.None);

        Assert.NotNull(error);
        Assert.Equal(ProjectStatusErrorCodes.NoteRequired, error.Code);
    }

    [Fact]
    public async Task EvaluateAsync_SpaceVerifiedToMeasurementRequired_WithoutDesigner_ReturnsDesignerNotAssigned()
    {
        var salesId = Guid.NewGuid();
        var evaluator = CreateEvaluator();
        var project = CreateProject(
            status: ProjectStatus.SPACE_VERIFIED,
            assignedSalesId: salesId);

        var error = await evaluator.EvaluateAsync(
            project,
            ProjectStatus.MEASUREMENT_REQUIRED,
            note: "Need on-site measurement.",
            salesId,
            "SALES",
            CancellationToken.None);

        Assert.NotNull(error);
        Assert.Equal(ProjectStatusErrorCodes.DesignerNotAssigned, error.Code);
    }

    [Fact]
    public async Task EvaluateAsync_MeasurementRequiredToProposalDrafting_WithoutMeasurementFiles_ReturnsMeasurementFileRequired()
    {
        var designerId = Guid.NewGuid();
        var fakes = new ProjectServiceTransitionFakes
        {
            Schedules = { HasCompletedMeasurement = true },
            Files = { HasMeasurementFiles = false },
            Settings = { RequireMeasurementFileOnProposalDrafting = true }
        };
        var evaluator = CreateEvaluator(fakes);
        var project = CreateProject(
            status: ProjectStatus.MEASUREMENT_REQUIRED,
            assignedDesignerId: designerId);

        var error = await evaluator.EvaluateAsync(
            project,
            ProjectStatus.PROPOSAL_DRAFTING,
            note: null,
            designerId,
            "DESIGNER",
            CancellationToken.None);

        Assert.NotNull(error);
        Assert.Equal(ProjectStatusErrorCodes.MeasurementFileRequired, error.Code);
    }

    [Fact]
    public async Task EvaluateAsync_MeasurementRequiredToProposalDrafting_WithCompletedMeasurement_ReturnsNull()
    {
        var designerId = Guid.NewGuid();
        var fakes = new ProjectServiceTransitionFakes
        {
            Schedules = { HasCompletedMeasurement = true },
            Files = { HasMeasurementFiles = true }
        };
        var evaluator = CreateEvaluator(fakes);
        var project = CreateProject(
            status: ProjectStatus.MEASUREMENT_REQUIRED,
            assignedDesignerId: designerId);

        var error = await evaluator.EvaluateAsync(
            project,
            ProjectStatus.PROPOSAL_DRAFTING,
            note: null,
            designerId,
            "DESIGNER",
            CancellationToken.None);

        Assert.Null(error);
    }

    [Fact]
    public async Task EvaluateAsync_ProposalDraftingToCustomerReview_WithoutProposal_ReturnsFinalProposalRequired()
    {
        var designerId = Guid.NewGuid();
        var evaluator = CreateEvaluator();
        var project = CreateProject(
            status: ProjectStatus.PROPOSAL_DRAFTING,
            assignedDesignerId: designerId);

        var error = await evaluator.EvaluateAsync(
            project,
            ProjectStatus.WAITING_FOR_CUSTOMER_REVIEW,
            note: null,
            designerId,
            "DESIGNER",
            CancellationToken.None);

        Assert.NotNull(error);
        Assert.Equal(ProjectStatusErrorCodes.FinalProposalRequired, error.Code);
    }

    [Fact]
    public async Task EvaluateAsync_ProposalDraftingToCustomerReview_WithActiveScene_ReturnsNull()
    {
        var designerId = Guid.NewGuid();
        var fakes = new ProjectServiceTransitionFakes
        {
            Proposals = { HasProposalWithActiveScene = true }
        };
        var evaluator = CreateEvaluator(fakes);
        var project = CreateProject(
            status: ProjectStatus.PROPOSAL_DRAFTING,
            assignedDesignerId: designerId);

        var error = await evaluator.EvaluateAsync(
            project,
            ProjectStatus.WAITING_FOR_CUSTOMER_REVIEW,
            note: null,
            designerId,
            "DESIGNER",
            CancellationToken.None);

        Assert.Null(error);
    }

    [Fact]
    public async Task EvaluateAsync_CustomerReviewToRevisionRequested_WithAssignedDesigner_ReturnsNull()
    {
        var designerId = Guid.NewGuid();
        var evaluator = CreateEvaluator();
        var project = CreateProject(
            status: ProjectStatus.WAITING_FOR_CUSTOMER_REVIEW,
            assignedDesignerId: designerId);

        var error = await evaluator.EvaluateAsync(
            project,
            ProjectStatus.REVISION_REQUESTED,
            note: null,
            designerId,
            "DESIGNER",
            CancellationToken.None);

        Assert.Null(error);
    }

    [Fact]
    public async Task EvaluateAsync_CustomerReviewToRevisionRequested_WithUnauthorizedUser_ReturnsForbidden()
    {
        var evaluator = CreateEvaluator();
        var project = CreateProject(
            status: ProjectStatus.WAITING_FOR_CUSTOMER_REVIEW,
            assignedDesignerId: Guid.NewGuid());

        var error = await evaluator.EvaluateAsync(
            project,
            ProjectStatus.REVISION_REQUESTED,
            note: null,
            Guid.NewGuid(),
            "DESIGNER",
            CancellationToken.None);

        Assert.NotNull(error);
        Assert.Equal(403, error.Status);
        Assert.Equal(ProjectStatusErrorCodes.Forbidden, error.Code);
    }

    [Fact]
    public async Task EvaluateAsync_CustomerReviewToProposalDrafting_ReturnsDedicatedEndpointMessage()
    {
        var salesId = Guid.NewGuid();
        var evaluator = CreateEvaluator();
        var project = CreateProject(
            status: ProjectStatus.WAITING_FOR_CUSTOMER_REVIEW,
            assignedSalesId: salesId);

        var error = await evaluator.EvaluateAsync(
            project,
            ProjectStatus.PROPOSAL_DRAFTING,
            note: null,
            salesId,
            "SALES",
            CancellationToken.None);

        Assert.NotNull(error);
        Assert.Equal(ProjectStatusErrorCodes.InvalidProjectStatusTransition, error.Code);
        Assert.Contains("dedicated proposal revision endpoint", error.Message);
    }

    [Fact]
    public async Task EvaluateAsync_CustomerReviewToProposalSelected_WithNonSales_ReturnsForbidden()
    {
        var designerId = Guid.NewGuid();
        var fakes = new ProjectServiceTransitionFakes
        {
            Proposals = { HasSelectedFinalProposal = true }
        };
        var evaluator = CreateEvaluator(fakes);
        var project = CreateProject(
            status: ProjectStatus.WAITING_FOR_CUSTOMER_REVIEW,
            assignedDesignerId: designerId);

        var error = await evaluator.EvaluateAsync(
            project,
            ProjectStatus.PROPOSAL_SELECTED,
            note: null,
            designerId,
            "DESIGNER",
            CancellationToken.None);

        Assert.NotNull(error);
        Assert.Equal(403, error.Status);
    }

    [Fact]
    public async Task EvaluateAsync_MeasurementRequiredToSpaceVerified_WithUnauthorizedUser_ReturnsForbidden()
    {
        var evaluator = CreateEvaluator();
        var project = CreateProject(
            status: ProjectStatus.MEASUREMENT_REQUIRED,
            assignedDesignerId: Guid.NewGuid());

        var error = await evaluator.EvaluateAsync(
            project,
            ProjectStatus.SPACE_VERIFIED,
            note: "Verified.",
            Guid.NewGuid(),
            "DESIGNER",
            CancellationToken.None);

        Assert.NotNull(error);
        Assert.Equal(ProjectStatusErrorCodes.Forbidden, error.Code);
    }

    [Fact]
    public async Task EvaluateAsync_RevisionRequestedToProposalDrafting_WithAssignedSales_ReturnsNull()
    {
        var salesId = Guid.NewGuid();
        var evaluator = CreateEvaluator();
        var project = CreateProject(
            status: ProjectStatus.REVISION_REQUESTED,
            assignedSalesId: salesId,
            assignedDesignerId: Guid.NewGuid());

        var error = await evaluator.EvaluateAsync(
            project,
            ProjectStatus.PROPOSAL_DRAFTING,
            note: null,
            salesId,
            "SALES",
            CancellationToken.None);

        Assert.Null(error);
    }

    [Fact]
    public async Task EvaluateAsync_InConsultationToWaitingForDesigner_WithAdmin_ReturnsNull()
    {
        var evaluator = CreateEvaluator();
        var project = CreateProject(status: ProjectStatus.IN_CONSULTATION);

        var error = await evaluator.EvaluateAsync(
            project,
            ProjectStatus.WAITING_FOR_DESIGNER_ASSIGNMENT,
            note: null,
            Guid.NewGuid(),
            "ADMIN",
            CancellationToken.None);

        Assert.Null(error);
    }

    [Fact]
    public async Task EvaluateAsync_SpaceVerifiedToProposalDrafting_WithAssignedDesigner_ReturnsNull()
    {
        var designerId = Guid.NewGuid();
        var evaluator = CreateEvaluator();
        var project = CreateProject(
            status: ProjectStatus.SPACE_VERIFIED,
            assignedDesignerId: designerId);

        var error = await evaluator.EvaluateAsync(
            project,
            ProjectStatus.PROPOSAL_DRAFTING,
            note: null,
            designerId,
            "DESIGNER",
            CancellationToken.None);

        Assert.Null(error);
    }

    [Fact]
    public async Task EvaluateAsync_SpaceVerifiedToProposalDrafting_WithoutDesigner_ReturnsDesignerNotAssigned()
    {
        var salesId = Guid.NewGuid();
        var evaluator = CreateEvaluator();
        var project = CreateProject(
            status: ProjectStatus.SPACE_VERIFIED,
            assignedSalesId: salesId);

        var error = await evaluator.EvaluateAsync(
            project,
            ProjectStatus.PROPOSAL_DRAFTING,
            note: null,
            salesId,
            "SALES",
            CancellationToken.None);

        Assert.NotNull(error);
        Assert.Equal(ProjectStatusErrorCodes.DesignerNotAssigned, error.Code);
    }

    [Fact]
    public async Task EvaluateAsync_ProposalDraftingToCustomerReview_WithUnauthorizedUser_ReturnsForbidden()
    {
        var evaluator = CreateEvaluator();
        var project = CreateProject(
            status: ProjectStatus.PROPOSAL_DRAFTING,
            assignedDesignerId: Guid.NewGuid());

        var error = await evaluator.EvaluateAsync(
            project,
            ProjectStatus.WAITING_FOR_CUSTOMER_REVIEW,
            note: null,
            Guid.NewGuid(),
            "DESIGNER",
            CancellationToken.None);

        Assert.NotNull(error);
        Assert.Equal(ProjectStatusErrorCodes.Forbidden, error.Code);
    }

    [Fact]
    public async Task EvaluateAsync_CustomerReviewToProposalSelected_WithoutSelectedProposal_ReturnsFinalProposalRequired()
    {
        var salesId = Guid.NewGuid();
        var evaluator = CreateEvaluator();
        var project = CreateProject(
            status: ProjectStatus.WAITING_FOR_CUSTOMER_REVIEW,
            assignedSalesId: salesId);

        var error = await evaluator.EvaluateAsync(
            project,
            ProjectStatus.PROPOSAL_SELECTED,
            note: null,
            salesId,
            "SALES",
            CancellationToken.None);

        Assert.NotNull(error);
        Assert.Equal(ProjectStatusErrorCodes.FinalProposalRequired, error.Code);
    }

    [Fact]
    public async Task EvaluateAsync_CustomerReviewToProposalSelected_WithSelectedProposal_ReturnsNull()
    {
        var salesId = Guid.NewGuid();
        var fakes = new ProjectServiceTransitionFakes
        {
            Proposals = { HasSelectedFinalProposal = true }
        };
        var evaluator = CreateEvaluator(fakes);
        var project = CreateProject(
            status: ProjectStatus.WAITING_FOR_CUSTOMER_REVIEW,
            assignedSalesId: salesId);

        var error = await evaluator.EvaluateAsync(
            project,
            ProjectStatus.PROPOSAL_SELECTED,
            note: null,
            salesId,
            "SALES",
            CancellationToken.None);

        Assert.Null(error);
    }

    [Fact]
    public async Task EvaluateAsync_UnsupportedTransition_ReturnsInvalidProjectStatusTransition()
    {
        var salesId = Guid.NewGuid();
        var evaluator = CreateEvaluator();
        var project = CreateProject(
            status: ProjectStatus.ORDER_CONFIRMED,
            assignedSalesId: salesId);

        var error = await evaluator.EvaluateAsync(
            project,
            ProjectStatus.IN_PRODUCTION,
            note: null,
            salesId,
            "SALES",
            CancellationToken.None);

        Assert.NotNull(error);
        Assert.Equal(ProjectStatusErrorCodes.InvalidProjectStatusTransition, error.Code);
    }

    [Fact]
    public async Task EvaluateAsync_RevisionRequestedToProposalDrafting_WithUnauthorizedUser_ReturnsForbidden()
    {
        var evaluator = CreateEvaluator();
        var project = CreateProject(
            status: ProjectStatus.REVISION_REQUESTED,
            assignedDesignerId: Guid.NewGuid());

        var error = await evaluator.EvaluateAsync(
            project,
            ProjectStatus.PROPOSAL_DRAFTING,
            note: null,
            Guid.NewGuid(),
            "DESIGNER",
            CancellationToken.None);

        Assert.NotNull(error);
        Assert.Equal(ProjectStatusErrorCodes.Forbidden, error.Code);
    }

    [Fact]
    public async Task EvaluateAsync_SpaceVerifiedToMeasurementRequired_WithUnauthorizedUser_ReturnsForbidden()
    {
        var evaluator = CreateEvaluator();
        var project = CreateProject(
            status: ProjectStatus.SPACE_VERIFIED,
            assignedDesignerId: Guid.NewGuid());

        var error = await evaluator.EvaluateAsync(
            project,
            ProjectStatus.MEASUREMENT_REQUIRED,
            note: "Need measurement.",
            Guid.NewGuid(),
            "PRODUCTION",
            CancellationToken.None);

        Assert.NotNull(error);
        Assert.Equal(403, error.Status);
        Assert.Equal(ProjectStatusErrorCodes.InvalidProjectStatusTransition, error.Code);
    }

    private static ProjectStatusTransitionEvaluator CreateEvaluator(
        ProjectServiceTransitionFakes? fakes = null)
    {
        fakes ??= new ProjectServiceTransitionFakes();
        return new ProjectStatusTransitionEvaluator(
            fakes.Schedules,
            fakes.Files,
            fakes.Proposals,
            Options.Create(fakes.Settings));
    }

    private static Project CreateProject(
        ProjectStatus? status,
        Guid? assignedSalesId = null,
        Guid? assignedDesignerId = null)
    {
        return new Project
        {
            ProjectId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            AssignedSalesId = assignedSalesId ?? Guid.NewGuid(),
            AssignedDesignerId = assignedDesignerId,
            ProjectName = "Moc Coffee Interior Setup",
            BusinessType = "Cafe",
            FurnitureRequirement = "Counter, tables, chairs",
            Status = status
        };
    }
}
