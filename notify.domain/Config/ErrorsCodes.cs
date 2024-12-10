namespace Notify.Domain.Config;

public static class ErrorCodes
{
    public static (string, string) InvalidRequest = ("10000", "参数校验不通过");
    public static (string, string) GetRSSConfigError = ("20000", "读取RSS配置失败");
    public static (string, string) SaveRSSConfigError = ("20001", "保存RSS配置失败");
    public static (string, string) RSSConfigNotExist = ("20002", "RSS配置不存在");
    public static (string, string) GetChatConfigError = ("20010", "读取聊天配置失败");
    public static (string, string) SaveChatConfigError = ("20011", "读取聊天配置失败");
    public static (string, string) ChatConfigNotExist = ("20012", "聊天配置不存在");
    public static (string, string) GetEntityConfigError = ("20020", "读取全局配置失败");
    public static (string, string) SaveEntityConfigError = ("20001", "保存全局配置失败");
}