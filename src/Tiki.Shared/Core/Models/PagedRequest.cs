namespace Tiki.Shared.Core.Models;

public class PagedRequest
{
    public int PageNum { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}