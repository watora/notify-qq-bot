using System.Text.Json;
using AntDesign;
using Notify.Domain.Models.Response;


namespace Notify.Client.Utils;

public class BlazorHttpHelper
{
    private IMessageService messageService;

    public BlazorHttpHelper(IMessageService messageService)
    {
        this.messageService = messageService;
    }

    public async Task HandleResponse(HttpResponseMessage resp, string failMessage, string successMessage, Action? onSuccess = null)
    {
        if (!resp.IsSuccessStatusCode)
        {
            if (!string.IsNullOrEmpty(failMessage))
            {
                _ = messageService.Error($"{failMessage} {resp.StatusCode}", 10);
            }
            return;
        }
        var content = await resp.Content.ReadAsStringAsync();
        var respObj = JsonSerializer.Deserialize<CommonResponse>(content);
        if (respObj != null && respObj.Code == "0")
        {
            if (!string.IsNullOrEmpty(successMessage)) 
            {
                _ = messageService.Info(successMessage);
            }
            if (onSuccess != null) 
            {
                onSuccess();
            }
        }
        else
        {
            if (!string.IsNullOrEmpty(failMessage))
            {
                _ = messageService.Error($"{failMessage} {respObj?.Message ?? ""}", 10);
            }
        }
    }


    public async Task HandleResponse<T>(HttpResponseMessage resp, string failMessage, Action<T> onSuccess)
    {
        if (!resp.IsSuccessStatusCode)
        {
            if (!string.IsNullOrEmpty(failMessage))
            {
                _ = messageService.Error($"{failMessage} {resp.StatusCode}", 10);
            }
            return;
        }
        var content = await resp.Content.ReadAsStringAsync();
        var respObj = JsonSerializer.Deserialize<CommonResponse<T>>(content);
        if (respObj != null && respObj.Code == "0" && respObj.Data != null)
        {
            onSuccess(respObj.Data);
        }
        else
        {
            if (!string.IsNullOrEmpty(failMessage))
            {
                _ = messageService.Error($"{failMessage} {respObj?.Message ?? ""}", 10);
            }
        }
    }
}