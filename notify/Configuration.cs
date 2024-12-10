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
        builder.Services.Configure<ChatBotOption>(opt => {
            opt.OpenAIEndPoint = builder.Configuration["OPENAI_ENDPOINT"];
            opt.OpenAIToken = builder.Configuration["OPENAI_TOKEN"];
        });
    }
}