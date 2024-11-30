using System.Text.Json.Serialization;

namespace Notify.Domain.Models.Response;

public class CommonResponse
{
    public CommonResponse()
    {
        this.Code = "0";
    }

    public CommonResponse((string, string) err)
    {
        this.Code = err.Item1;
        this.Message = err.Item2;
    }

    [JsonPropertyName("code")]
    public string Code { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

}

public class CommonResponse<T>
{
    [JsonConstructor]
    public CommonResponse(T data)
    {
        this.Data = data;
        this.Code = "0";
    }

    public CommonResponse((string, string) err)
    {
        this.Code = err.Item1;
        this.Message = err.Item2;
    }

    [JsonPropertyName("data")]
    public T? Data { get; set; }

    [JsonPropertyName("code")]
    public string Code { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}