using FurniSpace.Domain.Enums;
using Mapster;

namespace FurniSpace.Application.DTOs.Payments;

public sealed class PaymentDetailDto : PaymentDto
{
    public bool? Reused { get; set; }

    public static PaymentDetailDto From(PaymentDto source)
    {
        return source.Adapt<PaymentDetailDto>();
    }
}
