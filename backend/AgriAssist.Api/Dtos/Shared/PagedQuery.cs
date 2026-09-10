namespace AgriAssist.Api.Dtos.Shared;

public class PagedQuery
{
    public string? Search { get; set; }
    public string? SortBy { get; set; }
    public string? SortDirection { get; set; } = "asc";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;

    public void Normalize()
    {
        Page = Math.Max(1, Page);
        PageSize = Math.Clamp(PageSize, 1, 100);
        SortDirection = string.Equals(SortDirection, "desc", StringComparison.OrdinalIgnoreCase) ? "desc" : "asc";
    }
}
