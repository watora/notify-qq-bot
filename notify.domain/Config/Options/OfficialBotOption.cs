namespace Notify.Domain.Config.Options;

public class OfficialBotOption
{
    public bool Enabled { get; set; }
    public string AppId { get; set; } = "";
    public string AppSecret { get; set; } = "";
    public string SendSecret { get; set; } = "";
    public string ApiEndpoint { get; set; } = "https://api.bot.qq.com";
    public string TokenEndpoint { get; set; } = "https://api.bot.qq.com/app/getAppAccessToken";
    public Dictionary<string, string> PrivateTargetMappings { get; set; } = new();
    public Dictionary<string, string> GroupTargetMappings { get; set; } = new();
}
