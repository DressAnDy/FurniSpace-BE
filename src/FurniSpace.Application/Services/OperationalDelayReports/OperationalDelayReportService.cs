using FurniSpace.Application.Common;
using FurniSpace.Application.Common.OperationalDelayReports;
using FurniSpace.Application.Constants.Common;
using FurniSpace.Application.DTOs.OperationalDelayReports;
using FurniSpace.Application.Interfaces.OperationalDelayReports;
using FurniSpace.Application.Interfaces.Projects;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Infrastructure.ReadModels.OperationalDelayReports;
using FurniSpace.Infrastructure.Repositories.IRepository;

namespace FurniSpace.Application.Services.OperationalDelayReports;

public sealed class OperationalDelayReportService : IOperationalDelayReportService
{
    private const string ProjectNotFoundMessage = "Project not found.";
    private const string ReportNotFoundMessage = "Operational delay report not found.";
    private const string ForbiddenMessage = "You do not have access to operational delay reports for this project.";
    private const int MaxReasonCodeLength = 100;
    private const int MaxReasonDetailLength = 4000;

    private readonly IOperationalDelayReportRepository _reports;
    private readonly IProjectRepository _projects;
    private readonly IProductionRequestRepository _productionRequests;
    private readonly IOrderRepository _orders;
    private readonly IDeliveryRepository _deliveries;
    private readonly IProjectPhaseDeadlineService _phaseDeadlines;
    private readonly IUnitOfWork _unitOfWork;

    public OperationalDelayReportService(
        IOperationalDelayReportRepository reports,
        IProjectRepository projects,
        IProductionRequestRepository productionRequests,
        IOrderRepository orders,
        IDeliveryRepository deliveries,
        IProjectPhaseDeadlineService phaseDeadlines,
        IUnitOfWork unitOfWork)
    {
        _reports = reports;
        _projects = projects;
        _productionRequests = productionRequests;
        _orders = orders;
        _deliveries = deliveries;
        _phaseDeadlines = phaseDeadlines;
        _unitOfWork = unitOfWork;
    }

    public async Task<ServiceResult<OperationalDelayReportDto>> CreateProductionReportAsync(
        Guid projectId,
        Guid currentUserId,
        CreateProductionDelayReportRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var validationError = ValidateProductionCreateRequest(projectId, currentUserId, request);
        if (validationError is not null)
        {
            return validationError;
        }

        var project = await _projects.GetByIdAsync(projectId, cancellationToken);
        if (project is null)
        {
            return NotFound(OperationalDelayReportErrorCodes.ProjectNotFound, ProjectNotFoundMessage);
        }

        var roleName = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!await CanManageProductionReportAsync(project, roleName, currentUserId, request.ProductionRequestId, cancellationToken))
        {
            return Forbidden();
        }

        var productionRequest = await _productionRequests.GetByIdAsync(request.ProductionRequestId, cancellationToken);
        if (productionRequest is null)
        {
            return NotFound(
                OperationalDelayReportErrorCodes.ProductionRequestNotFound,
                "Production request not found.");
        }

        if (productionRequest.ProjectId != projectId)
        {
            return BadRequest(
                OperationalDelayReportErrorCodes.ProductionRequestProjectMismatch,
                "Production request does not belong to this project.");
        }

        var productionDeadline = await _phaseDeadlines.GetProductionDeadlineAsync(projectId, cancellationToken);
        if (!productionDeadline.HasValue)
        {
            return BadRequest(
                OperationalDelayReportErrorCodes.ProductionDeadlineMissing,
                "Production deadline must be set before recording a production delay report.");
        }

        var now = DateTime.UtcNow;
        var report = BuildReport(
            projectId,
            OperationalDelayPhase.PRODUCTION,
            productionDeadline.Value,
            now,
            currentUserId,
            request.ReasonCode,
            request.ReasonDetail,
            productionRequestId: request.ProductionRequestId);

        await _reports.AddAsync(report, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ServiceResult<OperationalDelayReportDto>.Created(
            ToDto(report, project.ProjectName, null),
            "Production delay report recorded successfully.");
    }

    public async Task<ServiceResult<OperationalDelayReportDto>> CreateDeliveryReportAsync(
        Guid projectId,
        Guid currentUserId,
        CreateDeliveryDelayReportRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var validationError = ValidateDeliveryCreateRequest(projectId, currentUserId, request);
        if (validationError is not null)
        {
            return validationError;
        }

        var project = await _projects.GetByIdAsync(projectId, cancellationToken);
        if (project is null)
        {
            return NotFound(OperationalDelayReportErrorCodes.ProjectNotFound, ProjectNotFoundMessage);
        }

        var roleName = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!await CanViewStaffReportsAsync(project.ProjectId, project.AssignedSalesId, roleName, currentUserId, cancellationToken))
        {
            return Forbidden();
        }

        if (!project.TargetCompletionDate.HasValue)
        {
            return BadRequest(
                OperationalDelayReportErrorCodes.TargetCompletionDateMissing,
                "Project target completion date must be set before recording a delivery delay report.");
        }

        if (request.OrderId.HasValue)
        {
            var order = await _orders.GetByIdAsync(request.OrderId.Value, cancellationToken);
            if (order is null || order.ProjectId != projectId)
            {
                return BadRequest(
                    OperationalDelayReportErrorCodes.OrderProjectMismatch,
                    "Order does not belong to this project.");
            }
        }

        if (request.DeliveryId.HasValue)
        {
            var delivery = await _deliveries.GetByIdAsync(request.DeliveryId.Value, cancellationToken);
            if (delivery is null)
            {
                return BadRequest(
                    OperationalDelayReportErrorCodes.DeliveryProjectMismatch,
                    "Delivery does not belong to this project.");
            }

            var deliveryOrder = await _orders.GetByIdAsync(delivery.OrderId, cancellationToken);
            if (deliveryOrder is null || deliveryOrder.ProjectId != projectId)
            {
                return BadRequest(
                    OperationalDelayReportErrorCodes.DeliveryProjectMismatch,
                    "Delivery does not belong to this project.");
            }

            if (request.OrderId.HasValue && request.OrderId.Value != delivery.OrderId)
            {
                return BadRequest(
                    OperationalDelayReportErrorCodes.InvalidRequest,
                    "Delivery does not belong to the specified order.");
            }
        }

        var now = DateTime.UtcNow;
        var report = BuildReport(
            projectId,
            OperationalDelayPhase.DELIVERY,
            project.TargetCompletionDate.Value,
            now,
            currentUserId,
            request.ReasonCode,
            request.ReasonDetail,
            orderId: request.OrderId,
            deliveryId: request.DeliveryId);

        await _reports.AddAsync(report, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ServiceResult<OperationalDelayReportDto>.Created(
            ToDto(report, project.ProjectName, null),
            "Delivery delay report recorded successfully.");
    }

    public async Task<ServiceResult<OperationalDelayReportListResponseDto>> GetByProjectAsync(
        Guid projectId,
        Guid currentUserId,
        OperationalDelayPhase phase,
        CancellationToken cancellationToken = default)
    {
        if (projectId == Guid.Empty)
        {
            return BadRequestList(OperationalDelayReportErrorCodes.InvalidRequest, "Project id is required.");
        }

        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<OperationalDelayReportListResponseDto>.Unauthorized();
        }

        if (!Enum.IsDefined(typeof(OperationalDelayPhase), phase))
        {
            return BadRequestList(OperationalDelayReportErrorCodes.InvalidRequest, "Report phase is invalid.");
        }

        var project = await _projects.GetByIdAsync(projectId, cancellationToken);
        if (project is null)
        {
            return ServiceResult<OperationalDelayReportListResponseDto>.NotFound(ProjectNotFoundMessage);
        }

        var roleName = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!await CanViewStaffReportsAsync(project.ProjectId, project.AssignedSalesId, roleName, currentUserId, cancellationToken))
        {
            return ServiceResult<OperationalDelayReportListResponseDto>.Forbidden(ForbiddenMessage);
        }

        var items = await _reports.GetByProjectAsync(projectId, phase, cancellationToken);
        return ServiceResult<OperationalDelayReportListResponseDto>.Success(
            new OperationalDelayReportListResponseDto
            {
                Items = items.Select(ToDto).ToList()
            },
            "Operational delay reports retrieved successfully.");
    }

    public async Task<ServiceResult<OperationalDelayReportDto>> GetDetailAsync(
        Guid reportId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (reportId == Guid.Empty || currentUserId == Guid.Empty)
        {
            return BadRequest(OperationalDelayReportErrorCodes.InvalidRequest, "Report id is required.");
        }

        var detail = await _reports.GetDetailAsync(reportId, cancellationToken);
        if (detail is null)
        {
            return NotFound(OperationalDelayReportErrorCodes.ReportNotFound, ReportNotFoundMessage);
        }

        var project = await _projects.GetByIdAsync(detail.ProjectId, cancellationToken);
        if (project is null)
        {
            return NotFound(OperationalDelayReportErrorCodes.ProjectNotFound, ProjectNotFoundMessage);
        }

        var roleName = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!await CanViewStaffReportsAsync(project.ProjectId, project.AssignedSalesId, roleName, currentUserId, cancellationToken))
        {
            return Forbidden();
        }

        return ServiceResult<OperationalDelayReportDto>.Success(
            ToDto(detail),
            "Operational delay report retrieved successfully.");
    }

    private static OperationalDelayReport BuildReport(
        Guid projectId,
        OperationalDelayPhase phase,
        DateOnly deadlineSnapshot,
        DateTime reportedAt,
        Guid reportedBy,
        string? reasonCode,
        string reasonDetail,
        Guid? productionRequestId = null,
        Guid? orderId = null,
        Guid? deliveryId = null)
    {
        return new OperationalDelayReport
        {
            OperationalDelayReportId = Guid.NewGuid(),
            ProjectId = projectId,
            ReportPhase = phase,
            ProductionRequestId = productionRequestId,
            OrderId = orderId,
            DeliveryId = deliveryId,
            DeadlineSnapshot = deadlineSnapshot,
            DelayState = OperationalDelayClassificationSupport.DeriveDelayState(deadlineSnapshot, reportedAt),
            ReasonCode = NormalizeOptionalText(reasonCode),
            ReasonDetail = reasonDetail.Trim(),
            ReportedBy = reportedBy,
            ReportedAt = reportedAt,
            CreatedAt = reportedAt
        };
    }

    private async Task<bool> CanManageProductionReportAsync(
        Project project,
        string? roleName,
        Guid currentUserId,
        Guid productionRequestId,
        CancellationToken cancellationToken)
    {
        if (IsAdmin(roleName))
        {
            return true;
        }

        if (IsSales(roleName))
        {
            return project.AssignedSalesId == currentUserId;
        }

        if (!IsProduction(roleName))
        {
            return false;
        }

        var productionRequest = await _productionRequests.GetByIdAsync(productionRequestId, cancellationToken);
        return productionRequest is not null &&
            productionRequest.ProjectId == project.ProjectId &&
            productionRequest.AssignedTo == currentUserId;
    }

    private async Task<bool> CanViewStaffReportsAsync(
        Guid projectId,
        Guid? assignedSalesId,
        string? roleName,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        if (IsAdmin(roleName))
        {
            return true;
        }

        if (IsSales(roleName))
        {
            return assignedSalesId == currentUserId;
        }

        if (IsProduction(roleName))
        {
            return await _productionRequests.HasViewableAssignedRequestAsync(
                projectId,
                currentUserId,
                cancellationToken);
        }

        return false;
    }

    private static ServiceResult<OperationalDelayReportDto>? ValidateProductionCreateRequest(
        Guid projectId,
        Guid currentUserId,
        CreateProductionDelayReportRequestDto request)
    {
        if (projectId == Guid.Empty || currentUserId == Guid.Empty)
        {
            return BadRequest(OperationalDelayReportErrorCodes.InvalidRequest, "Project id is required.");
        }

        if (request.ProductionRequestId == Guid.Empty)
        {
            return BadRequest(OperationalDelayReportErrorCodes.InvalidRequest, "Production request id is required.");
        }

        return ValidateReasonFields(request.ReasonCode, request.ReasonDetail);
    }

    private static ServiceResult<OperationalDelayReportDto>? ValidateDeliveryCreateRequest(
        Guid projectId,
        Guid currentUserId,
        CreateDeliveryDelayReportRequestDto request)
    {
        if (projectId == Guid.Empty || currentUserId == Guid.Empty)
        {
            return BadRequest(OperationalDelayReportErrorCodes.InvalidRequest, "Project id is required.");
        }

        return ValidateReasonFields(request.ReasonCode, request.ReasonDetail);
    }

    private static ServiceResult<OperationalDelayReportDto>? ValidateReasonFields(
        string? reasonCode,
        string reasonDetail)
    {
        if (string.IsNullOrWhiteSpace(reasonDetail))
        {
            return BadRequest(OperationalDelayReportErrorCodes.InvalidRequest, "Reason detail is required.");
        }

        if (reasonDetail.Trim().Length > MaxReasonDetailLength)
        {
            return BadRequest(
                OperationalDelayReportErrorCodes.InvalidRequest,
                $"Reason detail must not exceed {MaxReasonDetailLength} characters.");
        }

        if (!string.IsNullOrWhiteSpace(reasonCode) && reasonCode.Trim().Length > MaxReasonCodeLength)
        {
            return BadRequest(
                OperationalDelayReportErrorCodes.InvalidRequest,
                $"Reason code must not exceed {MaxReasonCodeLength} characters.");
        }

        return null;
    }

    private static OperationalDelayReportDto ToDto(
        OperationalDelayReport report,
        string? projectName,
        string? reporterName)
    {
        return new OperationalDelayReportDto
        {
            OperationalDelayReportId = report.OperationalDelayReportId,
            ProjectId = report.ProjectId,
            ProjectName = projectName,
            ReportPhase = report.ReportPhase.ToString(),
            ProductionRequestId = report.ProductionRequestId,
            OrderId = report.OrderId,
            DeliveryId = report.DeliveryId,
            DeadlineSnapshot = report.DeadlineSnapshot,
            DelayState = report.DelayState.ToString(),
            ReasonCode = report.ReasonCode,
            ReasonDetail = report.ReasonDetail,
            ReportedBy = report.ReportedBy,
            ReporterName = reporterName,
            ReportedAt = report.ReportedAt,
            CreatedAt = report.CreatedAt
        };
    }

    private static OperationalDelayReportDto ToDto(OperationalDelayReportListItemReadModel item)
    {
        return new OperationalDelayReportDto
        {
            OperationalDelayReportId = item.OperationalDelayReportId,
            ProjectId = item.ProjectId,
            ReportPhase = item.ReportPhase.ToString(),
            ProductionRequestId = item.ProductionRequestId,
            OrderId = item.OrderId,
            DeliveryId = item.DeliveryId,
            DeadlineSnapshot = item.DeadlineSnapshot,
            DelayState = item.DelayState.ToString(),
            ReasonCode = item.ReasonCode,
            ReasonDetail = item.ReasonDetail,
            ReportedBy = item.ReportedBy,
            ReporterName = item.ReporterName,
            ReportedAt = item.ReportedAt,
            CreatedAt = item.CreatedAt
        };
    }

    private static OperationalDelayReportDto ToDto(OperationalDelayReportDetailReadModel item)
    {
        var dto = ToDto((OperationalDelayReportListItemReadModel)item);
        dto.ProjectName = item.ProjectName;
        return dto;
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static bool IsAdmin(string? roleName) =>
        string.Equals(roleName, ApplicationRoles.Admin, StringComparison.OrdinalIgnoreCase);

    private static bool IsSales(string? roleName) =>
        string.Equals(roleName, ApplicationRoles.Sales, StringComparison.OrdinalIgnoreCase);

    private static bool IsProduction(string? roleName) =>
        string.Equals(roleName, ApplicationRoles.Production, StringComparison.OrdinalIgnoreCase);

    private static ServiceResult<OperationalDelayReportDto> NotFound(string code, string message) =>
        ServiceResult<OperationalDelayReportDto>.Failure(Error.NotFound(code, message));

    private static ServiceResult<OperationalDelayReportDto> BadRequest(string code, string message) =>
        ServiceResult<OperationalDelayReportDto>.Failure(Error.Validation(code, message));

    private static ServiceResult<OperationalDelayReportDto> Forbidden() =>
        ServiceResult<OperationalDelayReportDto>.Forbidden(ForbiddenMessage);

    private static ServiceResult<OperationalDelayReportListResponseDto> BadRequestList(string code, string message) =>
        ServiceResult<OperationalDelayReportListResponseDto>.Failure(Error.Validation(code, message));
}
