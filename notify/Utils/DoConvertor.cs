using Notify.Domain.Models;
using Notify.Repository.Entity;

public static class DoConvertor
{
    public static RSSConfigDO ToRSSConfigDO(this RSSConfig config)
    {
        return new RSSConfigDO
        {
            Id = config.Id,
            SubscribeChannel = config.SubscribeChannel,
            SubscribeId = config.SubscribeId,
            MsgTargetId = config.MsgTargetId,
            MsgTargetType = config.MsgTargetType,
            LastCheckTime = config.LastCheckTime,
            LastCheckTimeStr = config.LastCheckTime > 0 ? DateTimeOffset.FromUnixTimeSeconds(config.LastCheckTime).ToString("yyyy-MM-dd HH:mm:ss") : "",
            LastMsgSendTime = config.LastMsgSendTime,
            LastMsgSendTimeStr = config.LastMsgSendTime > 0 ? DateTimeOffset.FromUnixTimeSeconds(config.LastMsgSendTime).ToString("yyyy-MM-dd HH:mm:ss") : "",
            CheckInterval = config.CheckInterval,
            Creator = config.Creator,
            IsActive = config.IsActive,
            ExpCheckTime = config.ExpCheckTime,
            ExpCheckTimeStr = config.ExpCheckTime > 0 ? DateTimeOffset.FromUnixTimeSeconds(config.ExpCheckTime).ToString("yyyy-MM-dd HH:mm:ss") : "",
            Comment = config.Comment
        };
    }

    public static RSSConfig ToRSSConfigEntity(this RSSConfigDO configDO)
    {
        return new RSSConfig
        {
            Id = configDO.Id,
            SubscribeChannel = configDO.SubscribeChannel,
            SubscribeId = configDO.SubscribeId,
            MsgTargetId = configDO.MsgTargetId,
            MsgTargetType = configDO.MsgTargetType,
            CheckInterval = configDO.CheckInterval,
            IsActive = configDO.IsActive,
            Creator = configDO.Creator,
            Comment = configDO.Comment,
        };
    }

    public static ChatConfig ToChatConfigEntity(this ChatConfigDO configDO)
    {
        return new ChatConfig
        {
            Id = configDO.Id,
            TargetId = configDO.TargetId!,
            TargetType = configDO.TargetType!,
            Model = configDO.Model!,
            Provider = configDO.Provider!,
            Comment = configDO.Comment!,
            IsActive = configDO.IsActive,
            Creator = configDO.Creator!
        };
    }

    public static ChatConfigDO ToChatConfigDO(this ChatConfig configDO)
    {
        return new ChatConfigDO
        {
            Id = configDO.Id,
            TargetId = configDO.TargetId,
            TargetType = configDO.TargetType,
            Model = configDO.Model,
            Provider = configDO.Provider,
            Comment = configDO.Comment,
            IsActive = configDO.IsActive,
            Creator = configDO.Creator
        };
    }
}
