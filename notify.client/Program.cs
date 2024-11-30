using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Notify.Client.Utils;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddAntDesign();

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<BlazorHttpHelper>();

await builder.Build().RunAsync();
