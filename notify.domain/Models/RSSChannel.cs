namespace Notify.Domain.Models;

public class RSSChannel
{
    public required string Title { get; set; }
    public required string Link { get; set; }
    public required string Description { get; set; }
    public required List<RSSItem> Items { get; set; }
}

public class RSSItem
{
    public required string Title { get; set; }
    public required string Link { get; set; }
    public required string Description { get; set; }
    public string? Guid { get; set; }
    public required string Author { get; set; }
    public string? Category { get; set; }
    public required string PubDate { get; set; }
    public string? RelatedImg { get; set; }
}