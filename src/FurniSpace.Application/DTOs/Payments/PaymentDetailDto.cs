using FurniSpace.Domain.Enums;
using Mapster;

namespace FurniSpace.Application.DTOs.Payments;

public sealed class PaymentDetailDto : PaymentDto
{
    public static PaymentDetailDto From(PaymentDto source)
    {
        return source.Adapt<PaymentDetailDto>();
    }
}
