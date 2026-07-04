#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.Common;
using FurniSpace.Application.Common.Notifications;
using FurniSpace.Application.DTOs.ProjectChats;
using FurniSpace.Application.DTOs.Projects;
using FurniSpace.Application.Interfaces.Notifications;
using FurniSpace.Application.Interfaces.ProjectChats;
using FurniSpace.Application.Interfaces.Search;
using FurniSpace.Application.Services.Projects;
using FurniSpace.Application.Tests.TestDoubles;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Common.Search;
using FurniSpace.Infrastructure.ReadModels.Projects;
using FurniSpace.Infrastructure.Interfaces;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Xunit;

namespace FurniSpace.Application.Tests.Projects;

public sealed class ProjectServiceTests
{
    [Fact]
    public async Task AssignDesignerAsync_WithAssignedSalesAndSufficientSpace_AssignsDesignerAndVerifiesSpace()
    {
        var salesId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var designer = CreateDesigner();
        var project = CreateDesignerAssignableProject(projectId, salesId);
        var repository = new FakeProjectRepository(roleName: "SALES", entities: [project], designer: designer);
        var projectChats = new FakeProjectChatService();
        var service = ProjectServiceTestFactory.Create(
            repository,
            TestUnitOfWork.ForSaveChanges(repository.SaveChangesAsync),
            projectChats: projectChats);

        var result = await service.AssignDesignerAsync(projectId, salesId, new AssignProjectDesignerRequestDto
        {
            DesignerId = designer.AccountId,
            SpaceDataStatus = ProjectSpaceDataStatus.SUFFICIENT,
            Note = "Please review."
        });

        Assert.Equal(200, result.Status);
        Assert.Equal("Designer assigned successfully.", result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal(projectId, result.Data.ProjectId);
        Assert.Equal(designer.AccountId, result.Data.AssignedDesigner.AccountId);
        Assert.Equal(designer.FullName, result.Data.AssignedDesigner.FullName);
        Assert.Equal(ProjectStatus.SPACE_VERIFIED, result.Data.Status);
        Assert.NotNull(result.Data.DesignerAssignedAt);
        Assert.Equal(designer.AccountId, project.AssignedDesignerId);
        Assert.Equal(ProjectStatus.SPACE_VERIFIED, project.Status);
        Assert.Equal(project.DesignerAssignedAt, project.UpdatedAt);
        Assert.Equal(1, repository.GetByIdCallCount);
        Assert.Equal(1, repository.GetAccountRoleNameCallCount);
        Assert.Equal(1, repository.GetActiveDesignerCallCount);
        Assert.Equal(1, repository.SaveChangesCallCount);
        Assert.Equal(1, projectChats.UpsertCallCount);
        Assert.Equal(projectId, projectChats.ProjectId);
        Assert.Equal(ProjectChatType.DESIGNER, projectChats.ChatType);
        Assert.Equal(designer.AccountId, projectChats.StaffId);
        Assert.Equal("Design Discussion", projectChats.Title);
    }

    [Fact]
    public async Task AssignDesignerAsync_WithAdminAndInsufficientSpace_AssignsDesignerAndRequiresMeasurement()
    {
        var projectId = Guid.NewGuid();
        var designer = CreateDesigner();
        var project = CreateDesignerAssignableProject(projectId, Guid.NewGuid());
        var repository = new FakeProjectRepository(roleName: "ADMIN", entities: [project], designer: designer);
        var projectChats = new FakeProjectChatService();
        var service = ProjectServiceTestFactory.Create(
            repository,
            TestUnitOfWork.Instance,
            projectChats: projectChats);

        var result = await service.AssignDesignerAsync(projectId, Guid.NewGuid(), new AssignProjectDesignerRequestDto
        {
            DesignerId = designer.AccountId,
            SpaceDataStatus = ProjectSpaceDataStatus.INSUFFICIENT
        });

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(ProjectStatus.MEASUREMENT_REQUIRED, result.Data.Status);
        Assert.Equal(1, projectChats.UpsertCallCount);
    }

    [Fact]
    public async Task AssignDesignerAsync_WithEmptyProjectId_ReturnsBadRequest()
    {
        var repository = new FakeProjectRepository(roleName: "SALES");
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.AssignDesignerAsync(Guid.Empty, Guid.NewGuid(), ValidAssignDesignerRequest());

        Assert.Equal(400, result.Status);
        Assert.Equal("Project id is required.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.GetByIdCallCount);
    }

    [Fact]
    public async Task AssignDesignerAsync_WithEmptyCurrentUser_ReturnsUnauthorized()
    {
        var repository = new FakeProjectRepository(roleName: "SALES");
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.AssignDesignerAsync(Guid.NewGuid(), Guid.Empty, ValidAssignDesignerRequest());

        Assert.Equal(401, result.Status);
        Assert.Equal("Authenticated account id is required.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.GetByIdCallCount);
    }

    [Fact]
    public async Task AssignDesignerAsync_WithEmptyDesignerId_ReturnsBadRequest()
    {
        var repository = new FakeProjectRepository(roleName: "SALES");
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);
        var request = ValidAssignDesignerRequest();
        request.DesignerId = Guid.Empty;

        var result = await service.AssignDesignerAsync(Guid.NewGuid(), Guid.NewGuid(), request);

        Assert.Equal(400, result.Status);
        Assert.Equal("Designer id is required.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.GetByIdCallCount);
    }

    [Fact]
    public async Task AssignDesignerAsync_WithMissingSpaceDataStatus_ReturnsBadRequest()
    {
        var repository = new FakeProjectRepository(roleName: "SALES");
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);
        var request = ValidAssignDesignerRequest();
        request.SpaceDataStatus = null;

        var result = await service.AssignDesignerAsync(Guid.NewGuid(), Guid.NewGuid(), request);

        Assert.Equal(400, result.Status);
        Assert.Equal("Space data status is required.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.GetByIdCallCount);
    }

    [Fact]
    public async Task AssignDesignerAsync_WithTooLongNote_ReturnsBadRequest()
    {
        var repository = new FakeProjectRepository(roleName: "SALES");
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);
        var request = ValidAssignDesignerRequest();
        request.Note = new string('N', 1001);

        var result = await service.AssignDesignerAsync(Guid.NewGuid(), Guid.NewGuid(), request);

        Assert.Equal(400, result.Status);
        Assert.Equal("Designer assignment note must not exceed 1000 characters.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.GetByIdCallCount);
    }

    [Fact]
    public async Task AssignDesignerAsync_WithMissingProject_ReturnsNotFound()
    {
        var repository = new FakeProjectRepository(roleName: "SALES");
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.AssignDesignerAsync(Guid.NewGuid(), Guid.NewGuid(), ValidAssignDesignerRequest());

        Assert.Equal(404, result.Status);
        Assert.Equal("Project not found.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(1, repository.GetByIdCallCount);
        Assert.Equal(0, repository.GetAccountRoleNameCallCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("CUSTOMER")]
    [InlineData("DESIGNER")]
    [InlineData("SALES")]
    public async Task AssignDesignerAsync_WithUnauthorizedRoleOrSales_ReturnsForbidden(string? roleName)
    {
        var projectId = Guid.NewGuid();
        var project = CreateDesignerAssignableProject(projectId, Guid.NewGuid());
        var repository = new FakeProjectRepository(roleName: roleName, entities: [project], designer: CreateDesigner());
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.AssignDesignerAsync(projectId, Guid.NewGuid(), ValidAssignDesignerRequest());

        Assert.Equal(403, result.Status);
        Assert.Equal("You do not have access to assign a designer to this project.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(1, repository.GetByIdCallCount);
        Assert.Equal(1, repository.GetAccountRoleNameCallCount);
        Assert.Equal(0, repository.GetActiveDesignerCallCount);
    }

    [Fact]
    public async Task AssignDesignerAsync_WithProjectWithoutAssignedSales_ReturnsBadRequest()
    {
        var projectId = Guid.NewGuid();
        var project = CreateDesignerAssignableProject(projectId, Guid.NewGuid());
        project.AssignedSalesId = null;
        var repository = new FakeProjectRepository(roleName: "ADMIN", entities: [project], designer: CreateDesigner());
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.AssignDesignerAsync(projectId, Guid.NewGuid(), ValidAssignDesignerRequest());

        Assert.Equal(400, result.Status);
        Assert.Equal("Project must have assigned sales before designer assignment.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.GetActiveDesignerCallCount);
    }

    [Fact]
    public async Task AssignDesignerAsync_WithInvalidProjectStatus_ReturnsBadRequest()
    {
        var salesId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var project = CreateDesignerAssignableProject(projectId, salesId);
        project.Status = ProjectStatus.IN_CONSULTATION;
        var repository = new FakeProjectRepository(roleName: "SALES", entities: [project], designer: CreateDesigner());
        var projectChats = new FakeProjectChatService();
        var service = ProjectServiceTestFactory.Create(
            repository,
            TestUnitOfWork.Instance,
            projectChats: projectChats);

        var result = await service.AssignDesignerAsync(projectId, salesId, ValidAssignDesignerRequest());

        Assert.Equal(400, result.Status);
        Assert.Equal("Project must be waiting for designer assignment.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.GetActiveDesignerCallCount);
        Assert.Equal(0, projectChats.UpsertCallCount);
    }

    [Fact]
    public async Task AssignDesignerAsync_WithInvalidDesigner_ReturnsBadRequest()
    {
        var salesId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var project = CreateDesignerAssignableProject(projectId, salesId);
        var repository = new FakeProjectRepository(roleName: "SALES", entities: [project]);
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.AssignDesignerAsync(projectId, salesId, ValidAssignDesignerRequest());

        Assert.Equal(400, result.Status);
        Assert.Equal("Designer account is not active or does not have Designer role.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(1, repository.GetActiveDesignerCallCount);
        Assert.Equal(0, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task AssignDesignerAsync_AfterSuccessfulAssignment_DispatchesDesignerAssignedNotification()
    {
        var salesId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var designer = CreateDesigner();
        var project = CreateDesignerAssignableProject(projectId, salesId);
        var dispatcher = new FakeNotificationDispatcher();
        var projectChats = new FakeProjectChatService();
        var repository = new FakeProjectRepository(roleName: "SALES", entities: [project], designer: designer);
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.ForSaveChanges(repository.SaveChangesAsync), dispatcher, projectChats: projectChats);

        var result = await service.AssignDesignerAsync(projectId, salesId, new AssignProjectDesignerRequestDto
        {
            DesignerId = designer.AccountId,
            SpaceDataStatus = ProjectSpaceDataStatus.SUFFICIENT
        });

        Assert.Equal(200, result.Status);
        Assert.Equal(1, dispatcher.DispatchCallCount);
        Assert.Equal(NotificationType.ProjectDesignerAssigned, dispatcher.LastType);
        Assert.Equal(projectId, dispatcher.LastProjectId);
        Assert.Equal("PROJECT", dispatcher.LastReferenceType);
        Assert.Equal(projectId, dispatcher.LastReferenceId);
        Assert.Equal([designer.AccountId], dispatcher.LastReceiverIds);
        Assert.NotNull(dispatcher.LastParameters);
        Assert.Equal("Moc Coffee Interior Setup", dispatcher.LastParameters["ProjectName"]);
        Assert.Equal(1, projectChats.UpsertCallCount);
    }

    [Fact]
    public async Task AssignDesignerAsync_WhenNotificationFails_StillReturnsSuccess()
    {
        var salesId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var designer = CreateDesigner();
        var project = CreateDesignerAssignableProject(projectId, salesId);
        var dispatcher = new FakeNotificationDispatcher(throwOnDispatch: true);
        var projectChats = new FakeProjectChatService();
        var repository = new FakeProjectRepository(roleName: "SALES", entities: [project], designer: designer);
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.ForSaveChanges(repository.SaveChangesAsync), dispatcher, projectChats: projectChats);

        var result = await service.AssignDesignerAsync(projectId, salesId, new AssignProjectDesignerRequestDto
        {
            DesignerId = designer.AccountId,
            SpaceDataStatus = ProjectSpaceDataStatus.INSUFFICIENT
        });

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(ProjectStatus.MEASUREMENT_REQUIRED, result.Data.Status);
        Assert.Equal(1, repository.SaveChangesCallCount);
        Assert.Equal(1, dispatcher.DispatchCallCount);
        Assert.Equal(1, projectChats.UpsertCallCount);
    }

    [Fact]
    public async Task AssignDesignerAsync_WhenSuccessful_CommitsBeforeNotification()
    {
        var salesId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var designer = CreateDesigner();
        var project = CreateDesignerAssignableProject(projectId, salesId);
        var repository = new FakeProjectRepository(
            roleName: "SALES",
            entities: [project],
            designer: designer);
        var projectChats = new FakeProjectChatService();
        var beginCallCount = 0;
        var commitCallCount = 0;
        var rollbackCallCount = 0;
        var dispatcher = new FakeNotificationDispatcher(
            onDispatch: () => Assert.Equal(1, commitCallCount));
        var unitOfWork = TestUnitOfWork.ForTransaction(
            _ =>
            {
                beginCallCount++;
                return Task.CompletedTask;
            },
            repository.SaveChangesAsync,
            _ =>
            {
                commitCallCount++;
                return Task.CompletedTask;
            },
            _ =>
            {
                rollbackCallCount++;
                return Task.CompletedTask;
            });
        var service = ProjectServiceTestFactory.Create(repository, unitOfWork, dispatcher, projectChats: projectChats);

        var result = await service.AssignDesignerAsync(
            projectId,
            salesId,
            new AssignProjectDesignerRequestDto
            {
                DesignerId = designer.AccountId,
                SpaceDataStatus = ProjectSpaceDataStatus.SUFFICIENT
            });

        Assert.Equal(200, result.Status);
        Assert.Equal(1, beginCallCount);
        Assert.Equal(1, projectChats.UpsertCallCount);
        Assert.Equal(1, repository.SaveChangesCallCount);
        Assert.Equal(1, commitCallCount);
        Assert.Equal(0, rollbackCallCount);
        Assert.Equal(1, dispatcher.DispatchCallCount);
    }

    [Fact]
    public async Task AssignDesignerAsync_WhenChatUpsertFails_RollsBackAndDoesNotNotify()
    {
        var salesId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var designer = CreateDesigner();
        var project = CreateDesignerAssignableProject(projectId, salesId);
        var repository = new FakeProjectRepository(
            roleName: "SALES",
            entities: [project],
            designer: designer);
        var projectChats = new FakeProjectChatService(throwOnUpsert: true);
        var dispatcher = new FakeNotificationDispatcher();
        var rollbackCallCount = 0;
        var unitOfWork = TestUnitOfWork.ForTransaction(
            _ => Task.CompletedTask,
            repository.SaveChangesAsync,
            _ => Task.CompletedTask,
            _ =>
            {
                rollbackCallCount++;
                return Task.CompletedTask;
            });
        var service = ProjectServiceTestFactory.Create(repository, unitOfWork, dispatcher, projectChats: projectChats);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AssignDesignerAsync(
                projectId,
                salesId,
                new AssignProjectDesignerRequestDto
                {
                    DesignerId = designer.AccountId,
                    SpaceDataStatus = ProjectSpaceDataStatus.SUFFICIENT
                }));

        Assert.Equal("Project chat upsert failed.", exception.Message);
        Assert.Equal(1, rollbackCallCount);
        Assert.Equal(0, repository.SaveChangesCallCount);
        Assert.Equal(0, dispatcher.DispatchCallCount);
    }

    [Fact]
    public async Task AssignDesignerAsync_WhenSaveFails_RollsBackAndDoesNotNotify()
    {
        var salesId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var designer = CreateDesigner();
        var project = CreateDesignerAssignableProject(projectId, salesId);
        var repository = new FakeProjectRepository(
            roleName: "SALES",
            entities: [project],
            designer: designer);
        var projectChats = new FakeProjectChatService();
        var dispatcher = new FakeNotificationDispatcher();
        var commitCallCount = 0;
        var rollbackCallCount = 0;
        var unitOfWork = TestUnitOfWork.ForTransaction(
            _ => Task.CompletedTask,
            _ => Task.FromException<int>(new InvalidOperationException("Project save failed.")),
            _ =>
            {
                commitCallCount++;
                return Task.CompletedTask;
            },
            _ =>
            {
                rollbackCallCount++;
                return Task.CompletedTask;
            });
        var service = ProjectServiceTestFactory.Create(repository, unitOfWork, dispatcher, projectChats: projectChats);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AssignDesignerAsync(
                projectId,
                salesId,
                new AssignProjectDesignerRequestDto
                {
                    DesignerId = designer.AccountId,
                    SpaceDataStatus = ProjectSpaceDataStatus.SUFFICIENT
                }));

        Assert.Equal("Project save failed.", exception.Message);
        Assert.Equal(1, projectChats.UpsertCallCount);
        Assert.Equal(0, commitCallCount);
        Assert.Equal(1, rollbackCallCount);
        Assert.Equal(0, dispatcher.DispatchCallCount);
    }

    [Fact]
    public async Task RejectAsync_WithAssignedSales_RejectsProject()
    {
        var salesId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var project = CreateQualifiedProject(projectId, salesId);
        var repository = new FakeProjectRepository(roleName: "SALES", entities: [project]);
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.ForSaveChanges(repository.SaveChangesAsync));

        var result = await service.RejectAsync(projectId, salesId, new RejectProjectRequestDto
        {
            RejectionReason = " Requested service scope is unsupported. "
        });

        Assert.Equal(200, result.Status);
        Assert.Equal("Project request rejected.", result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal(projectId, result.Data.ProjectId);
        Assert.Equal(ProjectStatus.REJECTED, result.Data.Status);
        Assert.Equal("Requested service scope is unsupported.", result.Data.RejectionReason);
        Assert.NotNull(result.Data.RejectedAt);
        Assert.Equal(ProjectStatus.REJECTED, project.Status);
        Assert.Equal("Requested service scope is unsupported.", project.RejectionReason);
        Assert.NotNull(project.RejectedAt);
        Assert.Equal(project.RejectedAt, project.UpdatedAt);
        Assert.Equal(1, repository.GetByIdCallCount);
        Assert.Equal(1, repository.GetAccountRoleNameCallCount);
        Assert.Equal(1, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task RejectAsync_WithAdmin_RejectsUnassignedProjectBeforeOrderConfirmed()
    {
        var projectId = Guid.NewGuid();
        var project = CreateQualifiedProject(projectId, Guid.NewGuid());
        project.AssignedSalesId = null;
        project.Status = ProjectStatus.QUOTATION_SENT;
        var repository = new FakeProjectRepository(roleName: "ADMIN", entities: [project]);
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.RejectAsync(projectId, Guid.NewGuid(), ValidRejectRequest());

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(ProjectStatus.REJECTED, result.Data.Status);
    }

    [Fact]
    public async Task RejectAsync_WithEmptyProjectId_ReturnsBadRequest()
    {
        var repository = new FakeProjectRepository(roleName: "SALES");
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.RejectAsync(Guid.Empty, Guid.NewGuid(), ValidRejectRequest());

        Assert.Equal(400, result.Status);
        Assert.Equal("Project id is required.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.GetByIdCallCount);
    }

    [Fact]
    public async Task RejectAsync_WithEmptyCurrentUser_ReturnsUnauthorized()
    {
        var repository = new FakeProjectRepository(roleName: "SALES");
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.RejectAsync(Guid.NewGuid(), Guid.Empty, ValidRejectRequest());

        Assert.Equal(401, result.Status);
        Assert.Equal("Authenticated account id is required.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.GetByIdCallCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task RejectAsync_WithMissingReason_ReturnsBadRequest(string reason)
    {
        var repository = new FakeProjectRepository(roleName: "SALES");
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.RejectAsync(Guid.NewGuid(), Guid.NewGuid(), new RejectProjectRequestDto
        {
            RejectionReason = reason
        });

        Assert.Equal(400, result.Status);
        Assert.Equal("Rejection reason is required.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.GetByIdCallCount);
    }

    [Fact]
    public async Task RejectAsync_WithTooLongReason_ReturnsBadRequest()
    {
        var repository = new FakeProjectRepository(roleName: "SALES");
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.RejectAsync(Guid.NewGuid(), Guid.NewGuid(), new RejectProjectRequestDto
        {
            RejectionReason = new string('R', 1001)
        });

        Assert.Equal(400, result.Status);
        Assert.Equal("Rejection reason must not exceed 1000 characters.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.GetByIdCallCount);
    }

    [Fact]
    public async Task RejectAsync_WithMissingProject_ReturnsNotFound()
    {
        var repository = new FakeProjectRepository(roleName: "SALES");
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.RejectAsync(Guid.NewGuid(), Guid.NewGuid(), ValidRejectRequest());

        Assert.Equal(404, result.Status);
        Assert.Equal("Project not found.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(1, repository.GetByIdCallCount);
        Assert.Equal(0, repository.GetAccountRoleNameCallCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("CUSTOMER")]
    [InlineData("DESIGNER")]
    [InlineData("SALES")]
    public async Task RejectAsync_WithUnauthorizedRoleOrSales_ReturnsForbidden(string? roleName)
    {
        var projectId = Guid.NewGuid();
        var project = CreateQualifiedProject(projectId, Guid.NewGuid());
        var repository = new FakeProjectRepository(roleName: roleName, entities: [project]);
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.RejectAsync(projectId, Guid.NewGuid(), ValidRejectRequest());

        Assert.Equal(403, result.Status);
        Assert.Equal("You do not have access to reject this project.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(1, repository.GetByIdCallCount);
        Assert.Equal(1, repository.GetAccountRoleNameCallCount);
        Assert.Equal(0, repository.SaveChangesCallCount);
    }

    [Theory]
    [InlineData(ProjectStatus.ORDER_CONFIRMED)]
    [InlineData(ProjectStatus.IN_PRODUCTION)]
    [InlineData(ProjectStatus.REJECTED)]
    public async Task RejectAsync_WithNonRejectableStatus_ReturnsBadRequest(ProjectStatus status)
    {
        var salesId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var project = CreateQualifiedProject(projectId, salesId);
        project.Status = status;
        var repository = new FakeProjectRepository(roleName: "SALES", entities: [project]);
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.RejectAsync(projectId, salesId, ValidRejectRequest());

        Assert.Equal(400, result.Status);
        Assert.Equal("Project cannot be rejected from its current status.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task UpdateStatusAsync_WithAssignedSales_MovesToWaitingForDesignerAssignment()
    {
        var salesId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var project = CreateQualifiedProject(projectId, salesId);
        var repository = new FakeProjectRepository(roleName: "SALES", entities: [project]);
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.ForSaveChanges(repository.SaveChangesAsync));

        var result = await service.UpdateStatusAsync(projectId, salesId, new UpdateProjectStatusRequestDto
        {
            Status = ProjectStatus.WAITING_FOR_DESIGNER_ASSIGNMENT,
            Note = "Ready for designer."
        });

        Assert.Equal(200, result.Status);
        Assert.Equal("Project status updated successfully.", result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal(projectId, result.Data.ProjectId);
        Assert.Equal(ProjectStatus.WAITING_FOR_DESIGNER_ASSIGNMENT, result.Data.Status);
        Assert.NotNull(result.Data.UpdatedAt);
        Assert.Equal(ProjectStatus.WAITING_FOR_DESIGNER_ASSIGNMENT, project.Status);
        Assert.Equal(result.Data.UpdatedAt, project.UpdatedAt);
        Assert.Equal(1, repository.GetByIdCallCount);
        Assert.Equal(1, repository.GetAccountRoleNameCallCount);
        Assert.Equal(1, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task UpdateStatusAsync_AfterSuccessfulUpdate_DispatchesRealtimeStatusChangedToProjectParticipants()
    {
        var salesId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var designerId = Guid.NewGuid();
        var project = CreateQualifiedProject(projectId, salesId);
        project.AssignedDesignerId = designerId;
        var dispatcher = new FakeNotificationDispatcher();
        var repository = new FakeProjectRepository(roleName: "SALES", entities: [project]);
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.ForSaveChanges(repository.SaveChangesAsync), dispatcher);

        var result = await service.UpdateStatusAsync(projectId, salesId, ValidStatusRequest());

        Assert.Equal(200, result.Status);
        Assert.Equal(1, dispatcher.DispatchCallCount);
        Assert.Equal(NotificationType.ProjectStatusChanged, dispatcher.LastType);
        Assert.Equal(projectId, dispatcher.LastProjectId);
        Assert.Equal("PROJECT", dispatcher.LastReferenceType);
        Assert.Equal(projectId, dispatcher.LastReferenceId);
        Assert.Equal(
            [project.CustomerId, salesId, designerId],
            dispatcher.LastReceiverIds);
        Assert.NotNull(dispatcher.LastParameters);
        Assert.Equal("Moc Coffee Interior Setup", dispatcher.LastParameters["ProjectName"]);
        Assert.Equal(ProjectStatus.WAITING_FOR_DESIGNER_ASSIGNMENT.ToString(), dispatcher.LastParameters["Status"]);
    }

    [Fact]
    public async Task UpdateStatusAsync_WhenParticipantsContainDuplicates_DispatchesDistinctRealtimeReceivers()
    {
        var sharedAccountId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var project = CreateQualifiedProject(projectId, sharedAccountId);
        project.CustomerId = sharedAccountId;
        project.AssignedDesignerId = sharedAccountId;
        var dispatcher = new FakeNotificationDispatcher();
        var repository = new FakeProjectRepository(roleName: "SALES", entities: [project]);
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.ForSaveChanges(repository.SaveChangesAsync), dispatcher);

        var result = await service.UpdateStatusAsync(projectId, sharedAccountId, ValidStatusRequest());

        Assert.Equal(200, result.Status);
        Assert.Equal(1, dispatcher.DispatchCallCount);
        Assert.Equal([sharedAccountId], dispatcher.LastReceiverIds);
    }

    [Fact]
    public async Task UpdateStatusAsync_WhenNotificationFails_StillReturnsSuccess()
    {
        var salesId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var project = CreateQualifiedProject(projectId, salesId);
        var dispatcher = new FakeNotificationDispatcher(throwOnDispatch: true);
        var repository = new FakeProjectRepository(roleName: "SALES", entities: [project]);
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.ForSaveChanges(repository.SaveChangesAsync), dispatcher);

        var result = await service.UpdateStatusAsync(projectId, salesId, ValidStatusRequest());

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(ProjectStatus.WAITING_FOR_DESIGNER_ASSIGNMENT, result.Data.Status);
        Assert.Equal(1, repository.SaveChangesCallCount);
        Assert.Equal(1, dispatcher.DispatchCallCount);
    }

    [Fact]
    public async Task UpdateStatusAsync_WithAdmin_UpdatesAnyQualifiedProject()
    {
        var projectId = Guid.NewGuid();
        var project = CreateQualifiedProject(projectId, Guid.NewGuid());
        var repository = new FakeProjectRepository(roleName: "ADMIN", entities: [project]);
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.UpdateStatusAsync(projectId, Guid.NewGuid(), new UpdateProjectStatusRequestDto
        {
            Status = ProjectStatus.WAITING_FOR_DESIGNER_ASSIGNMENT
        });

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(ProjectStatus.WAITING_FOR_DESIGNER_ASSIGNMENT, result.Data.Status);
    }

    [Fact]
    public async Task UpdateStatusAsync_WithAssignedDesigner_MovesDesignStatus()
    {
        var designerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var project = CreateQualifiedProject(projectId, Guid.NewGuid());
        project.AssignedDesignerId = designerId;
        project.Status = ProjectStatus.SPACE_VERIFIED;
        var repository = new FakeProjectRepository(roleName: "DESIGNER", entities: [project]);
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.ForSaveChanges(repository.SaveChangesAsync));

        var result = await service.UpdateStatusAsync(projectId, designerId, new UpdateProjectStatusRequestDto
        {
            Status = ProjectStatus.PROPOSAL_DRAFTING,
            Note = "Designer starts drafting proposal after space verification."
        });

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(ProjectStatus.SPACE_VERIFIED, result.Data.OldStatus);
        Assert.Equal(ProjectStatus.PROPOSAL_DRAFTING, result.Data.NewStatus);
        Assert.Equal(ProjectStatus.PROPOSAL_DRAFTING, project.Status);
    }

    [Fact]
    public async Task UpdateStatusAsync_WithUnassignedDesigner_ReturnsForbidden()
    {
        var projectId = Guid.NewGuid();
        var project = CreateQualifiedProject(projectId, Guid.NewGuid());
        project.AssignedDesignerId = Guid.NewGuid();
        project.Status = ProjectStatus.SPACE_VERIFIED;
        var repository = new FakeProjectRepository(roleName: "DESIGNER", entities: [project]);
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.UpdateStatusAsync(projectId, Guid.NewGuid(), new UpdateProjectStatusRequestDto
        {
            Status = ProjectStatus.PROPOSAL_DRAFTING
        });

        Assert.Equal(403, result.Status);
        Assert.Equal(ProjectStatusErrorCodes.Forbidden, result.ErrorCode);
        Assert.Equal(ProjectStatus.SPACE_VERIFIED, project.Status);
    }

    [Fact]
    public async Task UpdateStatusAsync_WithAssignedDesignerMovingToSpaceVerified_Succeeds()
    {
        var designerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var project = CreateQualifiedProject(projectId, Guid.NewGuid());
        project.AssignedDesignerId = designerId;
        project.Status = ProjectStatus.MEASUREMENT_REQUIRED;
        var repository = new FakeProjectRepository(roleName: "DESIGNER", entities: [project]);
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.ForSaveChanges(repository.SaveChangesAsync));

        var result = await service.UpdateStatusAsync(projectId, designerId, new UpdateProjectStatusRequestDto
        {
            Status = ProjectStatus.SPACE_VERIFIED,
            Note = "Measurement completed and space verified."
        });

        Assert.Equal(200, result.Status);
        Assert.Equal(ProjectStatus.SPACE_VERIFIED, project.Status);
    }

    [Fact]
    public async Task UpdateStatusAsync_WithAssignedDesignerResumingAfterRevision_Succeeds()
    {
        var designerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var project = CreateQualifiedProject(projectId, Guid.NewGuid());
        project.AssignedDesignerId = designerId;
        project.Status = ProjectStatus.REVISION_REQUESTED;
        var repository = new FakeProjectRepository(roleName: "DESIGNER", entities: [project]);
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.ForSaveChanges(repository.SaveChangesAsync));

        var result = await service.UpdateStatusAsync(projectId, designerId, new UpdateProjectStatusRequestDto
        {
            Status = ProjectStatus.PROPOSAL_DRAFTING,
            Note = "Designer resumes drafting after customer revision request."
        });

        Assert.Equal(200, result.Status);
        Assert.Equal(ProjectStatus.PROPOSAL_DRAFTING, project.Status);
    }

    [Fact]
    public async Task UpdateStatusAsync_WithAssignedDesignerMovingToQuotation_ReturnsForbidden()
    {
        var designerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var project = CreateQualifiedProject(projectId, Guid.NewGuid());
        project.AssignedDesignerId = designerId;
        project.Status = ProjectStatus.PROPOSAL_DRAFTING;
        var repository = new FakeProjectRepository(roleName: "DESIGNER", entities: [project]);
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.UpdateStatusAsync(projectId, designerId, new UpdateProjectStatusRequestDto
        {
            Status = ProjectStatus.QUOTATION_SENT
        });

        Assert.Equal(403, result.Status);
        Assert.Equal(ProjectStatusErrorCodes.InvalidProjectStatus, result.ErrorCode);
        Assert.Equal(ProjectStatus.PROPOSAL_DRAFTING, project.Status);
    }

    [Fact]
    public async Task UpdateStatusAsync_WithAssignedDesignerMovingToInProduction_ReturnsForbidden()
    {
        var designerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var project = CreateQualifiedProject(projectId, Guid.NewGuid());
        project.AssignedDesignerId = designerId;
        project.Status = ProjectStatus.PROPOSAL_DRAFTING;
        var repository = new FakeProjectRepository(roleName: "DESIGNER", entities: [project]);
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.UpdateStatusAsync(projectId, designerId, new UpdateProjectStatusRequestDto
        {
            Status = ProjectStatus.IN_PRODUCTION
        });

        Assert.Equal(403, result.Status);
        Assert.Equal(ProjectStatusErrorCodes.InvalidProjectStatus, result.ErrorCode);
        Assert.Equal(ProjectStatus.PROPOSAL_DRAFTING, project.Status);
    }

    [Fact]
    public async Task UpdateStatusAsync_WithEmptyProjectId_ReturnsBadRequest()
    {
        var repository = new FakeProjectRepository(roleName: "SALES");
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.UpdateStatusAsync(Guid.Empty, Guid.NewGuid(), ValidStatusRequest());

        Assert.Equal(400, result.Status);
        Assert.Equal("Project id is required.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.GetByIdCallCount);
    }

    [Fact]
    public async Task UpdateStatusAsync_WithEmptyCurrentUser_ReturnsUnauthorized()
    {
        var repository = new FakeProjectRepository(roleName: "SALES");
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.UpdateStatusAsync(Guid.NewGuid(), Guid.Empty, ValidStatusRequest());

        Assert.Equal(401, result.Status);
        Assert.Equal("Authenticated account id is required.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.GetByIdCallCount);
    }

    [Fact]
    public async Task UpdateStatusAsync_WithMissingTargetStatus_ReturnsBadRequest()
    {
        var repository = new FakeProjectRepository(roleName: "SALES");
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.UpdateStatusAsync(Guid.NewGuid(), Guid.NewGuid(), new UpdateProjectStatusRequestDto());

        Assert.Equal(400, result.Status);
        Assert.Equal("Project status is required.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.GetByIdCallCount);
    }

    [Fact]
    public async Task UpdateStatusAsync_WithUnsupportedTargetStatus_ReturnsBadRequest()
    {
        var salesId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var project = CreateQualifiedProject(projectId, salesId);
        var repository = new FakeProjectRepository(roleName: "SALES", entities: [project]);
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.UpdateStatusAsync(projectId, salesId, new UpdateProjectStatusRequestDto
        {
            Status = ProjectStatus.COMPLETED
        });

        Assert.Equal(400, result.Status);
        Assert.Equal(ProjectStatusErrorCodes.InvalidProjectStatusTransition, result.ErrorCode);
        Assert.Null(result.Data);
        Assert.Equal(1, repository.GetByIdCallCount);
    }

    [Fact]
    public async Task UpdateStatusAsync_WithTooLongNote_ReturnsBadRequest()
    {
        var repository = new FakeProjectRepository(roleName: "SALES");
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.UpdateStatusAsync(Guid.NewGuid(), Guid.NewGuid(), new UpdateProjectStatusRequestDto
        {
            Status = ProjectStatus.WAITING_FOR_DESIGNER_ASSIGNMENT,
            Note = new string('N', 1001)
        });

        Assert.Equal(400, result.Status);
        Assert.Equal("Status update note must not exceed 1000 characters.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.GetByIdCallCount);
    }

    [Fact]
    public async Task UpdateStatusAsync_WithMissingProject_ReturnsNotFound()
    {
        var repository = new FakeProjectRepository(roleName: "SALES");
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.UpdateStatusAsync(Guid.NewGuid(), Guid.NewGuid(), ValidStatusRequest());

        Assert.Equal(404, result.Status);
        Assert.Equal("Project not found.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(1, repository.GetByIdCallCount);
        Assert.Equal(0, repository.GetAccountRoleNameCallCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("CUSTOMER")]
    [InlineData("DESIGNER")]
    [InlineData("SALES")]
    public async Task UpdateStatusAsync_WithUnauthorizedRoleOrSales_ReturnsForbidden(string? roleName)
    {
        var projectId = Guid.NewGuid();
        var project = CreateQualifiedProject(projectId, Guid.NewGuid());
        var repository = new FakeProjectRepository(roleName: roleName, entities: [project]);
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.UpdateStatusAsync(projectId, Guid.NewGuid(), ValidStatusRequest());

        Assert.Equal(403, result.Status);
        Assert.Null(result.Data);
        Assert.Equal(1, repository.GetByIdCallCount);
        Assert.Equal(1, repository.GetAccountRoleNameCallCount);
        Assert.Equal(0, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task UpdateStatusAsync_WithInvalidCurrentProjectStatus_ReturnsBadRequest()
    {
        var salesId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var project = CreateQualifiedProject(projectId, salesId);
        project.Status = ProjectStatus.NEED_BASIC_INFORMATION;
        var repository = new FakeProjectRepository(roleName: "SALES", entities: [project]);
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.UpdateStatusAsync(projectId, salesId, ValidStatusRequest());

        Assert.Equal(400, result.Status);
        Assert.Equal(ProjectStatusErrorCodes.InvalidProjectStatusTransition, result.ErrorCode);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task UpdateStatusAsync_ToProposalSelected_WithSelectedFinalProposal_Succeeds()
    {
        var salesId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var project = CreateQualifiedProject(projectId, salesId);
        project.Status = ProjectStatus.WAITING_FOR_CUSTOMER_REVIEW;
        var repository = new FakeProjectRepository(roleName: "SALES", entities: [project]);
        var transitionFakes = new ProjectServiceTransitionFakes
        {
            Proposals =
            {
                HasSelectedFinalProposal = true
            }
        };
        var service = ProjectServiceTestFactory.Create(
            repository,
            TestUnitOfWork.ForSaveChanges(repository.SaveChangesAsync),
            transitionFakes: transitionFakes);

        var result = await service.UpdateStatusAsync(projectId, salesId, new UpdateProjectStatusRequestDto
        {
            Status = ProjectStatus.PROPOSAL_SELECTED,
            Note = "Customer selected final proposal."
        });

        Assert.Equal(200, result.Status);
        Assert.Equal("Project moved to proposal selected successfully.", result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal(ProjectStatus.WAITING_FOR_CUSTOMER_REVIEW, result.Data.OldStatus);
        Assert.Equal(ProjectStatus.PROPOSAL_SELECTED, result.Data.NewStatus);
        Assert.Equal(ProjectStatus.PROPOSAL_SELECTED, project.Status);
        Assert.Equal(1, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task UpdateStatusAsync_ToProposalSelected_WithoutSelectedFinalProposal_ReturnsFinalProposalRequired()
    {
        var salesId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var project = CreateQualifiedProject(projectId, salesId);
        project.Status = ProjectStatus.WAITING_FOR_CUSTOMER_REVIEW;
        var repository = new FakeProjectRepository(roleName: "SALES", entities: [project]);
        var service = ProjectServiceTestFactory.Create(
            repository,
            TestUnitOfWork.Instance,
            transitionFakes: new ProjectServiceTransitionFakes());

        var result = await service.UpdateStatusAsync(projectId, salesId, new UpdateProjectStatusRequestDto
        {
            Status = ProjectStatus.PROPOSAL_SELECTED
        });

        Assert.Equal(400, result.Status);
        Assert.Equal(ProjectStatusErrorCodes.FinalProposalRequired, result.ErrorCode);
        Assert.Equal(ProjectStatus.WAITING_FOR_CUSTOMER_REVIEW, project.Status);
        Assert.Equal(0, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task UpdateStatusAsync_ToProposalSelected_WithInvalidCurrentStatus_ReturnsInvalidProjectStatus()
    {
        var salesId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var project = CreateQualifiedProject(projectId, salesId);
        project.Status = ProjectStatus.PROPOSAL_DRAFTING;
        var repository = new FakeProjectRepository(roleName: "SALES", entities: [project]);
        var transitionFakes = new ProjectServiceTransitionFakes
        {
            Proposals =
            {
                HasSelectedFinalProposal = true
            }
        };
        var service = ProjectServiceTestFactory.Create(
            repository,
            TestUnitOfWork.Instance,
            transitionFakes: transitionFakes);

        var result = await service.UpdateStatusAsync(projectId, salesId, new UpdateProjectStatusRequestDto
        {
            Status = ProjectStatus.PROPOSAL_SELECTED
        });

        Assert.Equal(400, result.Status);
        Assert.Equal(ProjectStatusErrorCodes.InvalidProjectStatus, result.ErrorCode);
        Assert.Equal(ProjectStatus.PROPOSAL_DRAFTING, project.Status);
        Assert.Equal(0, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task UpdateStatusAsync_ToProposalDrafting_WithoutCompletedMeasurement_ReturnsMeasurementNotCompleted()
    {
        var salesId = Guid.NewGuid();
        var designerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var project = CreateQualifiedProject(projectId, salesId);
        project.Status = ProjectStatus.MEASUREMENT_REQUIRED;
        project.AssignedDesignerId = designerId;
        var repository = new FakeProjectRepository(roleName: "SALES", entities: [project]);
        var transitionFakes = new ProjectServiceTransitionFakes { Schedules = { HasCompletedMeasurement = false } };
        var service = ProjectServiceTestFactory.Create(
            repository,
            TestUnitOfWork.ForSaveChanges(repository.SaveChangesAsync),
            transitionFakes: transitionFakes);

        var result = await service.UpdateStatusAsync(projectId, salesId, new UpdateProjectStatusRequestDto
        {
            Status = ProjectStatus.PROPOSAL_DRAFTING
        });

        Assert.Equal(400, result.Status);
        Assert.Equal(ProjectStatusErrorCodes.MeasurementNotCompleted, result.ErrorCode);
    }

    [Fact]
    public async Task UpdateStatusAsync_MeasurementRequiredToSpaceVerified_WithoutNote_ReturnsNoteRequired()
    {
        var salesId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var project = CreateQualifiedProject(projectId, salesId);
        project.Status = ProjectStatus.MEASUREMENT_REQUIRED;
        project.AssignedDesignerId = Guid.NewGuid();
        var repository = new FakeProjectRepository(roleName: "SALES", entities: [project]);
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.ForSaveChanges(repository.SaveChangesAsync));

        var result = await service.UpdateStatusAsync(projectId, salesId, new UpdateProjectStatusRequestDto
        {
            Status = ProjectStatus.SPACE_VERIFIED
        });

        Assert.Equal(400, result.Status);
        Assert.Equal(ProjectStatusErrorCodes.NoteRequired, result.ErrorCode);
    }

    [Fact]
    public async Task UpdateStatusAsync_MeasurementRequiredToSpaceVerified_WithNote_Succeeds()
    {
        var salesId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var project = CreateQualifiedProject(projectId, salesId);
        project.Status = ProjectStatus.MEASUREMENT_REQUIRED;
        project.AssignedDesignerId = Guid.NewGuid();
        var repository = new FakeProjectRepository(roleName: "SALES", entities: [project]);
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.ForSaveChanges(repository.SaveChangesAsync));

        var result = await service.UpdateStatusAsync(projectId, salesId, new UpdateProjectStatusRequestDto
        {
            Status = ProjectStatus.SPACE_VERIFIED,
            Note = "Customer provided floor plan."
        });

        Assert.Equal(200, result.Status);
        Assert.Equal(ProjectStatus.SPACE_VERIFIED, project.Status);
        Assert.Equal(ProjectStatus.MEASUREMENT_REQUIRED, result.Data!.OldStatus);
        Assert.Equal(ProjectStatus.SPACE_VERIFIED, result.Data.NewStatus);
    }

    [Fact]
    public async Task RejectAsync_DispatchesProjectRequestRejectedNotification()
    {
        var salesId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var project = CreateQualifiedProject(projectId, salesId);
        project.Status = ProjectStatus.IN_CONSULTATION;
        var dispatcher = new FakeNotificationDispatcher();
        var repository = new FakeProjectRepository(roleName: "SALES", entities: [project]);
        var service = ProjectServiceTestFactory.Create(
            repository,
            TestUnitOfWork.ForSaveChanges(repository.SaveChangesAsync),
            dispatcher);

        var result = await service.RejectAsync(projectId, salesId, ValidRejectRequest());

        Assert.Equal(200, result.Status);
        Assert.Equal(1, dispatcher.DispatchCallCount);
        Assert.Equal(NotificationType.ProjectRequestRejected, dispatcher.LastType);
        Assert.Equal([project.CustomerId], dispatcher.LastReceiverIds);
    }

    [Fact]
    public async Task UpdateStatusAsync_WithIncompleteBasicInformation_ReturnsValidationErrors()
    {
        var salesId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var project = CreateQualifiedProject(projectId, salesId);
        project.BusinessType = " ";
        project.FurnitureRequirement = null;
        var repository = new FakeProjectRepository(roleName: "SALES", entities: [project]);
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.UpdateStatusAsync(projectId, salesId, ValidStatusRequest());

        Assert.Equal(400, result.Status);
        Assert.Equal("Project basic information is incomplete.", result.Message);
        Assert.Contains("Business type is required.", result.Errors!);
        Assert.Contains("Furniture requirement is required.", result.Errors!);
        Assert.Equal(0, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task UpdateBasicInformationAsync_WithOwnerCustomer_UpdatesProjectWithoutChangingStatus()
    {
        var customerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var assignedSalesId = Guid.NewGuid();
        var dispatcher = new FakeNotificationDispatcher();
        var project = new Project
        {
            ProjectId = projectId,
            CustomerId = customerId,
            AssignedSalesId = assignedSalesId,
            ProjectName = "Old",
            Status = ProjectStatus.NEED_BASIC_INFORMATION
        };
        var repository = new FakeProjectRepository(roleName: "CUSTOMER", entities: [project]);
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.ForSaveChanges(repository.SaveChangesAsync), dispatcher);
        var targetDate = DateOnly.FromDateTime(DateTime.UtcNow.Date).AddDays(30);

        var result = await service.UpdateBasicInformationAsync(projectId, customerId, new UpdateProjectBasicInformationRequestDto
        {
            ProjectName = " Moc Coffee Interior Setup ",
            BusinessType = " Cafe ",
            ProjectAddress = " District 7 ",
            BusinessPurpose = " Open cafe ",
            FurnitureRequirement = " Counter, tables, chairs ",
            Description = " Updated basic information ",
            TotalAreaSqm = 80,
            NumberOfFloors = 1,
            BudgetMin = 150000000,
            BudgetMax = 250000000,
            TargetCompletionDate = targetDate
        });

        Assert.Equal(200, result.Status);
        Assert.Equal("Project basic information updated successfully.", result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal(projectId, result.Data.ProjectId);
        Assert.Equal("Moc Coffee Interior Setup", result.Data.ProjectName);
        Assert.Equal(ProjectStatus.NEED_BASIC_INFORMATION, result.Data.Status);
        Assert.NotNull(result.Data.UpdatedAt);
        Assert.Equal("Moc Coffee Interior Setup", project.ProjectName);
        Assert.Equal("Cafe", project.BusinessType);
        Assert.Equal("District 7", project.ProjectAddress);
        Assert.Equal("Open cafe", project.BusinessPurpose);
        Assert.Equal("Counter, tables, chairs", project.FurnitureRequirement);
        Assert.Equal("Updated basic information", project.Description);
        Assert.Equal(80, project.TotalAreaSqm);
        Assert.Equal(1, project.NumberOfFloors);
        Assert.Equal(150000000, project.BudgetMin);
        Assert.Equal(250000000, project.BudgetMax);
        Assert.Equal(targetDate, project.TargetCompletionDate);
        Assert.Equal(ProjectStatus.NEED_BASIC_INFORMATION, project.Status);
        Assert.Equal(1, repository.GetByIdCallCount);
        Assert.Equal(1, repository.GetAccountRoleNameCallCount);
        Assert.Equal(1, repository.SaveChangesCallCount);
        Assert.Equal(1, dispatcher.DispatchCallCount);
        Assert.Equal(NotificationType.ProjectBasicInformationUpdated, dispatcher.LastType);
        Assert.Equal([assignedSalesId], dispatcher.LastReceiverIds);
        Assert.Equal(projectId, dispatcher.LastProjectId);
        Assert.Equal("PROJECT", dispatcher.LastReferenceType);
        Assert.Equal(projectId, dispatcher.LastReferenceId);
        Assert.Equal("Moc Coffee Interior Setup", dispatcher.LastParameters!["ProjectName"]);
    }

    [Fact]
    public async Task UpdateBasicInformationAsync_WithAssignedSales_UpdatesProject()
    {
        var salesId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var project = new Project
        {
            ProjectId = projectId,
            CustomerId = Guid.NewGuid(),
            AssignedSalesId = salesId,
            ProjectName = "Old",
            Status = ProjectStatus.IN_CONSULTATION
        };
        var repository = new FakeProjectRepository(roleName: "SALES", entities: [project]);
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.UpdateBasicInformationAsync(projectId, salesId, ValidBasicInformationRequest());

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(ProjectStatus.IN_CONSULTATION, result.Data.Status);
        Assert.Equal("Moc Coffee Interior Setup", project.ProjectName);
    }

    [Fact]
    public async Task UpdateBasicInformationAsync_WithNeedBasicInformationAndNoAssignedSales_DoesNotDispatchNotification()
    {
        var customerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var dispatcher = new FakeNotificationDispatcher();
        var project = new Project
        {
            ProjectId = projectId,
            CustomerId = customerId,
            ProjectName = "Old",
            Status = ProjectStatus.NEED_BASIC_INFORMATION
        };
        var repository = new FakeProjectRepository(roleName: "CUSTOMER", entities: [project]);
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.ForSaveChanges(repository.SaveChangesAsync), dispatcher);

        var result = await service.UpdateBasicInformationAsync(projectId, customerId, ValidBasicInformationRequest());

        Assert.Equal(200, result.Status);
        Assert.Equal(0, dispatcher.DispatchCallCount);
    }

    [Fact]
    public async Task UpdateBasicInformationAsync_WhenNotificationFails_StillReturnsSuccess()
    {
        var customerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var assignedSalesId = Guid.NewGuid();
        var dispatcher = new FakeNotificationDispatcher(throwOnDispatch: true);
        var project = new Project
        {
            ProjectId = projectId,
            CustomerId = customerId,
            AssignedSalesId = assignedSalesId,
            ProjectName = "Old",
            Status = ProjectStatus.NEED_BASIC_INFORMATION
        };
        var repository = new FakeProjectRepository(roleName: "CUSTOMER", entities: [project]);
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.ForSaveChanges(repository.SaveChangesAsync), dispatcher);

        var result = await service.UpdateBasicInformationAsync(projectId, customerId, ValidBasicInformationRequest());

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(ProjectStatus.NEED_BASIC_INFORMATION, project.Status);
        Assert.Equal(1, repository.SaveChangesCallCount);
        Assert.Equal(1, dispatcher.DispatchCallCount);
    }

    [Fact]
    public async Task UpdateBasicInformationAsync_WithAdmin_UpdatesSubmittedProject()
    {
        var projectId = Guid.NewGuid();
        var project = new Project
        {
            ProjectId = projectId,
            CustomerId = Guid.NewGuid(),
            ProjectName = "Old",
            Status = ProjectStatus.SUBMITTED
        };
        var repository = new FakeProjectRepository(roleName: "ADMIN", entities: [project]);
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.ForSaveChanges(repository.SaveChangesAsync));

        var result = await service.UpdateBasicInformationAsync(projectId, Guid.NewGuid(), ValidBasicInformationRequest());

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(ProjectStatus.SUBMITTED, result.Data.Status);
        Assert.Equal(1, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task UpdateBasicInformationAsync_WithBlankOptionalFields_NormalizesToNull()
    {
        var customerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var project = new Project
        {
            ProjectId = projectId,
            CustomerId = customerId,
            ProjectName = "Old",
            Status = ProjectStatus.SUBMITTED
        };
        var repository = new FakeProjectRepository(roleName: "CUSTOMER", entities: [project]);
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);
        var request = ValidBasicInformationRequest();
        request.ProjectAddress = " ";
        request.BusinessPurpose = " ";
        request.Description = " ";

        var result = await service.UpdateBasicInformationAsync(projectId, customerId, request);

        Assert.Equal(200, result.Status);
        Assert.Null(project.ProjectAddress);
        Assert.Null(project.BusinessPurpose);
        Assert.Null(project.Description);
    }

    [Fact]
    public async Task UpdateBasicInformationAsync_WithEmptyProjectId_ReturnsBadRequest()
    {
        var repository = new FakeProjectRepository(roleName: "ADMIN");
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.UpdateBasicInformationAsync(Guid.Empty, Guid.NewGuid(), ValidBasicInformationRequest());

        Assert.Equal(400, result.Status);
        Assert.Equal("Project id is required.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.GetByIdCallCount);
        Assert.Equal(0, repository.GetAccountRoleNameCallCount);
    }

    [Fact]
    public async Task UpdateBasicInformationAsync_WithEmptyCurrentUser_ReturnsUnauthorized()
    {
        var repository = new FakeProjectRepository(roleName: "ADMIN");
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.UpdateBasicInformationAsync(Guid.NewGuid(), Guid.Empty, ValidBasicInformationRequest());

        Assert.Equal(401, result.Status);
        Assert.Equal("Authenticated account id is required.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.GetByIdCallCount);
        Assert.Equal(0, repository.GetAccountRoleNameCallCount);
    }

    [Fact]
    public async Task UpdateBasicInformationAsync_WithInvalidRequest_ReturnsValidationErrors()
    {
        var repository = new FakeProjectRepository(roleName: "ADMIN");
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.UpdateBasicInformationAsync(Guid.NewGuid(), Guid.NewGuid(), new UpdateProjectBasicInformationRequestDto
        {
            ProjectName = " ",
            BusinessType = new string('B', 101),
            FurnitureRequirement = " ",
            TotalAreaSqm = -1,
            NumberOfFloors = -1,
            BudgetMin = 100,
            BudgetMax = 10,
            TargetCompletionDate = DateOnly.FromDateTime(DateTime.UtcNow.Date).AddDays(-1)
        });

        Assert.Equal(400, result.Status);
        Assert.Equal("Validation failed", result.Message);
        Assert.Contains("Project name is required.", result.Errors!);
        Assert.Contains("Business type must not exceed 100 characters.", result.Errors!);
        Assert.Contains("Furniture requirement is required.", result.Errors!);
        Assert.Contains("Total area must be greater than or equal to zero.", result.Errors!);
        Assert.Contains("Number of floors must be greater than or equal to zero.", result.Errors!);
        Assert.Contains("Minimum budget must be less than or equal to maximum budget.", result.Errors!);
        Assert.Contains("Target completion date must not be in the past.", result.Errors!);
        Assert.Equal(0, repository.GetByIdCallCount);
    }

    [Fact]
    public async Task UpdateBasicInformationAsync_WithNegativeBudgets_ReturnsValidationErrors()
    {
        var repository = new FakeProjectRepository(roleName: "ADMIN");
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);
        var request = ValidBasicInformationRequest();
        request.BudgetMin = -1;
        request.BudgetMax = -2;

        var result = await service.UpdateBasicInformationAsync(Guid.NewGuid(), Guid.NewGuid(), request);

        Assert.Equal(400, result.Status);
        Assert.Contains("Minimum budget must be greater than or equal to zero.", result.Errors!);
        Assert.Contains("Maximum budget must be greater than or equal to zero.", result.Errors!);
        Assert.Equal(0, repository.GetByIdCallCount);
    }

    [Fact]
    public async Task UpdateBasicInformationAsync_WithTooLongProjectName_ReturnsValidationError()
    {
        var repository = new FakeProjectRepository(roleName: "ADMIN");
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);
        var request = ValidBasicInformationRequest();
        request.ProjectName = new string('P', 151);

        var result = await service.UpdateBasicInformationAsync(Guid.NewGuid(), Guid.NewGuid(), request);

        Assert.Equal(400, result.Status);
        Assert.Contains("Project name must not exceed 150 characters.", result.Errors!);
    }

    [Fact]
    public async Task UpdateBasicInformationAsync_WithMissingProject_ReturnsNotFound()
    {
        var repository = new FakeProjectRepository(roleName: "ADMIN");
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.UpdateBasicInformationAsync(Guid.NewGuid(), Guid.NewGuid(), ValidBasicInformationRequest());

        Assert.Equal(404, result.Status);
        Assert.Equal("Project not found.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(1, repository.GetByIdCallCount);
        Assert.Equal(0, repository.GetAccountRoleNameCallCount);
    }

    [Theory]
    [InlineData("CUSTOMER")]
    [InlineData("SALES")]
    [InlineData("DESIGNER")]
    [InlineData(null)]
    public async Task UpdateBasicInformationAsync_WithUnauthorizedParticipant_ReturnsForbidden(string? roleName)
    {
        var projectId = Guid.NewGuid();
        var project = new Project
        {
            ProjectId = projectId,
            CustomerId = Guid.NewGuid(),
            AssignedSalesId = Guid.NewGuid(),
            ProjectName = "Old",
            Status = ProjectStatus.IN_CONSULTATION
        };
        var repository = new FakeProjectRepository(roleName: roleName, entities: [project]);
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.UpdateBasicInformationAsync(projectId, Guid.NewGuid(), ValidBasicInformationRequest());

        Assert.Equal(403, result.Status);
        Assert.Equal("You do not have access to update this project.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(1, repository.GetByIdCallCount);
        Assert.Equal(1, repository.GetAccountRoleNameCallCount);
        Assert.Equal(0, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task UpdateBasicInformationAsync_WithNonEditableStatus_ReturnsBadRequest()
    {
        var projectId = Guid.NewGuid();
        var project = new Project
        {
            ProjectId = projectId,
            CustomerId = Guid.NewGuid(),
            ProjectName = "Old",
            Status = ProjectStatus.COMPLETED
        };
        var repository = new FakeProjectRepository(roleName: "ADMIN", entities: [project]);
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.UpdateBasicInformationAsync(projectId, Guid.NewGuid(), ValidBasicInformationRequest());

        Assert.Equal(400, result.Status);
        Assert.Equal("Project basic information cannot be updated from its current status.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task RequestInformationAsync_WithAssignedSales_UpdatesProjectStatus()
    {
        var projectId = Guid.NewGuid();
        var salesId = Guid.NewGuid();
        var project = new Project
        {
            ProjectId = projectId,
            CustomerId = Guid.NewGuid(),
            ProjectName = "Moc Coffee",
            AssignedSalesId = salesId,
            Status = ProjectStatus.IN_CONSULTATION
        };
        var repository = new FakeProjectRepository(roleName: "SALES", entities: [project]);
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.ForSaveChanges(repository.SaveChangesAsync));

        var result = await service.RequestInformationAsync(projectId, salesId, new RequestProjectInformationRequestDto
        {
            Message = "Please provide exact store dimensions."
        });

        Assert.Equal(200, result.Status);
        Assert.Equal("More information requested successfully.", result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal(projectId, result.Data.ProjectId);
        Assert.Equal(ProjectStatus.NEED_BASIC_INFORMATION, result.Data.Status);
        Assert.Equal(ProjectStatus.NEED_BASIC_INFORMATION, project.Status);
        Assert.NotNull(project.UpdatedAt);
        Assert.Equal(1, repository.GetAccountRoleNameCallCount);
        Assert.Equal(1, repository.GetByIdCallCount);
        Assert.Equal(1, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task RequestInformationAsync_WithAdmin_AllowsUnassignedProject()
    {
        var projectId = Guid.NewGuid();
        var project = new Project
        {
            ProjectId = projectId,
            CustomerId = Guid.NewGuid(),
            ProjectName = "Moc Coffee",
            Status = ProjectStatus.SUBMITTED
        };
        var repository = new FakeProjectRepository(roleName: "ADMIN", entities: [project]);
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.ForSaveChanges(repository.SaveChangesAsync));

        var result = await service.RequestInformationAsync(projectId, Guid.NewGuid(), new RequestProjectInformationRequestDto
        {
            Message = "Need more info"
        });

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(ProjectStatus.NEED_BASIC_INFORMATION, result.Data.Status);
        Assert.Equal(1, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task RequestInformationAsync_WithEmptyProjectId_ReturnsBadRequest()
    {
        var repository = new FakeProjectRepository(roleName: "SALES");
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.RequestInformationAsync(Guid.Empty, Guid.NewGuid(), new RequestProjectInformationRequestDto
        {
            Message = "Need more info"
        });

        Assert.Equal(400, result.Status);
        Assert.Equal("Project id is required.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.GetAccountRoleNameCallCount);
        Assert.Equal(0, repository.GetByIdCallCount);
    }

    [Fact]
    public async Task RequestInformationAsync_WithEmptyCurrentUser_ReturnsUnauthorized()
    {
        var repository = new FakeProjectRepository(roleName: "SALES");
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.RequestInformationAsync(Guid.NewGuid(), Guid.Empty, new RequestProjectInformationRequestDto
        {
            Message = "Need more info"
        });

        Assert.Equal(401, result.Status);
        Assert.Equal("Authenticated account id is required.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.GetAccountRoleNameCallCount);
        Assert.Equal(0, repository.GetByIdCallCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task RequestInformationAsync_WithMissingMessage_ReturnsBadRequest(string message)
    {
        var repository = new FakeProjectRepository(roleName: "SALES");
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.RequestInformationAsync(Guid.NewGuid(), Guid.NewGuid(), new RequestProjectInformationRequestDto
        {
            Message = message
        });

        Assert.Equal(400, result.Status);
        Assert.Equal("Request message is required.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.GetAccountRoleNameCallCount);
        Assert.Equal(0, repository.GetByIdCallCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("CUSTOMER")]
    [InlineData("DESIGNER")]
    public async Task RequestInformationAsync_WithUnsupportedRole_ReturnsForbidden(string? roleName)
    {
        var repository = new FakeProjectRepository(roleName: roleName);
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.RequestInformationAsync(Guid.NewGuid(), Guid.NewGuid(), new RequestProjectInformationRequestDto
        {
            Message = "Need more info"
        });

        Assert.Equal(403, result.Status);
        Assert.Equal("Only assigned sales or admin accounts can request more information.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(1, repository.GetAccountRoleNameCallCount);
        Assert.Equal(0, repository.GetByIdCallCount);
    }

    [Fact]
    public async Task RequestInformationAsync_WithMissingProject_ReturnsNotFound()
    {
        var repository = new FakeProjectRepository(roleName: "SALES");
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.RequestInformationAsync(Guid.NewGuid(), Guid.NewGuid(), new RequestProjectInformationRequestDto
        {
            Message = "Need more info"
        });

        Assert.Equal(404, result.Status);
        Assert.Equal("Project not found.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(1, repository.GetAccountRoleNameCallCount);
        Assert.Equal(1, repository.GetByIdCallCount);
        Assert.Equal(0, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task RequestInformationAsync_WithUnassignedSales_ReturnsForbidden()
    {
        var projectId = Guid.NewGuid();
        var project = new Project
        {
            ProjectId = projectId,
            CustomerId = Guid.NewGuid(),
            ProjectName = "Moc Coffee",
            AssignedSalesId = Guid.NewGuid(),
            Status = ProjectStatus.IN_CONSULTATION
        };
        var repository = new FakeProjectRepository(roleName: "SALES", entities: [project]);
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.ForSaveChanges(repository.SaveChangesAsync));

        var result = await service.RequestInformationAsync(projectId, Guid.NewGuid(), new RequestProjectInformationRequestDto
        {
            Message = "Need more info"
        });

        Assert.Equal(403, result.Status);
        Assert.Equal("You do not have access to request more information for this project.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task AssignSalesAsync_WithSalesRole_AssignsCurrentSalesAndStartsConsultation()
    {
        var projectId = Guid.NewGuid();
        var salesId = Guid.NewGuid();
        var project = new Project
        {
            ProjectId = projectId,
            CustomerId = Guid.NewGuid(),
            ProjectName = "Moc Coffee",
            Status = ProjectStatus.SUBMITTED
        };
        var repository = new FakeProjectRepository(roleName: "SALES", entities: [project]);
        var projectChats = new FakeProjectChatService();
        var service = ProjectServiceTestFactory.Create(
            repository,
            TestUnitOfWork.ForSaveChanges(repository.SaveChangesAsync),
            projectChats: projectChats);

        var result = await service.AssignSalesAsync(projectId, salesId, new AssignProjectSalesRequestDto
        {
            Note = "Accepted for consultation."
        });

        Assert.Equal(200, result.Status);
        Assert.Equal("Project request accepted successfully.", result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal(projectId, result.Data.ProjectId);
        Assert.Equal(salesId, result.Data.AssignedSalesId);
        Assert.Equal(ProjectStatus.IN_CONSULTATION, result.Data.Status);
        Assert.NotNull(result.Data.SalesAssignedAt);
        Assert.NotNull(result.Data.SalesChat);
        Assert.Equal(projectChats.Summary.ChatId, result.Data.SalesChat.ChatId);
        Assert.Equal("SALES", result.Data.SalesChat.ChatType);
        Assert.Equal(salesId, result.Data.SalesChat.StaffId);
        Assert.Equal(salesId, project.AssignedSalesId);
        Assert.Equal(ProjectStatus.IN_CONSULTATION, project.Status);
        Assert.NotNull(project.SalesAssignedAt);
        Assert.Equal(1, repository.GetAccountRoleNameCallCount);
        Assert.Equal(1, repository.GetByIdCallCount);
        Assert.Equal(1, repository.SaveChangesCallCount);
        Assert.Equal(1, projectChats.UpsertCallCount);
        Assert.Equal(projectId, projectChats.ProjectId);
        Assert.Equal(ProjectChatType.SALES, projectChats.ChatType);
        Assert.Equal(salesId, projectChats.StaffId);
        Assert.Equal("Sales Consultation", projectChats.Title);
    }

    [Fact]
    public async Task AssignSalesAsync_AfterSuccessfulAssignment_DispatchesAcceptedNotificationToCustomer()
    {
        var projectId = Guid.NewGuid();
        var salesId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var dispatcher = new FakeNotificationDispatcher();
        var project = new Project
        {
            ProjectId = projectId,
            CustomerId = customerId,
            ProjectName = "Moc Coffee",
            Status = ProjectStatus.SUBMITTED
        };
        var repository = new FakeProjectRepository(roleName: "SALES", entities: [project]);
        var projectChats = new FakeProjectChatService();
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.ForSaveChanges(repository.SaveChangesAsync), dispatcher, projectChats: projectChats);

        var result = await service.AssignSalesAsync(projectId, salesId, new AssignProjectSalesRequestDto());

        Assert.Equal(200, result.Status);
        Assert.Equal(1, repository.SaveChangesCallCount);
        Assert.Equal(1, dispatcher.DispatchCallCount);
        Assert.Equal(NotificationType.ProjectRequestAccepted, dispatcher.LastType);
        Assert.Equal([customerId], dispatcher.LastReceiverIds);
        Assert.Equal(projectId, dispatcher.LastProjectId);
        Assert.Equal("PROJECT", dispatcher.LastReferenceType);
        Assert.Equal(projectId, dispatcher.LastReferenceId);
        Assert.NotNull(dispatcher.LastParameters);
        Assert.Equal("Moc Coffee", dispatcher.LastParameters["ProjectName"]);
        Assert.Equal(1, projectChats.UpsertCallCount);
    }

    [Fact]
    public async Task AssignSalesAsync_WhenAcceptedNotificationFails_StillAssignsSales()
    {
        var projectId = Guid.NewGuid();
        var salesId = Guid.NewGuid();
        var project = new Project
        {
            ProjectId = projectId,
            CustomerId = Guid.NewGuid(),
            ProjectName = "Moc Coffee",
            Status = ProjectStatus.SUBMITTED
        };
        var repository = new FakeProjectRepository(roleName: "SALES", entities: [project]);
        var dispatcher = new FakeNotificationDispatcher(throwOnDispatch: true);
        var projectChats = new FakeProjectChatService();
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.ForSaveChanges(repository.SaveChangesAsync), dispatcher, projectChats: projectChats);

        var result = await service.AssignSalesAsync(projectId, salesId, new AssignProjectSalesRequestDto());

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(salesId, project.AssignedSalesId);
        Assert.Equal(ProjectStatus.IN_CONSULTATION, project.Status);
        Assert.Equal(1, repository.SaveChangesCallCount);
        Assert.Equal(1, dispatcher.DispatchCallCount);
        Assert.Equal(1, projectChats.UpsertCallCount);
    }

    [Fact]
    public async Task AssignSalesAsync_WhenSuccessful_CommitsAssignmentAndChatTransaction()
    {
        var projectId = Guid.NewGuid();
        var salesId = Guid.NewGuid();
        var project = new Project
        {
            ProjectId = projectId,
            CustomerId = Guid.NewGuid(),
            ProjectName = "Moc Coffee",
            Status = ProjectStatus.SUBMITTED
        };
        var repository = new FakeProjectRepository(roleName: "SALES", entities: [project]);
        var projectChats = new FakeProjectChatService();
        var beginCallCount = 0;
        var commitCallCount = 0;
        var rollbackCallCount = 0;
        var dispatcher = new FakeNotificationDispatcher(
            onDispatch: () => Assert.Equal(1, commitCallCount));
        var unitOfWork = TestUnitOfWork.ForTransaction(
            _ =>
            {
                beginCallCount++;
                return Task.CompletedTask;
            },
            repository.SaveChangesAsync,
            _ =>
            {
                commitCallCount++;
                return Task.CompletedTask;
            },
            _ =>
            {
                rollbackCallCount++;
                return Task.CompletedTask;
            });
        var service = ProjectServiceTestFactory.Create(repository, unitOfWork, dispatcher, projectChats: projectChats);

        var result = await service.AssignSalesAsync(
            projectId,
            salesId,
            new AssignProjectSalesRequestDto());

        Assert.Equal(200, result.Status);
        Assert.Equal(1, beginCallCount);
        Assert.Equal(1, repository.SaveChangesCallCount);
        Assert.Equal(1, commitCallCount);
        Assert.Equal(0, rollbackCallCount);
        Assert.Equal(1, projectChats.UpsertCallCount);
        Assert.Equal(1, dispatcher.DispatchCallCount);
    }

    [Fact]
    public async Task AssignSalesAsync_WhenChatUpsertFails_RollsBackAndDoesNotNotify()
    {
        var projectId = Guid.NewGuid();
        var salesId = Guid.NewGuid();
        var project = new Project
        {
            ProjectId = projectId,
            CustomerId = Guid.NewGuid(),
            ProjectName = "Moc Coffee",
            Status = ProjectStatus.SUBMITTED
        };
        var repository = new FakeProjectRepository(roleName: "SALES", entities: [project]);
        var projectChats = new FakeProjectChatService(throwOnUpsert: true);
        var dispatcher = new FakeNotificationDispatcher();
        var beginCallCount = 0;
        var commitCallCount = 0;
        var rollbackCallCount = 0;
        var unitOfWork = TestUnitOfWork.ForTransaction(
            _ =>
            {
                beginCallCount++;
                return Task.CompletedTask;
            },
            repository.SaveChangesAsync,
            _ =>
            {
                commitCallCount++;
                return Task.CompletedTask;
            },
            _ =>
            {
                rollbackCallCount++;
                return Task.CompletedTask;
            });
        var service = ProjectServiceTestFactory.Create(repository, unitOfWork, dispatcher, projectChats: projectChats);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AssignSalesAsync(projectId, salesId, new AssignProjectSalesRequestDto()));

        Assert.Equal("Project chat upsert failed.", exception.Message);
        Assert.Equal(1, beginCallCount);
        Assert.Equal(1, projectChats.UpsertCallCount);
        Assert.Equal(0, repository.SaveChangesCallCount);
        Assert.Equal(0, commitCallCount);
        Assert.Equal(1, rollbackCallCount);
        Assert.Equal(0, dispatcher.DispatchCallCount);
    }

    [Fact]
    public async Task RequestInformationAsync_AfterSuccessfulUpdate_DispatchesMoreInformationNotificationToCustomer()
    {
        var projectId = Guid.NewGuid();
        var salesId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var dispatcher = new FakeNotificationDispatcher();
        var project = new Project
        {
            ProjectId = projectId,
            CustomerId = customerId,
            AssignedSalesId = salesId,
            ProjectName = "Moc Coffee",
            Status = ProjectStatus.IN_CONSULTATION
        };
        var repository = new FakeProjectRepository(roleName: "SALES", entities: [project]);
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.ForSaveChanges(repository.SaveChangesAsync), dispatcher);

        var result = await service.RequestInformationAsync(projectId, salesId, new RequestProjectInformationRequestDto
        {
            Message = "Please provide exact dimensions."
        });

        Assert.Equal(200, result.Status);
        Assert.Equal(1, repository.SaveChangesCallCount);
        Assert.Equal(1, dispatcher.DispatchCallCount);
        Assert.Equal(NotificationType.ProjectMoreInformationRequested, dispatcher.LastType);
        Assert.Equal([customerId], dispatcher.LastReceiverIds);
        Assert.Equal(projectId, dispatcher.LastProjectId);
        Assert.Equal("PROJECT", dispatcher.LastReferenceType);
        Assert.Equal(projectId, dispatcher.LastReferenceId);
        Assert.NotNull(dispatcher.LastParameters);
        Assert.Equal("Moc Coffee", dispatcher.LastParameters["ProjectName"]);
    }

    [Fact]
    public async Task RequestInformationAsync_WhenNotificationFails_StillReturnsSuccess()
    {
        var projectId = Guid.NewGuid();
        var salesId = Guid.NewGuid();
        var project = new Project
        {
            ProjectId = projectId,
            CustomerId = Guid.NewGuid(),
            AssignedSalesId = salesId,
            ProjectName = "Moc Coffee",
            Status = ProjectStatus.IN_CONSULTATION
        };
        var repository = new FakeProjectRepository(roleName: "SALES", entities: [project]);
        var dispatcher = new FakeNotificationDispatcher(throwOnDispatch: true);
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.ForSaveChanges(repository.SaveChangesAsync), dispatcher);

        var result = await service.RequestInformationAsync(projectId, salesId, new RequestProjectInformationRequestDto
        {
            Message = "Please provide exact dimensions."
        });

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(ProjectStatus.NEED_BASIC_INFORMATION, project.Status);
        Assert.Equal(1, repository.SaveChangesCallCount);
        Assert.Equal(1, dispatcher.DispatchCallCount);
    }

    [Fact]
    public async Task AssignSalesAsync_WithAdminRole_OverridesExistingSalesAssignment()
    {
        var projectId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var project = new Project
        {
            ProjectId = projectId,
            CustomerId = Guid.NewGuid(),
            ProjectName = "Moc Coffee",
            AssignedSalesId = Guid.NewGuid(),
            Status = ProjectStatus.NEED_BASIC_INFORMATION
        };
        var repository = new FakeProjectRepository(roleName: "ADMIN", entities: [project]);
        var projectChats = new FakeProjectChatService();
        var service = ProjectServiceTestFactory.Create(
            repository,
            TestUnitOfWork.ForSaveChanges(repository.SaveChangesAsync),
            projectChats: projectChats);

        var result = await service.AssignSalesAsync(projectId, adminId, new AssignProjectSalesRequestDto());

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(adminId, result.Data.AssignedSalesId);
        Assert.Equal(ProjectStatus.IN_CONSULTATION, result.Data.Status);
        Assert.Equal(1, repository.SaveChangesCallCount);
        Assert.Equal(adminId, projectChats.StaffId);
    }

    [Fact]
    public async Task AssignSalesAsync_WithSameSales_ReacceptsProject()
    {
        var projectId = Guid.NewGuid();
        var salesId = Guid.NewGuid();
        var project = new Project
        {
            ProjectId = projectId,
            CustomerId = Guid.NewGuid(),
            ProjectName = "Moc Coffee",
            AssignedSalesId = salesId,
            Status = ProjectStatus.SUBMITTED
        };
        var repository = new FakeProjectRepository(roleName: "SALES", entities: [project]);
        var projectChats = new FakeProjectChatService();
        var service = ProjectServiceTestFactory.Create(
            repository,
            TestUnitOfWork.Instance,
            projectChats: projectChats);

        var result = await service.AssignSalesAsync(projectId, salesId, new AssignProjectSalesRequestDto());

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(salesId, result.Data.AssignedSalesId);
        Assert.Equal(1, projectChats.UpsertCallCount);
    }

    [Fact]
    public async Task AssignSalesAsync_WithEmptyProjectId_ReturnsBadRequest()
    {
        var repository = new FakeProjectRepository(roleName: "SALES");
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.AssignSalesAsync(Guid.Empty, Guid.NewGuid(), new AssignProjectSalesRequestDto());

        Assert.Equal(400, result.Status);
        Assert.Equal("Project id is required.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.GetAccountRoleNameCallCount);
        Assert.Equal(0, repository.GetByIdCallCount);
    }

    [Fact]
    public async Task AssignSalesAsync_WithEmptyCurrentUser_ReturnsUnauthorized()
    {
        var repository = new FakeProjectRepository(roleName: "SALES");
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.AssignSalesAsync(Guid.NewGuid(), Guid.Empty, new AssignProjectSalesRequestDto());

        Assert.Equal(401, result.Status);
        Assert.Equal("Authenticated account id is required.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.GetAccountRoleNameCallCount);
        Assert.Equal(0, repository.GetByIdCallCount);
    }

    [Fact]
    public async Task AssignSalesAsync_WithTooLongNote_ReturnsBadRequest()
    {
        var repository = new FakeProjectRepository(roleName: "SALES");
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.AssignSalesAsync(Guid.NewGuid(), Guid.NewGuid(), new AssignProjectSalesRequestDto
        {
            Note = new string('N', 1001)
        });

        Assert.Equal(400, result.Status);
        Assert.Equal("Assignment note must not exceed 1000 characters.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.GetAccountRoleNameCallCount);
        Assert.Equal(0, repository.GetByIdCallCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("CUSTOMER")]
    [InlineData("DESIGNER")]
    public async Task AssignSalesAsync_WithUnsupportedRole_ReturnsForbidden(string? roleName)
    {
        var repository = new FakeProjectRepository(roleName: roleName);
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.AssignSalesAsync(Guid.NewGuid(), Guid.NewGuid(), new AssignProjectSalesRequestDto());

        Assert.Equal(403, result.Status);
        Assert.Equal("Only sales or admin accounts can accept project requests.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(1, repository.GetAccountRoleNameCallCount);
        Assert.Equal(0, repository.GetByIdCallCount);
    }

    [Fact]
    public async Task AssignSalesAsync_WithMissingProject_ReturnsNotFound()
    {
        var repository = new FakeProjectRepository(roleName: "SALES");
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.AssignSalesAsync(Guid.NewGuid(), Guid.NewGuid(), new AssignProjectSalesRequestDto());

        Assert.Equal(404, result.Status);
        Assert.Equal("Project not found.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(1, repository.GetAccountRoleNameCallCount);
        Assert.Equal(1, repository.GetByIdCallCount);
        Assert.Equal(0, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task AssignSalesAsync_WithInvalidStatus_ReturnsBadRequest()
    {
        var projectId = Guid.NewGuid();
        var project = new Project
        {
            ProjectId = projectId,
            CustomerId = Guid.NewGuid(),
            ProjectName = "Moc Coffee",
            Status = ProjectStatus.IN_CONSULTATION
        };
        var repository = new FakeProjectRepository(roleName: "SALES", entities: [project]);
        var projectChats = new FakeProjectChatService();
        var service = ProjectServiceTestFactory.Create(
            repository,
            TestUnitOfWork.Instance,
            projectChats: projectChats);

        var result = await service.AssignSalesAsync(projectId, Guid.NewGuid(), new AssignProjectSalesRequestDto());

        Assert.Equal(400, result.Status);
        Assert.Equal("Project cannot be accepted from its current status.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.SaveChangesCallCount);
        Assert.Equal(0, projectChats.UpsertCallCount);
    }

    [Fact]
    public async Task AssignSalesAsync_WhenAssignedToAnotherSales_ReturnsConflict()
    {
        var projectId = Guid.NewGuid();
        var project = new Project
        {
            ProjectId = projectId,
            CustomerId = Guid.NewGuid(),
            ProjectName = "Moc Coffee",
            AssignedSalesId = Guid.NewGuid(),
            Status = ProjectStatus.SUBMITTED
        };
        var repository = new FakeProjectRepository(roleName: "SALES", entities: [project]);
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.AssignSalesAsync(projectId, Guid.NewGuid(), new AssignProjectSalesRequestDto());

        Assert.Equal(409, result.Status);
        Assert.Equal("Project is already assigned to another sales account.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task GetByIdAsync_WithAdminRole_ReturnsProjectDetail()
    {
        var projectId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var repository = new FakeProjectRepository(
            roleName: "ADMIN",
            detail: CreateProjectDetail(projectId, customerId));
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.GetByIdAsync(projectId, Guid.NewGuid());

        Assert.Equal(200, result.Status);
        Assert.Equal("Project detail retrieved successfully.", result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal(projectId, result.Data.ProjectId);
        Assert.Equal(customerId, result.Data.CustomerId);
        Assert.Equal("PRJ-2026-0001", result.Data.ProjectCode);
        Assert.Equal("Moc Coffee", result.Data.ProjectName);
        Assert.Equal("Cafe", result.Data.BusinessType);
        Assert.Equal("District 7", result.Data.ProjectAddress);
        Assert.Equal("Open cafe", result.Data.BusinessPurpose);
        Assert.Equal("Tables", result.Data.FurnitureRequirement);
        Assert.Equal("Warm design", result.Data.Description);
        Assert.Equal(80, result.Data.TotalAreaSqm);
        Assert.Equal(1, result.Data.NumberOfFloors);
        Assert.Equal(150000000, result.Data.BudgetMin);
        Assert.Equal(250000000, result.Data.BudgetMax);
        Assert.Equal(ProjectStatus.SUBMITTED, result.Data.Status);
        Assert.Equal(1, repository.GetDetailCallCount);
        Assert.Equal(1, repository.GetAccountRoleNameCallCount);
    }

    [Fact]
    public async Task GetByIdAsync_WithOwnerCustomer_ReturnsProjectDetail()
    {
        var projectId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var repository = new FakeProjectRepository(
            roleName: "CUSTOMER",
            detail: CreateProjectDetail(projectId, customerId));
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.GetByIdAsync(projectId, customerId);

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(customerId, result.Data.CustomerId);
    }

    [Fact]
    public async Task GetByIdAsync_WithAssignedSales_ReturnsProjectDetail()
    {
        var projectId = Guid.NewGuid();
        var salesId = Guid.NewGuid();
        var detail = CreateProjectDetail(projectId, Guid.NewGuid());
        detail.AssignedSalesId = salesId;
        var repository = new FakeProjectRepository(roleName: "SALES", detail: detail);
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.GetByIdAsync(projectId, salesId);

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(salesId, result.Data.AssignedSalesId);
    }

    [Fact]
    public async Task GetByIdAsync_WithSalesRoleAndUnassignedProject_ReturnsProjectDetail()
    {
        var projectId = Guid.NewGuid();
        var detail = CreateProjectDetail(projectId, Guid.NewGuid());
        detail.AssignedSalesId = null;
        detail.AssignedDesignerId = null;
        var repository = new FakeProjectRepository(roleName: "SALES", detail: detail);
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.GetByIdAsync(projectId, Guid.NewGuid());

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Null(result.Data.AssignedSalesId);
        Assert.Null(result.Data.AssignedDesignerId);
    }

    [Fact]
    public async Task GetByIdAsync_WithAssignedDesigner_ReturnsProjectDetail()
    {
        var projectId = Guid.NewGuid();
        var designerId = Guid.NewGuid();
        var detail = CreateProjectDetail(projectId, Guid.NewGuid());
        detail.AssignedDesignerId = designerId;
        var repository = new FakeProjectRepository(roleName: "DESIGNER", detail: detail);
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.GetByIdAsync(projectId, designerId);

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(designerId, result.Data.AssignedDesignerId);
    }

    [Fact]
    public async Task GetByIdAsync_WithEmptyProjectId_ReturnsBadRequest()
    {
        var repository = new FakeProjectRepository(roleName: "ADMIN");
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.GetByIdAsync(Guid.Empty, Guid.NewGuid());

        Assert.Equal(400, result.Status);
        Assert.Equal("Project id is required.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.GetDetailCallCount);
    }

    [Fact]
    public async Task GetByIdAsync_WithEmptyCurrentUser_ReturnsUnauthorized()
    {
        var repository = new FakeProjectRepository(roleName: "ADMIN");
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.GetByIdAsync(Guid.NewGuid(), Guid.Empty);

        Assert.Equal(401, result.Status);
        Assert.Equal("Authenticated account id is required.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.GetDetailCallCount);
    }

    [Fact]
    public async Task GetByIdAsync_WithMissingProject_ReturnsNotFound()
    {
        var repository = new FakeProjectRepository(roleName: "ADMIN");
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.GetByIdAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(404, result.Status);
        Assert.Equal("Project not found.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(1, repository.GetDetailCallCount);
        Assert.Equal(0, repository.GetAccountRoleNameCallCount);
    }

    [Theory]
    [InlineData("CUSTOMER")]
    [InlineData("DESIGNER")]
    [InlineData(null)]
    public async Task GetByIdAsync_WithUnauthorizedParticipant_ReturnsForbidden(string? roleName)
    {
        var projectId = Guid.NewGuid();
        var repository = new FakeProjectRepository(
            roleName: roleName,
            detail: CreateProjectDetail(projectId, Guid.NewGuid()));
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.GetByIdAsync(projectId, Guid.NewGuid());

        Assert.Equal(403, result.Status);
        Assert.Equal("You do not have access to view this project.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(1, repository.GetDetailCallCount);
        Assert.Equal(1, repository.GetAccountRoleNameCallCount);
    }

    [Fact]
    public async Task GetListAsync_WhenElasticsearchUnavailable_FallsBackToRepository()
    {
        var projectId = Guid.NewGuid();
        var repository = new FakeProjectRepository(
            roleName: "ADMIN",
            listItems:
            [
                new ProjectListItemReadModel
                {
                    ProjectId = projectId,
                    ProjectCode = "PRJ-2026-0099",
                    ProjectName = "Oak Workspace",
                    Status = ProjectStatus.SUBMITTED,
                    CustomerId = Guid.NewGuid(),
                    SubmittedAt = DateTime.UtcNow
                }
            ]);
        var service = ProjectServiceTestFactory.Create(
            repository,
            TestUnitOfWork.Instance,
            search: new ThrowingSearchIndexService());

        var result = await service.GetListAsync(Guid.NewGuid(), new ProjectListQueryDto
        {
            Search = "Oak",
            Page = 1,
            Limit = 20
        });

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(1, result.Data!.Total);
        Assert.Equal(projectId, Assert.Single(result.Data.Items).ProjectId);
        Assert.Equal(1, repository.GetListCallCount);
        Assert.Equal(1, repository.CountCallCount);
    }

    [Fact]
    public async Task GetListAsync_WithSalesRole_ReturnsFilteredProjectQueue()
    {
        var salesId = Guid.NewGuid();
        var designerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var repository = new FakeProjectRepository(
            roleName: "SALES",
            listItems:
            [
                new ProjectListItemReadModel
                {
                    ProjectId = projectId,
                    ProjectCode = "PRJ-2026-0001",
                    ProjectName = "Moc Coffee",
                    BusinessType = "Cafe",
                    Status = ProjectStatus.SUBMITTED,
                    CustomerId = Guid.NewGuid(),
                    AssignedSalesId = salesId,
                    AssignedDesignerId = designerId,
                    SubmittedAt = DateTime.UtcNow
                }
            ]);
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.GetListAsync(Guid.NewGuid(), new ProjectListQueryDto
        {
            Status = ProjectStatus.SUBMITTED,
            AssignedSalesId = salesId,
            AssignedDesignerId = designerId,
            Search = "moc",
            Page = 1,
            Limit = 20
        });

        Assert.Equal(200, result.Status);
        Assert.Equal("Project request queue retrieved successfully.", result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal(1, result.Data.Page);
        Assert.Equal(20, result.Data.Limit);
        Assert.Equal(1, result.Data.Total);
        var item = Assert.Single(result.Data.Items);
        Assert.Equal(projectId, item.ProjectId);
        Assert.Equal("PRJ-2026-0001", item.ProjectCode);
        Assert.Equal("Moc Coffee", item.ProjectName);
        Assert.Equal(ProjectStatus.SUBMITTED, item.Status);
        Assert.Equal(salesId, item.AssignedSalesId);
        Assert.Equal(1, repository.GetAccountRoleNameCallCount);
        Assert.Equal(1, repository.GetListCallCount);
        Assert.Equal(1, repository.CountCallCount);
        Assert.Null(repository.LastListQuery!.CustomerId);
        Assert.Equal(ProjectStatus.SUBMITTED, repository.LastListQuery.Status);
        Assert.Equal(salesId, repository.LastListQuery.AssignedSalesId);
        Assert.Equal(designerId, repository.LastListQuery.AssignedDesignerId);
        Assert.Equal("moc", repository.LastListQuery.Search);
    }

    [Fact]
    public async Task GetListAsync_WithCustomerRole_RestrictsToCurrentCustomer()
    {
        var customerId = Guid.NewGuid();
        var repository = new FakeProjectRepository(roleName: "customer");
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.GetListAsync(customerId, new ProjectListQueryDto());

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data.Items);
        Assert.Equal(customerId, repository.LastListQuery!.CustomerId);
    }

    [Fact]
    public async Task GetListAsync_WithDesignerRole_RestrictsToAssignedProjects()
    {
        var designerId = Guid.NewGuid();
        var assignedSalesId = Guid.NewGuid();
        var repository = new FakeProjectRepository(roleName: "designer");
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.GetListAsync(designerId, new ProjectListQueryDto
        {
            AssignedDesignerId = Guid.NewGuid(),
            AssignedSalesId = assignedSalesId,
            Status = ProjectStatus.SPACE_VERIFIED,
            Search = "coffee",
            Page = 2,
            Limit = 10
        });

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(designerId, repository.LastListQuery!.AssignedDesignerId);
        Assert.Equal(assignedSalesId, repository.LastListQuery.AssignedSalesId);
        Assert.Equal(ProjectStatus.SPACE_VERIFIED, repository.LastListQuery.Status);
        Assert.Equal("coffee", repository.LastListQuery.Search);
        Assert.Equal(2, repository.LastListQuery.Page);
        Assert.Equal(10, repository.LastListQuery.Limit);
        Assert.Null(repository.LastListQuery.CustomerId);
    }

    [Fact]
    public async Task GetListAsync_WithAdminRole_UsesDefaultPagination()
    {
        var repository = new FakeProjectRepository(roleName: "ADMIN");
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.GetListAsync(Guid.NewGuid(), new ProjectListQueryDto());

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(1, result.Data.Page);
        Assert.Equal(20, result.Data.Limit);
        Assert.Null(repository.LastListQuery!.CustomerId);
    }

    [Fact]
    public async Task GetListAsync_WithEmptyCurrentUser_ReturnsUnauthorized()
    {
        var repository = new FakeProjectRepository(roleName: "ADMIN");
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.GetListAsync(Guid.Empty, new ProjectListQueryDto());

        Assert.Equal(401, result.Status);
        Assert.Equal("Authenticated account id is required.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.GetAccountRoleNameCallCount);
        Assert.Equal(0, repository.GetListCallCount);
    }

    [Theory]
    [InlineData(0, 20, "Page must be greater than zero.")]
    [InlineData(1, 0, "Limit must be between 1 and 100.")]
    [InlineData(1, 101, "Limit must be between 1 and 100.")]
    public async Task GetListAsync_WithInvalidPagination_ReturnsBadRequest(int page, int limit, string message)
    {
        var repository = new FakeProjectRepository(roleName: "ADMIN");
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.GetListAsync(Guid.NewGuid(), new ProjectListQueryDto
        {
            Page = page,
            Limit = limit
        });

        Assert.Equal(400, result.Status);
        Assert.Equal(message, result.Message);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.GetAccountRoleNameCallCount);
        Assert.Equal(0, repository.GetListCallCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task GetListAsync_WithUnsupportedRole_ReturnsForbidden(string? roleName)
    {
        var repository = new FakeProjectRepository(roleName: roleName);
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.GetListAsync(Guid.NewGuid(), new ProjectListQueryDto());

        Assert.Equal(403, result.Status);
        Assert.Equal("You do not have access to view project requests.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(1, repository.GetAccountRoleNameCallCount);
        Assert.Equal(0, repository.GetListCallCount);
    }

    [Fact]
    public async Task CreateAsync_WithValidCustomerRequest_CreatesSubmittedProject()
    {
        var customerId = Guid.NewGuid();
        var repository = new FakeProjectRepository(roleName: "CUSTOMER", submittedCount: 3);
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.ForSaveChanges(repository.SaveChangesAsync));
        var targetDate = DateOnly.FromDateTime(DateTime.UtcNow.Date).AddDays(30);

        var result = await service.CreateAsync(customerId, new CreateProjectRequestDto
        {
            ProjectName = " Moc Coffee Interior Setup ",
            BusinessType = " Cafe ",
            ProjectAddress = " District 7, Ho Chi Minh City ",
            BusinessPurpose = " Open a small coffee shop ",
            FurnitureRequirement = " Need counter and tables ",
            Description = " Warm minimalist cafe interior ",
            TotalAreaSqm = 80,
            NumberOfFloors = 1,
            BudgetMin = 150000000,
            BudgetMax = 250000000,
            TargetCompletionDate = targetDate
        });

        Assert.Equal(201, result.Status);
        Assert.Equal("Project request submitted successfully.", result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal(customerId, result.Data.CustomerId);
        Assert.Equal("PRJ-" + DateTime.UtcNow.Year + "-0004", result.Data.ProjectCode);
        Assert.Equal("Moc Coffee Interior Setup", result.Data.ProjectName);
        Assert.Equal("Cafe", result.Data.BusinessType);
        Assert.Equal("District 7, Ho Chi Minh City", result.Data.ProjectAddress);
        Assert.Equal("Open a small coffee shop", result.Data.BusinessPurpose);
        Assert.Equal("Need counter and tables", result.Data.FurnitureRequirement);
        Assert.Equal("Warm minimalist cafe interior", result.Data.Description);
        Assert.Equal(80, result.Data.TotalAreaSqm);
        Assert.Equal(1, result.Data.NumberOfFloors);
        Assert.Equal(150000000, result.Data.BudgetMin);
        Assert.Equal(250000000, result.Data.BudgetMax);
        Assert.Equal(targetDate, result.Data.TargetCompletionDate);
        Assert.Equal(ProjectStatus.SUBMITTED, result.Data.Status);
        Assert.NotNull(result.Data.SubmittedAt);
        Assert.Null(result.Data.AssignedSalesId);
        Assert.Null(result.Data.AssignedDesignerId);
        Assert.Equal(1, repository.GetAccountRoleNameCallCount);
        Assert.Equal(1, repository.CountSubmittedInYearCallCount);
        Assert.Equal(1, repository.AddCallCount);
        Assert.Equal(1, repository.SaveChangesCallCount);
        var project = Assert.Single(repository.Projects);
        Assert.Equal(result.Data.ProjectId, project.ProjectId);
    }

    [Fact]
    public async Task CreateAsync_AfterSuccessfulCreate_DispatchesProjectSubmittedNotification()
    {
        var customerId = Guid.NewGuid();
        var salesId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var dispatcher = new FakeNotificationDispatcher();
        var repository = new FakeProjectRepository(
            roleName: "CUSTOMER",
            receiverIds: [salesId, adminId],
            accountFullName: "Moc Owner");
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.ForSaveChanges(repository.SaveChangesAsync), dispatcher);

        var result = await service.CreateAsync(customerId, ValidRequest());

        Assert.Equal(201, result.Status);
        Assert.Equal(1, repository.AddCallCount);
        Assert.Equal(1, repository.SaveChangesCallCount);
        Assert.Equal(1, repository.GetActiveAccountIdsByRoleNamesCallCount);
        Assert.Equal(1, repository.GetAccountFullNameCallCount);
        Assert.Equal(1, dispatcher.DispatchCallCount);
        Assert.Equal(NotificationType.ProjectRequestSubmitted, dispatcher.LastType);
        Assert.Equal(result.Data!.ProjectId, dispatcher.LastProjectId);
        Assert.Equal("PROJECT", dispatcher.LastReferenceType);
        Assert.Equal(result.Data.ProjectId, dispatcher.LastReferenceId);
        Assert.Equal([salesId, adminId], dispatcher.LastReceiverIds);
        Assert.NotNull(dispatcher.LastParameters);
        Assert.Equal("Moc Owner", dispatcher.LastParameters["CustomerName"]);
        Assert.Equal("Moc Coffee Interior Setup", dispatcher.LastParameters["ProjectName"]);
    }

    [Fact]
    public async Task CreateAsync_WhenNotificationFails_StillReturnsCreatedProject()
    {
        var repository = new FakeProjectRepository(
            roleName: "CUSTOMER",
            receiverIds: [Guid.NewGuid()],
            accountFullName: "Moc Owner");
        var dispatcher = new FakeNotificationDispatcher(throwOnDispatch: true);
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.ForSaveChanges(repository.SaveChangesAsync), dispatcher);

        var result = await service.CreateAsync(Guid.NewGuid(), ValidRequest());

        Assert.Equal(201, result.Status);
        Assert.NotNull(result.Data);
        Assert.Single(repository.Projects);
        Assert.Equal(1, repository.SaveChangesCallCount);
        Assert.Equal(1, dispatcher.DispatchCallCount);
    }

    [Fact]
    public async Task CreateAsync_WithBlankOptionalFields_NormalizesToNull()
    {
        var repository = new FakeProjectRepository(roleName: "customer");
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.CreateAsync(Guid.NewGuid(), new CreateProjectRequestDto
        {
            ProjectName = "Project",
            BusinessType = "Cafe",
            FurnitureRequirement = "Tables",
            ProjectAddress = " ",
            BusinessPurpose = " ",
            Description = " "
        });

        Assert.Equal(201, result.Status);
        Assert.NotNull(result.Data);
        Assert.Null(result.Data.ProjectAddress);
        Assert.Null(result.Data.BusinessPurpose);
        Assert.Null(result.Data.Description);
    }

    [Fact]
    public async Task CreateAsync_WithEmptyCurrentUser_ReturnsUnauthorized()
    {
        var repository = new FakeProjectRepository(roleName: "CUSTOMER");
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.CreateAsync(Guid.Empty, ValidRequest());

        Assert.Equal(401, result.Status);
        Assert.Equal("Authenticated account id is required.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.GetAccountRoleNameCallCount);
        Assert.Equal(0, repository.AddCallCount);
    }

    [Fact]
    public async Task CreateAsync_WithMissingRequiredFields_ReturnsValidationErrors()
    {
        var repository = new FakeProjectRepository(roleName: "CUSTOMER");
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.CreateAsync(Guid.NewGuid(), new CreateProjectRequestDto
        {
            ProjectName = " ",
            BusinessType = " ",
            FurnitureRequirement = " "
        });

        Assert.Equal(400, result.Status);
        Assert.Equal("Validation failed", result.Message);
        Assert.Contains("Project name is required.", result.Errors!);
        Assert.Contains("Business type is required.", result.Errors!);
        Assert.Contains("Furniture requirement is required.", result.Errors!);
        Assert.Null(result.Data);
        Assert.Equal(0, repository.GetAccountRoleNameCallCount);
    }

    [Fact]
    public async Task CreateAsync_WithNullRequiredFields_ReturnsValidationErrors()
    {
        var repository = new FakeProjectRepository(roleName: "CUSTOMER");
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.CreateAsync(Guid.NewGuid(), new CreateProjectRequestDto
        {
            ProjectName = null!,
            BusinessType = null!,
            FurnitureRequirement = null!
        });

        Assert.Equal(400, result.Status);
        Assert.Contains("Project name is required.", result.Errors!);
        Assert.Contains("Business type is required.", result.Errors!);
        Assert.Contains("Furniture requirement is required.", result.Errors!);
        Assert.Equal(0, repository.GetAccountRoleNameCallCount);
    }

    [Fact]
    public async Task CreateAsync_WithInvalidNumericAndDateFields_ReturnsValidationErrors()
    {
        var repository = new FakeProjectRepository(roleName: "CUSTOMER");
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.CreateAsync(Guid.NewGuid(), new CreateProjectRequestDto
        {
            ProjectName = new string('P', 151),
            BusinessType = new string('B', 101),
            FurnitureRequirement = "Tables",
            TotalAreaSqm = -1,
            NumberOfFloors = -1,
            BudgetMin = 10,
            BudgetMax = 5,
            TargetCompletionDate = DateOnly.FromDateTime(DateTime.UtcNow.Date).AddDays(-1)
        });

        Assert.Equal(400, result.Status);
        Assert.Contains("Project name must not exceed 150 characters.", result.Errors!);
        Assert.Contains("Business type must not exceed 100 characters.", result.Errors!);
        Assert.Contains("Total area must be greater than or equal to zero.", result.Errors!);
        Assert.Contains("Number of floors must be greater than or equal to zero.", result.Errors!);
        Assert.Contains("Minimum budget must be less than or equal to maximum budget.", result.Errors!);
        Assert.Contains("Target completion date must not be in the past.", result.Errors!);
        Assert.Equal(0, repository.GetAccountRoleNameCallCount);
    }

    [Fact]
    public async Task CreateAsync_WithNegativeBudgets_ReturnsValidationErrors()
    {
        var repository = new FakeProjectRepository(roleName: "CUSTOMER");
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.CreateAsync(Guid.NewGuid(), new CreateProjectRequestDto
        {
            ProjectName = "Project",
            BusinessType = "Cafe",
            FurnitureRequirement = "Tables",
            BudgetMin = -1,
            BudgetMax = -2
        });

        Assert.Equal(400, result.Status);
        Assert.Contains("Minimum budget must be greater than or equal to zero.", result.Errors!);
        Assert.Contains("Maximum budget must be greater than or equal to zero.", result.Errors!);
        Assert.Equal(0, repository.GetAccountRoleNameCallCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ADMIN")]
    [InlineData("SALES")]
    public async Task CreateAsync_WithNonCustomerRole_ReturnsForbidden(string? roleName)
    {
        var repository = new FakeProjectRepository(roleName: roleName);
        var service = ProjectServiceTestFactory.Create(repository, TestUnitOfWork.Instance);

        var result = await service.CreateAsync(Guid.NewGuid(), ValidRequest());

        Assert.Equal(403, result.Status);
        Assert.Equal("Only customer accounts can submit project requests.", result.Message);
        Assert.Null(result.Data);
        Assert.Equal(1, repository.GetAccountRoleNameCallCount);
        Assert.Equal(0, repository.AddCallCount);
        Assert.Equal(0, repository.SaveChangesCallCount);
    }

    private static CreateProjectRequestDto ValidRequest()
    {
        return new CreateProjectRequestDto
        {
            ProjectName = "Moc Coffee Interior Setup",
            BusinessType = "Cafe",
            FurnitureRequirement = "Need counter and tables",
            TargetCompletionDate = DateOnly.FromDateTime(DateTime.UtcNow.Date).AddDays(1)
        };
    }

    private static UpdateProjectBasicInformationRequestDto ValidBasicInformationRequest()
    {
        return new UpdateProjectBasicInformationRequestDto
        {
            ProjectName = "Moc Coffee Interior Setup",
            BusinessType = "Cafe",
            ProjectAddress = "District 7",
            BusinessPurpose = "Open cafe",
            FurnitureRequirement = "Counter, tables, chairs",
            Description = "Updated basic information",
            TotalAreaSqm = 80,
            NumberOfFloors = 1,
            BudgetMin = 150000000,
            BudgetMax = 250000000,
            TargetCompletionDate = DateOnly.FromDateTime(DateTime.UtcNow.Date).AddDays(30)
        };
    }

    private static UpdateProjectStatusRequestDto ValidStatusRequest()
    {
        return new UpdateProjectStatusRequestDto
        {
            Status = ProjectStatus.WAITING_FOR_DESIGNER_ASSIGNMENT,
            Note = "Project has enough basic information."
        };
    }

    private static RejectProjectRequestDto ValidRejectRequest()
    {
        return new RejectProjectRequestDto
        {
            RejectionReason = "Requested service scope is unsupported."
        };
    }

    private static AssignProjectDesignerRequestDto ValidAssignDesignerRequest()
    {
        return new AssignProjectDesignerRequestDto
        {
            DesignerId = Guid.NewGuid(),
            SpaceDataStatus = ProjectSpaceDataStatus.SUFFICIENT,
            Note = "Please review the project requirement."
        };
    }

    private static DesignerAccountReadModel CreateDesigner()
    {
        return new DesignerAccountReadModel
        {
            AccountId = Guid.NewGuid(),
            FullName = "Le Designer"
        };
    }

    private static Project CreateDesignerAssignableProject(Guid projectId, Guid salesId)
    {
        return new Project
        {
            ProjectId = projectId,
            CustomerId = Guid.NewGuid(),
            AssignedSalesId = salesId,
            ProjectName = "Moc Coffee Interior Setup",
            BusinessType = "Cafe",
            FurnitureRequirement = "Counter, tables, chairs",
            Status = ProjectStatus.WAITING_FOR_DESIGNER_ASSIGNMENT
        };
    }

    private static Project CreateQualifiedProject(Guid projectId, Guid salesId)
    {
        return new Project
        {
            ProjectId = projectId,
            CustomerId = Guid.NewGuid(),
            AssignedSalesId = salesId,
            ProjectName = "Moc Coffee Interior Setup",
            BusinessType = "Cafe",
            FurnitureRequirement = "Counter, tables, chairs",
            Status = ProjectStatus.IN_CONSULTATION
        };
    }

    private static ProjectDetailReadModel CreateProjectDetail(Guid projectId, Guid customerId)
    {
        return new ProjectDetailReadModel
        {
            ProjectId = projectId,
            CustomerId = customerId,
            ProjectCode = "PRJ-2026-0001",
            ProjectName = "Moc Coffee",
            BusinessType = "Cafe",
            ProjectAddress = "District 7",
            BusinessPurpose = "Open cafe",
            FurnitureRequirement = "Tables",
            Description = "Warm design",
            TotalAreaSqm = 80,
            NumberOfFloors = 1,
            BudgetMin = 150000000,
            BudgetMax = 250000000,
            TargetCompletionDate = DateOnly.FromDateTime(DateTime.UtcNow.Date).AddDays(30),
            Status = ProjectStatus.SUBMITTED,
            SubmittedAt = DateTime.UtcNow
        };
    }

    internal sealed record FakeProjectRepositoryState
    {
        public IReadOnlyList<ProjectListItemReadModel>? ListItems { get; init; }
        public ProjectDetailReadModel? Detail { get; init; }
        public IReadOnlyList<Project>? Entities { get; init; }
        public DesignerAccountReadModel? Designer { get; init; }
        public IReadOnlyList<Guid>? ReceiverIds { get; init; }
        public string? AccountFullName { get; init; }
    }

    private sealed class ThrowingSearchIndexService : ISearchIndexService
    {
        public Task IndexAsync<TDocument>(string indexName, string id, TDocument document, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task BulkIndexAsync<TDocument>(string indexName, IReadOnlyList<BulkIndexItem<TDocument>> items, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(string indexName, string id, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<SearchResult<TDocument>> SearchAsync<TDocument>(string indexName, SearchRequest request, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Elasticsearch unavailable.");

        public Task<IReadOnlyList<TDocument>> SearchAsync<TDocument>(string indexName, string query, int size = 100, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Elasticsearch unavailable.");

        public Task<SuggestResult> SuggestAsync(string indexName, SuggestRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new SuggestResult());

        public Task<SearchResult<TDocument>> MoreLikeThisAsync<TDocument>(
            string indexName,
            string documentId,
            MoreLikeThisRequest request,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Elasticsearch unavailable.");

        public Task<SearchAggregationResult> AggregateAsync(
            string indexName,
            SearchAggregationRequest request,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Elasticsearch unavailable.");
    }

    internal sealed class FakeProjectRepository : IProjectRepository
    {
        private readonly string? _roleName;
        private readonly int _submittedCount;
        private readonly FakeProjectRepositoryState _state;
        private readonly List<Project> _projects = [];

        public FakeProjectRepository(
            string? roleName,
            int submittedCount = 0,
            FakeProjectRepositoryState? state = null)
        {
            _roleName = roleName;
            _submittedCount = submittedCount;
            _state = state ?? new FakeProjectRepositoryState();
            _projects = _state.Entities?.ToList() ?? [];
        }

        public FakeProjectRepository(string? roleName, IReadOnlyList<Project>? entities)
            : this(roleName, 0, new FakeProjectRepositoryState { Entities = entities })
        {
        }

        public FakeProjectRepository(
            string? roleName,
            IReadOnlyList<Project>? entities,
            DesignerAccountReadModel? designer)
            : this(roleName, 0, new FakeProjectRepositoryState
            {
                Entities = entities,
                Designer = designer
            })
        {
        }

        public FakeProjectRepository(string? roleName, ProjectDetailReadModel? detail)
            : this(roleName, 0, new FakeProjectRepositoryState { Detail = detail })
        {
        }

        public FakeProjectRepository(
            string? roleName,
            IReadOnlyList<ProjectListItemReadModel>? listItems)
            : this(roleName, 0, new FakeProjectRepositoryState { ListItems = listItems })
        {
        }

        public FakeProjectRepository(
            string? roleName,
            IReadOnlyList<Guid>? receiverIds,
            string? accountFullName)
            : this(roleName, 0, new FakeProjectRepositoryState
            {
                ReceiverIds = receiverIds,
                AccountFullName = accountFullName
            })
        {
        }

        public IReadOnlyList<Project> Projects => _projects;
        public int GetAccountRoleNameCallCount { get; private set; }
        public int CountSubmittedInYearCallCount { get; private set; }
        public int GetDetailCallCount { get; private set; }
        public int GetByIdCallCount { get; private set; }
        public int GetActiveDesignerCallCount { get; private set; }
        public int GetListCallCount { get; private set; }
        public int CountCallCount { get; private set; }
        public int AddCallCount { get; private set; }
        public int SaveChangesCallCount { get; private set; }
        public int GetActiveAccountIdsByRoleNamesCallCount { get; private set; }
        public int GetAccountFullNameCallCount { get; private set; }
        public ProjectListQueryReadModel? LastListQuery { get; private set; }

        public Task<string?> GetAccountRoleNameAsync(Guid accountId, CancellationToken cancellationToken = default)
        {
            GetAccountRoleNameCallCount++;
            return Task.FromResult(_roleName);
        }

        public Task<string?> GetAccountFullNameAsync(Guid accountId, CancellationToken cancellationToken = default)
        {
            GetAccountFullNameCallCount++;
            return Task.FromResult(_state.AccountFullName);
        }

        public Task<IReadOnlyList<Guid>> GetActiveAccountIdsByRoleNamesAsync(
            IReadOnlyCollection<string> roleNames,
            CancellationToken cancellationToken = default)
        {
            GetActiveAccountIdsByRoleNamesCallCount++;
            return Task.FromResult(_state.ReceiverIds ?? []);
        }

        public Task<int> CountSubmittedInYearAsync(int year, CancellationToken cancellationToken = default)
        {
            CountSubmittedInYearCallCount++;
            return Task.FromResult(_submittedCount);
        }

        public Task<ProjectDetailReadModel?> GetDetailAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
        {
            GetDetailCallCount++;
            return Task.FromResult(_state.Detail?.ProjectId == projectId ? _state.Detail : null);
        }

        public Task<DesignerAccountReadModel?> GetActiveDesignerAsync(
            Guid designerId,
            CancellationToken cancellationToken = default)
        {
            GetActiveDesignerCallCount++;
            return Task.FromResult(_state.Designer?.AccountId == designerId ? _state.Designer : null);
        }

        public Task<IReadOnlyList<ProjectListItemReadModel>> GetListAsync(
            ProjectListQueryReadModel query,
            CancellationToken cancellationToken = default)
        {
            GetListCallCount++;
            LastListQuery = query;
            return Task.FromResult(_state.ListItems ?? []);
        }

        public Task<int> CountAsync(
            ProjectListQueryReadModel query,
            CancellationToken cancellationToken = default)
        {
            CountCallCount++;
            LastListQuery = query;
            return Task.FromResult((_state.ListItems ?? []).Count);
        }

        public Task<ProjectSearchIndexItemReadModel?> GetSearchIndexItemAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<ProjectSearchIndexItemReadModel?>(null);

        public Task<IReadOnlyList<ProjectSearchIndexItemReadModel>> GetSearchIndexPageAsync(
            int page,
            int limit,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProjectSearchIndexItemReadModel>>([]);
        public Task<IReadOnlyList<ProjectByUserItemReadModel>> GetByUserAsync(
            ProjectByUserQueryReadModel query,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ProjectByUserItemReadModel>>([]);
        }

        public Task<int> CountByUserAsync(
            ProjectByUserQueryReadModel query,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(0);
        }

        public IQueryable<Project> Query() => _projects.AsQueryable();
        public Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            GetByIdCallCount++;
            return Task.FromResult(_projects.FirstOrDefault(project => project.ProjectId == id));
        }

        public Task<IReadOnlyList<Project>> ListAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Project>>(_projects);
        }

        public Task AddAsync(Project entity, CancellationToken cancellationToken = default)
        {
            AddCallCount++;
            _projects.Add(entity);
            return Task.CompletedTask;
        }

        public Task AddRangeAsync(IEnumerable<Project> entities, CancellationToken cancellationToken = default)
        {
            _projects.AddRange(entities);
            return Task.CompletedTask;
        }

        public void Update(Project entity)
        {
        }

        public void Remove(Project entity)
        {
            _projects.Remove(entity);
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCallCount++;
            return Task.FromResult(1);
        }
    }

    private sealed class FakeProjectChatService : IProjectChatService
    {
        private readonly bool _throwOnUpsert;

        public FakeProjectChatService(bool throwOnUpsert = false)
        {
            _throwOnUpsert = throwOnUpsert;
            Summary = new ProjectChatSummaryDto
            {
                ChatId = Guid.NewGuid(),
                ChatType = ProjectChatType.SALES.ToString(),
                Title = "Sales Consultation",
                Status = ProjectChatStatus.OPEN.ToString()
            };
        }

        public int UpsertCallCount { get; private set; }
        public Guid ProjectId { get; private set; }
        public ProjectChatType ChatType { get; private set; }
        public Guid StaffId { get; private set; }
        public string? Title { get; private set; }
        public ProjectChatSummaryDto Summary { get; }

        public Task<bool> CanAccessProjectAsync(
            Guid projectId,
            Guid currentUserId,
            CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<ServiceResult<ProjectChatSummaryDto>> CreateManualAsync(
            Guid projectId,
            Guid currentUserId,
            CreateProjectChatRequestDto request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ServiceResult<ProjectChatSummaryDto>.Created(Summary));

        public Task<ServiceResult<ProjectChatListResponseDto>> GetProjectChatsAsync(
            Guid projectId,
            Guid currentUserId,
            ProjectChatListQueryDto query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ServiceResult<ProjectChatListResponseDto>.Success(
                new ProjectChatListResponseDto()));

        public Task<ProjectChatSummaryDto> UpsertProjectChatAsync(
            Guid projectId,
            ProjectChatType chatType,
            Guid staffId,
            string title,
            CancellationToken cancellationToken = default)
        {
            UpsertCallCount++;
            ProjectId = projectId;
            ChatType = chatType;
            StaffId = staffId;
            Title = title;
            Summary.ProjectId = projectId;
            Summary.StaffId = staffId;

            return _throwOnUpsert
                ? Task.FromException<ProjectChatSummaryDto>(
                    new InvalidOperationException("Project chat upsert failed."))
                : Task.FromResult(Summary);
        }

        public Task<ServiceResult<ProjectChatSummaryDto>> UpdateStatusAsync(
            Guid chatId,
            Guid currentUserId,
            UpdateProjectChatStatusRequestDto request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ServiceResult<ProjectChatSummaryDto>.Success(Summary));
    }

    private sealed class FakeNotificationDispatcher : INotificationDispatcher
    {
        private readonly bool _throwOnDispatch;
        private readonly Action? _onDispatch;

        public FakeNotificationDispatcher(
            bool throwOnDispatch = false,
            Action? onDispatch = null)
        {
            _throwOnDispatch = throwOnDispatch;
            _onDispatch = onDispatch;
        }

        public int DispatchCallCount { get; private set; }
        public NotificationType LastType { get; private set; }
        public IReadOnlyDictionary<string, string>? LastParameters { get; private set; }
        public IReadOnlyList<Guid> LastReceiverIds { get; private set; } = [];
        public Guid? LastProjectId { get; private set; }
        public string? LastReferenceType { get; private set; }
        public Guid? LastReferenceId { get; private set; }

        public Task DispatchAsync(
            NotificationType type,
            IReadOnlyDictionary<string, string> parameters,
            IEnumerable<Guid> receiverIds,
            Guid? projectId = null,
            string? referenceType = null,
            Guid? referenceId = null,
            CancellationToken cancellationToken = default)
        {
            DispatchCallCount++;
            LastType = type;
            LastParameters = parameters;
            LastReceiverIds = receiverIds.ToList();
            LastProjectId = projectId;
            LastReferenceType = referenceType;
            LastReferenceId = referenceId;
            _onDispatch?.Invoke();

            if (_throwOnDispatch)
            {
                throw new InvalidOperationException("Notification dispatch failed.");
            }

            return Task.CompletedTask;
        }
    }
}
