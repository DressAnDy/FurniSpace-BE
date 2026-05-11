using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace FurniSpace.Domain.Specifications;

public abstract class BaseSpecification<T>
{
    public Expression<Func<T, bool>>? Criteria { get; protected set; }
    public List<Expression<Func<T, object>>> Includes { get; } = new();
    public Expression<Func<T, object>>? OrderBy { get; protected set; }
    public bool IsPagingEnabled { get; protected set; }
    public int Skip { get; protected set; }
    public int Take { get; protected set; }

    protected void ApplyPaging(int page, int pageSize)
    {
        Skip = (page - 1) * pageSize;
        Take = pageSize;
        IsPagingEnabled = true;
    }
}
