namespace ShopeeMini.Application.Common.Models;

public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public long TotalCount { get; set; }
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public record PaginationQuery
{
    private const int MaxPageSize = 100;
    private int _pageSize = 20;

    public int Page { get; set; } = 1;

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value <= 0 ? 20 : Math.Min(value, MaxPageSize);
    }

    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }

    public int Skip => (Math.Max(Page, 1) - 1) * PageSize;
}
