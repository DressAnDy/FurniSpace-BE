#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.API.Controllers.Admin;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Reports;
using FurniSpace.Application.Interfaces.Reports;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FurniSpace.API.Tests.Controllers;

public sealed class AdminProjectReportsControllerTests
{
    [Fact]
    public async Task GetList_ReturnsServiceResultThroughBaseController()
    {
        var page = PagedResult<AdminProjectReportListItemDto>.Create(
            [
                new AdminProjectReportListItemDto
                {
                    ProjectId = Guid.NewGuid(),
                    ProjectName = "Cafe",
                    AttentionReason = "UNASSIGNED_INTAKE",
                    Severity = "ACTION"
                }
            ],
            1,
            20,
            1);
        var service = new FakeAdminProjectReportService(
            ServiceResult<PagedResult<AdminProjectReportListItemDto>>.Success(
                page,
                "Admin project reports retrieved successfully."));
        var controller = new AdminProjectReportsController(service);
        var query = new AdminProjectReportsQueryDto { AttentionOnly = true };

        var actionResult = await controller.GetList(query);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Same(query, service.ListQuery);
    }

    [Fact]
    public async Task GetDetail_ReturnsServiceResultThroughBaseController()
    {
        var projectId = Guid.NewGuid();
        var service = new FakeAdminProjectReportService(
            listResult: ServiceResult<PagedResult<AdminProjectReportListItemDto>>.Success(
                PagedResult<AdminProjectReportListItemDto>.Create([], 1, 20, 0)),
            detailResult: ServiceResult<AdminProjectReportDetailDto>.Success(
                new AdminProjectReportDetailDto
                {
                    Header = new AdminProjectReportHeaderDto
                    {
                        ProjectId = projectId,
                        ProjectName = "Cafe"
                    }
                },
                "Admin project report retrieved successfully."));
        var controller = new AdminProjectReportsController(service);

        var actionResult = await controller.GetDetail(projectId);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal(projectId, service.DetailProjectId);
    }

    private sealed class FakeAdminProjectReportService : IAdminProjectReportService
    {
        private readonly ServiceResult<PagedResult<AdminProjectReportListItemDto>> _listResult;
        private readonly ServiceResult<AdminProjectReportDetailDto> _detailResult;

        public FakeAdminProjectReportService(
            ServiceResult<PagedResult<AdminProjectReportListItemDto>> listResult,
            ServiceResult<AdminProjectReportDetailDto>? detailResult = null)
        {
            _listResult = listResult;
            _detailResult = detailResult
                ?? ServiceResult<AdminProjectReportDetailDto>.Success(new AdminProjectReportDetailDto());
        }

        public AdminProjectReportsQueryDto? ListQuery { get; private set; }
        public Guid? DetailProjectId { get; private set; }

        public Task<ServiceResult<PagedResult<AdminProjectReportListItemDto>>> GetListAsync(
            AdminProjectReportsQueryDto query,
            CancellationToken cancellationToken = default)
        {
            ListQuery = query;
            return Task.FromResult(_listResult);
        }

        public Task<ServiceResult<AdminProjectReportDetailDto>> GetDetailAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
        {
            DetailProjectId = projectId;
            return Task.FromResult(_detailResult);
        }
    }
}
