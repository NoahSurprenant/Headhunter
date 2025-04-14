using Microsoft.EntityFrameworkCore;

namespace Headhunter.API.Pagination;

public static class PaginationExtensions
{
    public static async Task<PaginationResult<T>> PaginationResult<T>(this IQueryable<T> query, PaginationFilter filter, CancellationToken ct = default)
    {
        var count = await query.CountAsync(ct);
        if (count is 0)
        {
            return new()
            {
                TotalCount = count,
                Results = Array.Empty<T>(),
            };
        }

        var results = await query
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToArrayAsync(ct);

        return new()
        {
            TotalCount = count,
            Results = results,
        };
    }
}