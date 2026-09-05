using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.OperationalDelayReports;
using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.Common.OperationalDelayReports;

public static class OperationalDelayReasonCodeSupport
{
    public static ServiceResult<T>? ValidateProductionReasonCode<T>(
        string? productionReasonCode,
        string? deliveryReasonCode,
        string reasonDetail)
    {
        if (!string.IsNullOrWhiteSpace(deliveryReasonCode))
        {
            return ServiceResult<T>.Failure(
                Error.Validation(
                    OperationalDelayReportErrorCodes.InvalidRequest,
                    "Delivery reason code is not accepted for production delay reports."));
        }

        if (string.IsNullOrWhiteSpace(productionReasonCode))
        {
            return ServiceResult<T>.Failure(
                Error.Validation(
                    OperationalDelayReportErrorCodes.InvalidRequest,
                    "Production reason code is required."));
        }

        if (!TryParseProductionReasonCode(productionReasonCode, out _))
        {
            return ServiceResult<T>.Failure(
                Error.Validation(
                    OperationalDelayReportErrorCodes.InvalidRequest,
                    "Production reason code is invalid."));
        }

        return ValidateReasonDetail<T>(reasonDetail);
    }

    public static ServiceResult<T>? ValidateDeliveryReasonCode<T>(
        string? productionReasonCode,
        string? deliveryReasonCode,
        string reasonDetail)
    {
        if (!string.IsNullOrWhiteSpace(productionReasonCode))
        {
            return ServiceResult<T>.Failure(
                Error.Validation(
                    OperationalDelayReportErrorCodes.InvalidRequest,
                    "Production reason code is not accepted for delivery delay reports."));
        }

        if (string.IsNullOrWhiteSpace(deliveryReasonCode))
        {
            return ServiceResult<T>.Failure(
                Error.Validation(
                    OperationalDelayReportErrorCodes.InvalidRequest,
                    "Delivery reason code is required."));
        }

        if (!TryParseDeliveryReasonCode(deliveryReasonCode, out _))
        {
            return ServiceResult<T>.Failure(
                Error.Validation(
                    OperationalDelayReportErrorCodes.InvalidRequest,
                    "Delivery reason code is invalid."));
        }

        return ValidateReasonDetail<T>(reasonDetail);
    }

    public static bool TryParseProductionReasonCode(
        string value,
        out ProductionDelayReasonCode reasonCode)
    {
        reasonCode = default;
        if (!Enum.TryParse(value.Trim(), ignoreCase: true, out reasonCode))
        {
            return false;
        }

        return Enum.IsDefined(typeof(ProductionDelayReasonCode), reasonCode);
    }

    public static bool TryParseDeliveryReasonCode(
        string value,
        out DeliveryDelayReasonCode reasonCode)
    {
        reasonCode = default;
        if (!Enum.TryParse(value.Trim(), ignoreCase: true, out reasonCode))
        {
            return false;
        }

        return Enum.IsDefined(typeof(DeliveryDelayReasonCode), reasonCode);
    }

    private static ServiceResult<T>? ValidateReasonDetail<T>(string reasonDetail)
    {
        const int maxReasonDetailLength = 4000;

        if (string.IsNullOrWhiteSpace(reasonDetail))
        {
            return ServiceResult<T>.Failure(
                Error.Validation(
                    OperationalDelayReportErrorCodes.InvalidRequest,
                    "Reason detail is required."));
        }

        if (reasonDetail.Trim().Length > maxReasonDetailLength)
        {
            return ServiceResult<T>.Failure(
                Error.Validation(
                    OperationalDelayReportErrorCodes.InvalidRequest,
                    $"Reason detail must not exceed {maxReasonDetailLength} characters."));
        }

        return null;
    }
}
