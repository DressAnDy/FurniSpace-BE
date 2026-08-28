using System;

namespace FurniSpace.Domain.Common;

public abstract class AdminFinancialProjectPeriodCollectionShape
{
    public decimal CollectedInPeriod { get; set; }
    public DateTime? LastPaidInPeriod { get; set; }
}
