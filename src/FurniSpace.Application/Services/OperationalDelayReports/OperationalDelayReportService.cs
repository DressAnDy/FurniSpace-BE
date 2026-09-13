using FurniSpace.Application.Common;
using FurniSpace.Application.Common.OperationalDelayReports;
using FurniSpace.Application.Common.Reports;
using FurniSpace.Application.Constants.Common;
using FurniSpace.Application.DTOs.OperationalDelayReports;
using FurniSpace.Application.Interfaces.Notifications;
using FurniSpace.Application.Interfaces.OperationalDelayReports;
using FurniSpace.Application.Interfaces.Projects;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Infrastructure.ReadModels.OperationalDelayReports;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Microsoft.Extensions.Logging;

namespace FurniSpace.Application.Services.OperationalDelayReports;

public sealed class OperationalDelayReportService : IOperationalDelayReportService
{
    private const string ProjectNotFoundMessage = "Project not found.";
    private const string ReportNotFoundMessage = "Operational delay report not found.";
    private const string ForbiddenMessage = "You do not have access to operational delay reports for this project.";

    private readonly IOperationalDelayReportRepository _reports;
    private readonly IProjectRepository _projects;
    private readonly IProductionRequestRepository _productionRequests;
    private readonly IOrderRepository _orders;
    private readonly IDeliveryRepository _deliveries;
    private readonly IProjectPhaseDeadlineService _phaseDeadlines;
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationDispatcher? _notifications;
    private readonly ILogger<OperationalDelayReportService>? _logger;

    public OperationalDelayReportService(
        IOperationalDelayReportRepository reports,
        IProjectRepository projects,
        IProductionRequestRepository productionRequests,
        IOrderRepository orders,
        IDeliveryRepository deliveries,
        IProjectPhaseDeadlineService phaseDeadlines,
        IUnitOfWork unitOfWork,
        INotificationDispatcher? notifications = null,
        ILogger<OperationalDelayReportService>? logger = null)
    {
        _reports = reports;
        _projects = projects;
        _productionRequests = productionRequests;
        _orders = orders;
        _deliveries = deliveries;
        _phaseDeadlines = phaseDeadlines;
        _unitOfWork = unitOfWork;
        _notifications = notifications;
        _logger = logger;
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
            request.ProductionReasonCode,
            request.ReasonDetail,
            productionRequestId: request.ProductionRequestId);

        await _reports.AddAsync(report, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await OperationalDelayNotificationSupport.TryDispatchProductionDelayReportedAsync(
            _notifications,
            _projects,
            _logger,
            report,
            project,
            productionRequest,
            cancellationToken);

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
            request.DeliveryReasonCode,
            request.ReasonDetail,
            orderId: request.OrderId,
            deliveryId: request.DeliveryId);

        await _reports.AddAsync(report, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await OperationalDelayNotificationSupport.TryDispatchDeliveryDelayReportedAsync(
            _notifications,
            _projects,
            _productionRequests,
            _logger,
            report,
            project,
            cancellationToken);

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

    public async Task<ServiceResult<OperationalDelayReportDto>> ResolveAsync(
        Guid reportId,
        Guid currentUserId,
        ResolveReportRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (reportId == Guid.Empty || currentUserId == Guid.Empty)
        {
            return BadRequest(OperationalDelayReportErrorCodes.InvalidRequest, "Report id is required.");
        }

        var noteError = ValidateResolutionNote(request.ResolutionNote);
        if (noteError is not null)
        {
            return noteError;
        }

        var report = await _reports.GetByIdAsync(reportId, cancellationToken);
        if (report is null)
        {
            return NotFound(OperationalDelayReportErrorCodes.ReportNotFound, ReportNotFoundMessage);
        }

        var project = await _projects.GetByIdAsync(report.ProjectId, cancellationToken);
        if (project is null)
        {
            return NotFound(OperationalDelayReportErrorCodes.ProjectNotFound, ProjectNotFoundMessage);
        }

        var roleName = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!await CanResolveReportAsync(report, project, roleName, currentUserId, cancellationToken))
        {
            return Forbidden();
        }

        var normalizedNote = ReportResolutionSupport.NormalizeResolutionNote(request.ResolutionNote);
        ReportResolutionSupport.ApplyResolveTransition(
            report.Status,
            () =>
            {
                report.Status = ReportResolutionStatus.RESOLVED;
                report.ResolvedAt = DateTime.UtcNow;
                report.ResolutionNote = normalizedNote;
            });

        _reports.Update(report);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var detail = await _reports.GetDetailAsync(reportId, cancellationToken);
        return ServiceResult<OperationalDelayReportDto>.Success(
            ToDto(detail!),
            "Operational delay report resolved successfully.");
    }

    private static OperationalDelayReport BuildProductionReport(
        Guid projectId,
        OperationalDelayPhase phase,
        DateOnly deadlineSnapshot,
        DateTime reportedAt,
        Guid reportedBy,
        string productionReasonCode,
        string reasonDetail,
        Guid? productionRequestId = null,
        Guid? orderId = null,
        Guid? deliveryId = null)
    {
        _ = OperationalDelayReasonCodeSupport.TryParseProductionReasonCode(
            productionReasonCode,
            out var parsedProductionReasonCode);

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
            ProductionReasonCode = parsedProductionReasonCode,
            DeliveryReasonCode = null,
            ReasonDetail = reasonDetail.Trim(),
            ReportedBy = reportedBy,
            ReportedAt = reportedAt,
            CreatedAt = reportedAt,
            Status = ReportResolutionStatus.OPEN
        };
    }

    private static OperationalDelayReport BuildDeliveryReport(
        Guid projectId,
        OperationalDelayPhase phase,
        DateOnly deadlineSnapshot,
        DateTime reportedAt,
        Guid reportedBy,
        string deliveryReasonCode,
        string reasonDetail,
        Guid? orderId = null,
        Guid? deliveryId = null)
    {
        _ = OperationalDelayReasonCodeSupport.TryParseDeliveryReasonCode(
            deliveryReasonCode,
            out var parsedDeliveryReasonCode);

        return new OperationalDelayReport
        {
            OperationalDelayReportId = Guid.NewGuid(),
            ProjectId = projectId,
            ReportPhase = phase,
            ProductionRequestId = null,
            OrderId = orderId,
            DeliveryId = deliveryId,
            DeadlineSnapshot = deadlineSnapshot,
            DelayState = OperationalDelayClassificationSupport.DeriveDelayState(deadlineSnapshot, reportedAt),
            ProductionReasonCode = null,
            DeliveryReasonCode = parsedDeliveryReasonCode,
            ReasonDetail = reasonDetail.Trim(),
            ReportedBy = reportedBy,
            ReportedAt = reportedAt,
            CreatedAt = reportedAt,
            Status = ReportResolutionStatus.OPEN
        };
    }

    private async Task<bool> CanResolveReportAsync(
        OperationalDelayReport report,
        Project project,
        string? roleName,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        return report.ReportPhase switch
        {
            OperationalDelayPhase.PRODUCTION when report.ProductionRequestId.HasValue =>
                await CanManageProductionReportAsync(
                    project,
                    roleName,
                    currentUserId,
                    report.ProductionRequestId.Value,
                    cancellationToken),
            OperationalDelayPhase.DELIVERY =>
                await CanViewStaffReportsAsync(
                    project.ProjectId,
                    project.AssignedSalesId,
                    roleName,
                    currentUserId,
                    cancellationToken),
            _ => false
        };
    }

    private static OperationalDelayReport BuildReport(
        Guid projectId,
        OperationalDelayPhase phase,
        DateOnly deadlineSnapshot,
        DateTime reportedAt,
        Guid reportedBy,
        string reasonCode,
        string reasonDetail,
        Guid? productionRequestId = null,
        Guid? orderId = null,
        Guid? deliveryId = null)
    {
        return phase switch
        {
            OperationalDelayPhase.PRODUCTION => BuildProductionReport(
                projectId,
                phase,
                deadlineSnapshot,
                reportedAt,
                reportedBy,
                reasonCode,
                reasonDetail,
                productionRequestId,
                orderId,
                deliveryId),
            OperationalDelayPhase.DELIVERY => BuildDeliveryReport(
                projectId,
                phase,
                deadlineSnapshot,
                reportedAt,
                reportedBy,
                reasonCode,
                reasonDetail,
                orderId,
                deliveryId),
            _ => throw new InvalidOperationException("Unsupported operational delay report phase.")
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

        return OperationalDelayReasonCodeSupport.ValidateProductionReasonCode<OperationalDelayReportDto>(
            request.ProductionReasonCode,
            request.DeliveryReasonCode,
            request.ReasonDetail);
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

        return OperationalDelayReasonCodeSupport.ValidateDeliveryReasonCode<OperationalDelayReportDto>(
            request.ProductionReasonCode,
            request.DeliveryReasonCode,
            request.ReasonDetail);
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
            ProductionReasonCode = report.ProductionReasonCode?.ToString(),
            DeliveryReasonCode = report.DeliveryReasonCode?.ToString(),
            ReasonDetail = report.ReasonDetail,
            ReportedBy = report.ReportedBy,
            ReporterName = reporterName,
            ReportedAt = report.ReportedAt,
            CreatedAt = report.CreatedAt,
            Status = report.Status.ToString(),
            ResolvedAt = report.ResolvedAt,
            ResolutionNote = report.ResolutionNote
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
            ProductionReasonCode = item.ProductionReasonCode?.ToString(),
            DeliveryReasonCode = item.DeliveryReasonCode?.ToString(),
            ReasonDetail = item.ReasonDetail,
            ReportedBy = item.ReportedBy,
            ReporterName = item.ReporterName,
            ReportedAt = item.ReportedAt,
            CreatedAt = item.CreatedAt,
            Status = item.Status.ToString(),
            ResolvedAt = item.ResolvedAt,
            ResolutionNote = item.ResolutionNote
        };
    }

    private static OperationalDelayReportDto ToDto(OperationalDelayReportDetailReadModel item)
    {
        var dto = ToDto((OperationalDelayReportListItemReadModel)item);
        dto.ProjectName = item.ProjectName;
        return dto;
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

    private static ServiceResult<OperationalDelayReportDto>? ValidateResolutionNote(string? note)
    {
        var normalized = ReportResolutionSupport.NormalizeResolutionNote(note);
        if (normalized?.Length > ReportResolutionSupport.MaxResolutionNoteLength)
        {
            return BadRequest(
                OperationalDelayReportErrorCodes.ResolutionNoteTooLong,
                $"Resolution note must not exceed {ReportResolutionSupport.MaxResolutionNoteLength} characters.");
        }

        return null;
    }
}
