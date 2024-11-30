using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.OAuth;
using Notify.Domain.Config;
using Notify.Domain.Models;
using System.Net.Http.Headers;

namespace Notify.Service;

public class OAuth
{
    public static async Task OnCreatingTicket(OAuthCreatingTicketContext context)
    {
        var sp = context.HttpContext.RequestServices;
        var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
        var httpClient = httpClientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("notify-webapi");
        httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        var configuration = sp.GetRequiredService<IConfiguration>();
        var token = context.AccessToken;
        var req = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        var resp = await httpClient.SendAsync(req);
        if (resp.IsSuccessStatusCode)
        {
            var userInfo = JsonSerializer.Deserialize<GithubUserInfo>(await resp.Content.ReadAsStringAsync());
            if (userInfo != null)
            {
                var claims = new List<Claim> {
                    new("Login", userInfo.Login),
                    new("ShowName", userInfo.Name),
                    new("Avatar", userInfo.AvatarUrl),
                    new(ClaimTypes.Name, $"Github_{userInfo.Login}"),
                };
                if (configuration.GetSection("Auth:Admin").Get<List<string>>()?.Contains(userInfo.Login) ?? false)
                {
                    claims.Add(new Claim(ClaimTypes.Role, Consts.RoleAdmin));
                }
                context.Identity?.AddClaims(claims);
                return;
            }
            else
            {
                throw new Exception("user not exist");
            }
        }
        else
        {
            throw new Exception("failed to access user info");
        }
    }
}