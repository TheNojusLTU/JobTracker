using JobTracker.Contracts;
using Microsoft.EntityFrameworkCore;

namespace JobTracker.Extensions;

public static class PagingExtensions
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public static async Task<PagedResponse<TResult>> ToPagedResponseAsync<TSource, TResult>(
        this IQueryable<TSource> query,
        int page,
        int pageSize,
        Func<TSource, TResult> map)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(map);

        var totalCount = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResponse<TResult>(items.Select(map).ToList(), page, pageSize, totalCount);
    }

    public static Dictionary<string, string[]> ValidatePaging(int page, int pageSize)
    {
        var errors = new Dictionary<string, string[]>();

        if (page < 1)
        {
            errors["page"] = ["Page must be 1 or higher."];
        }

        if (pageSize is < 1 or > MaxPageSize)
        {
            errors["pageSize"] = [$"Page size must be between 1 and {MaxPageSize}."];
        }

        // multiply as long so a huge page can't overflow the int offset used by Skip
        if (errors.Count == 0 && (long)(page - 1) * pageSize > int.MaxValue)
        {
            errors["page"] = ["Page is too large."];
        }

        return errors;
    }
}
