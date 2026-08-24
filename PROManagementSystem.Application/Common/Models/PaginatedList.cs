namespace PROManagementSystem.Application.Common.Models;

public class PaginatedList<T>
{
    public IReadOnlyList<T> Items { get; }

    public int Page { get; }

    public int PageSize { get; }

    public int TotalCount { get; }

    public int TotalPages =>
        PageSize == 0
            ? 0
            : (int)Math.Ceiling(
                TotalCount / (double)PageSize);

    public bool HasPreviousPage =>
        Page > 1;

    public bool HasNextPage =>
        Page < TotalPages;

    public PaginatedList(
        IReadOnlyList<T> items,
        int totalCount,
        int page,
        int pageSize)
    {
        Items = items;
        TotalCount = totalCount;
        Page = page;
        PageSize = pageSize;
    }
}