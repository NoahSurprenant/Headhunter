namespace Headhunter.API.Pagination;

public class PaginationFilter
{
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public SortDirection? SortDirection { get; set; }
    public string? SortColumn { get; set; }
    public PaginationFilter()
    {
        this.PageNumber = 1;
        this.PageSize = 10;
    }
    public PaginationFilter(int pageNumber, int pageSize)
    {
        this.PageNumber = pageNumber < 1 ? 1 : pageNumber;
        this.PageSize = pageSize > 250 ? 250 : pageSize;
    }
}

public enum SortDirection
{
    Asc,
    Desc,
    None,
}
