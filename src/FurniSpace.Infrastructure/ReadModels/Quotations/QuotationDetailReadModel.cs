namespace FurniSpace.Infrastructure.ReadModels.Quotations;

public sealed class QuotationDetailReadModel : QuotationReadModel
{
    public IReadOnlyList<QuotationItemReadModel> Items { get; set; } = [];
}
