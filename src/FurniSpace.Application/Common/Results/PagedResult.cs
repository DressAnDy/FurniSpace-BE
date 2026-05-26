#nullable enable

using System;
using System.Collections.Generic;

namespace FurniSpace.Application.Common;

public sealed class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; }
    public int Page { get; }
    public int PageSize { get; }
    public int TotalItems { get; }
    public int TotalPages { get; }
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;

    public PagedResult(IReadOnlyList<T> items, int page, int pageSize, int totalItems)
    {
        ArgumentNullException.ThrowIfNull(items);

        if (page < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(page), "Page must be greater than zero.");
        }

        if (pageSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be greater than zero.");
        }

        if (totalItems < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalItems), "Total items cannot be negative.");
        }

        Items = items;
        Page = page;
        PageSize = pageSize;
        TotalItems = totalItems;
        TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
    }

    public static PagedResult<T> Create(IReadOnlyList<T> items, int page, int pageSize, int totalItems)
    {
        return new PagedResult<T>(items, page, pageSize, totalItems);
    }
}
