namespace FurniSpace.Infrastructure.Common.Search;

public enum SearchFilterOperator
{
    Term,
    Terms,
    RangeGte,
    RangeLte,
    Exists,
    NotExists
}

public sealed record SearchFilter(
    string Field,
    SearchFilterOperator Operator,
    object? Value = null,
    IReadOnlyList<object>? Values = null);
