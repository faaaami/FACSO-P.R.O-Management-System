namespace PROManagementSystem.Application.Common.Models;

public class PaginationRequest
{
    private const int MaxPageSize = 100;

    private int _page = 1;
    private int _pageSize = 20;

    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value switch
        {
            < 1 => 20,
            > MaxPageSize => MaxPageSize,
            _ => value
        };
    }

    public string? SortBy { get; set; }

    public string SortDir { get; set; } = "asc";

    public string? Search { get; set; }
}