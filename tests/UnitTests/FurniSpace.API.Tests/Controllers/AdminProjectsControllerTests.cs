#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.API.Controllers.Admin;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Projects;
using FurniSpace.Application.Interfaces.Projects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FurniSpace.API.Tests.Controllers;

public sealed class AdminProjectsControllerTests
{
    [Fact]
    public void AdminProjectsController_RequiresAdminRole()
    {
        var authorize = typeof(AdminProjectsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();
        Assert.Equal("ADMIN", authorize.Roles);
    }

    [Fact]
    public async Task GetWorkflow_ReturnsServiceResult()
    {
        var projectId = Guid.NewGuid();
        var controller = new AdminProjectsController(new FakeProjectWorkflowService(projectId));

        var result = await controller.GetWorkflow(projectId);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(200, objectResult.StatusCode);
        var payload = Assert.IsType<ServiceResult<ProjectWorkflowDto>>(objectResult.Value);
        Assert.Equal("Project workflow retrieved successfully.", payload.Message);
        Assert.NotNull(payload.Data);
        Assert.Equal(projectId, payload.Data.ProjectId);
        Assert.Equal(6, payload.Data.Stages.Count);
    }

    [Fact]
    public async Task GetWorkflow_WhenNotFound_Returns404()
    {
        var controller = new AdminProjectsController(new FakeProjectWorkflowService(null));

        var result = await controller.GetWorkflow(Guid.NewGuid());

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(404, objectResult.StatusCode);
    }

    private sealed class FakeProjectWorkflowService : IProjectWorkflowService
    {
        private readonly Guid? _projectId;

        public FakeProjectWorkflowService(Guid? projectId)
        {
            _projectId = projectId;
        }

        public Task<ServiceResult<ProjectWorkflowDto>> GetWorkflowAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
        {
            if (_projectId is null)
            {
                return Task.FromResult(ServiceResult<ProjectWorkflowDto>.NotFound("Project not found."));
            }

            return Task.FromResult(ServiceResult<ProjectWorkflowDto>.Success(
                new ProjectWorkflowDto
                {
                    ProjectId = _projectId.Value,
                    ProjectName = "Cafe ABC",
                    CurrentStatus = "PROPOSAL_CONSULTING",
                    CurrentStage = "DESIGN_REVIEW",
                    Stages =
                    [
                        new ProjectWorkflowStageDto { Key = "INTAKE", State = "COMPLETED" },
                        new ProjectWorkflowStageDto { Key = "DESIGNER_ASSIGNMENT", State = "COMPLETED" },
                        new ProjectWorkflowStageDto { Key = "DESIGN_REVIEW", State = "ACTIVE" },
                        new ProjectWorkflowStageDto { Key = "QUOTATION_ORDER", State = "NOT_STARTED" },
                        new ProjectWorkflowStageDto { Key = "PRODUCTION", State = "NOT_STARTED" },
                        new ProjectWorkflowStageDto { Key = "DELIVERY", State = "NOT_STARTED" }
                    ]
                },
                "Project workflow retrieved successfully."));
        }
    }
}
