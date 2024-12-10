using System.Text.Json.Serialization;

namespace Notify.Domain.Models.Response;

public class Page<T>
{
    public Page(List<T> items, bool hasNext, long totalCount) 
    {
        Items = items;
        HasNext = hasNext;
        TotalCount = totalCount;
    }

    [JsonPropertyName("items")]
    public List<T> Items { get; set; }

    [JsonPropertyName("has_next")]
    public bool HasNext { get; set; }

    [JsonPropertyName("total_count")]
    public long TotalCount { get; set; }
}