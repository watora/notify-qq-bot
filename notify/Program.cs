using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Notify.Domain.Config;
using Notify.Domain.Models.Response;
using Notify.Repository;
using Notify.Service;
using Notify.Service.Hubs;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEntityConfigurationSource();
builder.SetConfiguration();

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();
builder.Services.AddMemoryCache();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();
builder.Services.AddSignalR();

builder.Services.AddNotifyServices();
builder.Services.AddDbContext<NotifyContext>();
builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(@"./persist-keys"));
builder.Services.AddAuthentication(opt =>
{
    opt.DefaultAuthenticateScheme = "Github";
    opt.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    opt.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    opt.DefaultSignOutScheme = CookieAuthenticationDefaults.AuthenticationScheme;
}).AddCookie(opt =>
{
    opt.Cookie.Name = "auth_info";
    opt.LoginPath = "/";
    opt.ExpireTimeSpan = TimeSpan.FromDays(14);
    opt.Events.OnRedirectToLogin = context =>
    {
        if (context.Request.Path.Value?.StartsWith("/api") ?? false)
        {
            context.Response.Headers.Location = context.RedirectUri;
            context.Response.StatusCode = 401;
        }
        return Task.CompletedTask;
    };
}).AddOAuth("Github", opt =>
{
    opt.ClientId = builder.Configuration["NOTIFY_GITHUB_CLIENTID"] ?? "";
    opt.ClientSecret = builder.Configuration["NOTIFY_GITHUB_SECRET"] ?? "";
    opt.AuthorizationEndpoint = "https://github.com/login/oauth/authorize";
    opt.TokenEndpoint = "https://github.com/login/oauth/access_token";
    opt.CallbackPath = "/oauth-github";
    opt.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    opt.SaveTokens = true;
    opt.Events.OnCreatingTicket = OAuth.OnCreatingTicket;
});
builder.Services.AddAuthorization();
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = actionContext =>
    {
        return new ObjectResult(new CommonResponse(ErrorCodes.InvalidRequest));
    };
});
builder.Services.AddSerilog((sp, opt) => opt.ReadFrom.Configuration(builder.Configuration));
if (!builder.Environment.IsDevelopment()) 
{
    builder.WebHost.UseStaticWebAssets();
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCookiePolicy(new CookiePolicyOptions
{
    Secure = CookieSecurePolicy.Always,
    MinimumSameSitePolicy = SameSiteMode.None
});

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapControllers();
app.MapHub<PrivateHub>("/signalr/chathub/private");
app.MapRazorComponents<Notify.Client.Index>()
    .AddInteractiveWebAssemblyRenderMode();

app.Run();
