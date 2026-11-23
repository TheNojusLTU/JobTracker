namespace JobTracker.Api;

public record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);

    public PagedResponse<TResult> Map<TResult>(Func<T, TResult> map) =>
        PagedResponse<TResult>.From(Items, Page, PageSize, TotalCount, map);

    public static PagedResponse<T> From<TSource>(
        IEnumerable<TSource> items, int page, int pageSize, int totalCount, Func<TSource, T> map) =>
        new(items.Select(map).ToList(), page, pageSize, totalCount);
}
