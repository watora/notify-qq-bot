using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Notify.Domain.Models.Response;

namespace notify.Controllers;

[ApiController]
[Route("api/user")]
public class UserController : ControllerBase
{
    ILogger<UserController> logger;
    public UserController(ILogger<UserController> logger)
    {
        this.logger = logger;
    }

    [HttpGet("oauth/github")]
    public ActionResult GithubChallenge()
    {
        var prop = new AuthenticationProperties{
            RedirectUri = "/"
        };
        return Challenge(prop, "Github");
    }

    //[Authorize(AuthenticationSchemes = "Github")]
    //[HttpGet("oauth/login")]
    //public ActionResult OAuthLogin()
    //{
    //    logger.LogInformation($"user {HttpContext.User?.Identity?.Name ?? ""} login");
    //    var authInfo = HttpContext.Request.Cookies["auth_info"];
    //    return Redirect($"/authentication?token={authInfo}");
    //}

    [HttpGet("info")]
    public async Task<ActionResult<CommonResponse<GetUserInfoResponse>>> GetUserInfo()
    {
        var auth = await HttpContext.AuthenticateAsync();
        var resp = new GetUserInfoResponse();
        if (auth.Succeeded && auth.Principal != null)
        {
            resp.IsAuthed = true;
            resp.Id = auth.Principal.Identity!.Name;
            resp.Name = auth.Principal.Claims.FirstOrDefault(r => r.Type == "ShowName")?.Value;
            resp.AvatarUrl = auth.Principal.Claims.FirstOrDefault(r => r.Type == "Avatar")?.Value;
        }
        return new CommonResponse<GetUserInfoResponse>(resp);
    }
}