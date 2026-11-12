使用 QQ 官方 Bot API 发送消息的 QQ 机器人
- blazor实现web配置端
- 通过rsshub订阅并推送更新到qq
- 接入了gpt和claude，可以通过qq直接对话

## OpenAI Responses API

新增 `ChatBotOpenAIResponses`，使用标准 `POST /v1/responses` 和 Bearer 鉴权，作为独立的 `OpenAIResponses` 提供商。原有 `OpenAI`（Chat Completions）和 `Anthropic` 接口继续可用。

配置环境变量：

```text
OPENAI_RESPONSES_ENDPOINT=https://api.openai.com
OPENAI_RESPONSES_TOKEN=你的API密钥
```

未配置专用 endpoint/token 时分别复用 `OPENAI_ENDPOINT` / `OPENAI_TOKEN`；endpoint 均未配置时默认 `https://api.openai.com`。地址可填根地址或以 `/v1` 结尾的 API 基础地址，不要填写完整 `/responses` 路径。

在聊天配置页面选择 **OpenAI Responses** 并填写服务实际支持的模型名称，即可用于官方 Bot 或 OneBot 聊天。网页聊天页面也可选择 **OpenAI Responses**、填写模型后发送消息。只使用 Responses 时无需配置 Anthropic 或 Chat Completions 服务。

业务代码可直接注入 `Notify.Service.ChatBot.ChatBotOpenAIResponses`：

```csharp
var input = OpenAIResponseInput.FromText(modelName, "你好");
input.Instructions = "请简洁回答。"; // 可选
input.MaxOutputTokens = 2048;       // 可选
var response = await responses.Chat(conversationKey, input, cancellationToken);
var text = response?.GetReplyText();
```

支持 `input_text` 和 `input_image`（图片 URL 或 Base64 data URL），响应从 `output` 内提取 `output_text`，也处理 `refusal`。OneBot 输入沿用现有图片读取能力；官方 QQ 回调和网页聊天目前只传入文本。

多轮上下文按 API 地址、请求模型、会话和 instructions 隔离，缓存 12 小时无访问后失效。请求使用 `store=false`，后续请求携带完整历史输出（含 reasoning 和 phase），并请求 `reasoning.encrypted_content`。成功完成且含有文本/拒绝内容的响应才写入历史，失败、未完成或无文本的响应返回 `null`，不会污染下一轮。网络异常会抛给调用方。历史最多连续保留 25 轮，下一轮开启新会话，以免截断推理项依赖的历史前缀；进程重启也会清除历史。

本实现与现有聊天接口一致，采用非流式文本回复；不包含流式 SSE、工具调用执行或后台响应轮询。协议及历史管理依据 [OpenAI Responses API](https://developers.openai.com/api/reference/resources/responses/methods/create) 和 [Conversation state](https://developers.openai.com/api/docs/guides/conversation-state)。

## QQ 官方 Bot

所有消息发送统一使用 QQ 官方 Bot API，包括 RSS 推送、聊天回复和管理端测试消息。提供 `OfficialBotApi`、`OfficialBotEvent` 和独立 HTTP 回调，支持 QQ 单聊、群 @ 消息和全量群消息的接收，以及单聊/群聊文本、图片发送、主动发送和被动回复。官方回调的 GPT/Claude 自动回复目前处理文本；附件和其他消息字段会保留在事件模型中，但不自动做图片、语音、视频识别。OneBot 可继续接收消息、读取引用和图片，但不再调用 OneBot 发送接口。

### 配置与接入

AppSecret 仅从环境变量 `OFFICIALBOT_APP_SECRET` 读取。其他参数可在 `notify/appsettings.json` 的 `OfficialBot` 节配置，或使用环境变量：

```text
OfficialBot__Enabled=true
OfficialBot__AppId=你的AppID
OFFICIALBOT_APP_SECRET=你的AppSecret
```

默认 API 地址为新版官方文档的 `https://api.bot.qq.com`，取凭证地址为 `https://api.bot.qq.com/app/getAppAccessToken`。需要调整时可设置 `OfficialBot__ApiEndpoint`、`OfficialBot__TokenEndpoint`，均要求 HTTPS。凭证请通过部署环境注入，不要提交到仓库；默认不启用官方 Bot。

1. 在 QQ 开放平台配置 HTTPS 回调地址：`https://你的域名/api/officialbot/event`。该接口处理 `op=13` 地址验证，并使用 AppSecret 按官方 Ed25519 算法校验事件请求的原始字节。
2. 订阅 `C2C_MESSAGE_CREATE`（单聊）、`GROUP_AT_MESSAGE_CREATE`（群 @）。需要接收普通群消息时，再开通平台的“接收所有消息”功能并订阅 `GROUP_MESSAGE_CREATE`。已启用聊天配置的群会回复收到的全量群消息，请按实际需要订阅。
3. 给机器人发送消息，在服务日志的 `Receive official bot` 记录中查看 `target`。在现有聊天配置页面添加并启用配置：单聊类型为 `private`，目标填 `author.user_openid`；群聊类型为 `group`，目标填 `group_openid`，再选择 OpenAI/Anthropic 和模型。聊天服务的原有 endpoint/token 配置仍需填写。

OpenID 是字符串，与 QQ 号、群号不通用；`member_openid` 也不能用作单聊目标。不同 AppID 的 OpenID 不通用。官方 Bot 的聊天历史按 AppID、会话类型和 OpenID 隔离，与 OneBot 分开。

### 发送消息

业务代码通过依赖注入获得 `Notify.Service.OfficialBot.OfficialBotApi`，消息模型位于 `Notify.Domain.Models`：

```csharp
// 单聊被动回复：msgId 来自收到事件的 d.id，而非外层事件 id。
await officialBotApi.SendPrivateMessage(userOpenId, new OfficialBotMessage
{
    Content = "收到你的消息了",
    MsgId = msgId,
    MsgSeq = 1
});

// 群聊被动回复。
await officialBotApi.SendGroupMessage(groupOpenId, new OfficialBotMessage
{
    Content = "大家好",
    MsgId = groupMessageId,
    MsgSeq = 1
});

// 主动发送：不传 MsgId/EventId，需要平台允许且符合频控。
await officialBotApi.SendPrivateMessage(userOpenId, new OfficialBotMessage
{
    Content = "这是一条主动消息"
});
```

也可使用与 OneBot 相同形式的 `SendMessage(openId, "private"或"group", message)`。发送返回 `bool`，HTTP/平台业务错误记录状态码、错误码和 trace ID；网络或取凭证失败会抛出异常。凭证在过期前刷新，同一进程并发取凭证合并；HTTP 401 时刷新并重试一次，超时不会自动重发。

`MsgId` 和 `EventId` 互斥，同一消息多次回复时递增 `MsgSeq`，重试同一次回复则保持序号。主动发送的权限、限频和回复有效期由 QQ 平台判定。发送模型还提供 `Markdown`（`MsgType=2`，不填 `Content`）、`Media.FileInfo`（`MsgType=7`）和 `Keyboard` 字段；`UploadImage(openId, targetType, imageUrl)` 封装了对应单聊/群聊图片 URL 上传接口，返回 `file_info`，上传本身不发送消息。图片 URL 需允许 QQ 平台访问，暂不支持本地文件、Base64 或分片上传。

回调入队后立即返回 `{"op":12,"d":0}`，后台服务独立创建 DI scope 处理聊天，避免模型响应时间阻塞 QQ 回调。重复消息按会话、消息 ID、消息序号及 `message_scene.ext` 的 `msg_idx` 去重；队列满时返回 HTTP 503，允许平台重投。当前队列和去重缓存仅在单进程内存中保存，进程退出会丢失待处理消息；ACK 后处理失败会记录日志，不保证平台再次投递。需要多实例或持久化交付时需替换为共享队列/去重存储。

### 从 OneBot 迁移发送

先启用官方 Bot 并配置 AppID 和环境变量 `OFFICIALBOT_APP_SECRET`。原有 QQ 号/群号不能直接作为官方 OpenID 使用，需选择以下方式之一：

- 在 RSS 配置页面将接收方修改为官方 `user_openid` / `group_openid`。
- 保留 RSS 原有目标号码，在 `OfficialBot` 下配置号码到 OpenID 的映射；同一号码在私聊、群聊中分别配置。例如：

```json
{
  "OfficialBot": {
    "PrivateTargetMappings": {
      "123456789": "对应用户的user_openid"
    },
    "GroupTargetMappings": {
      "987654321": "对应群的group_openid"
    }
  }
}
```

RSS 的 B站动态/直播、YouTube 直播、漫画更新均通过 `OfficialBotSender` 发送。现有消息分段模型在内部保留，相邻文本合并，图片先上传获取 `file_info`，再按原有文本/图片顺序发送多条官方消息；全部发送成功后才更新 `LastMsgSendTime`。任一图片上传失败时整条通知不发送；发送阶段中途失败可能已有部分消息到达，需查看日志排查。数据库中的旧目标不会自动改写。

如果继续用 OneBot 接收聊天消息，必须配置相应号码映射，聊天配置仍按原 QQ 号/群号查找。没有映射时记录日志并跳过处理。回复由官方机器人以主动消息发送，不携带 OneBot 的消息 ID 或引用信息；这类 ID 无法作为官方被动回复凭据。需要官方被动回复时应使用 `/api/officialbot/event` 回调，并按 OpenID 配置聊天。请避免同时为同一对话启用两套自动回复。

管理端测试接口改为 `POST /api/manage/test-official-bot?targetId=官方OpenID&messageType=group`（管理员权限，`messageType` 可为 `private`）。旧 `GET /api/manage/test-one-bot` 路由仍可用，但同样必须提供 `targetId`，并使用官方 API 发送，已移除硬编码群号。

### 验证

```text
dotnet build notify.sln
dotnet test notify.tests/notify.tests.csproj
```

测试使用模拟 HTTP、内存 SQLite 和本地 RSS 样例，不需要真实 QQ 或 AI 凭证。覆盖按官方算法独立生成的 Go 签名向量、回调地址验证、验签失败、三类消息路由及去重、队列满、发送鉴权、并发令牌缓存、401 刷新、平台错误、GPT/Claude 文本回复、四类 RSS 通知、图文顺序、上传失败和旧号码映射。

接口依据：[消息收发概述](https://bot.q.qq.com/wiki/develop/api-v2/server-inter/message/overview.html)、[获取访问凭证](https://bot.q.qq.com/wiki/develop/api-v2/dev-prepare/access-token.html)、[单聊发送](https://bot.q.qq.com/wiki/develop/api-v2/autogen/api/v2_users_user_openid_messages.post.html)、[群聊发送](https://bot.q.qq.com/wiki/develop/api-v2/autogen/api/v2_groups_group_openid_messages.post.html)、[回调验证与事件通知](https://bot.q.qq.com/wiki/develop/api-v2/dev-prepare/interface-framework/event-emit.html)、[签名算法](https://bot.q.qq.com/wiki/develop/api-v2/dev-prepare/interface-framework/sign.html)。
