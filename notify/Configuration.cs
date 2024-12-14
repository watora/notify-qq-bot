using Notify.Utils;
using Notify.Domain.Config.Options;

public static class ProgramConfiguration
{
    public static ConfigurationManager AddEntityConfigurationSource(this ConfigurationManager manager)
    {
        var dbPath = manager["Db:Path"];
        if (!string.IsNullOrEmpty(dbPath))
        {
            IConfigurationBuilder builder = manager;
            builder.Add(new EntityConfigurationSource(dbPath));
        }
        return manager;
    }

    public static void SetConfiguration(this WebApplicationBuilder builder)
    {
        var configuration = builder.Configuration;
        builder.Services.Configure<ChatBotOption>(opt =>
        {
            opt.OpenAIEndpoint = configuration["OPENAI_ENDPOINT"];
            opt.OpenAIToken = configuration["OPENAI_TOKEN"];
            opt.AnthropicEndpoint = configuration["ANTHROPIC_ENDPOINT"];
            opt.AnthropicToken = configuration["ANTHROPIC_TOKEN"];
        });
        builder.Services.Configure<AuthOption>(opt =>
        {
            opt.Admin = configuration["Admin"]?.Split(",").ToList() ?? new List<string>();
            opt.User = configuration["User"]?.Split(",").ToList() ?? new List<string>();
            opt.Owner = configuration["Owner"] ?? "";
        });
        builder.Services.Configure<OneBotOption>(opt => {
            opt.PostSecret = configuration["ONEBOT_EVENT_SECRET"] ?? "";
            opt.OneBotEndpoint = configuration["OneBot:Url"] ?? "";
        });
    }
}