using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using notify.Controllers;
using Notify.Domain.Config;
using Notify.Domain.Config.Options;
using Notify.Domain.Models;
using Notify.Repository;
using Notify.Repository.Entity;
using Notify.Service.ChatBot;
using Notify.Service.Manager;
using Notify.Service.OfficialBot;
using Notify.Service.OneBot;
using Notify.Service.RSS;
using Xunit;

namespace Notify.Tests;

public partial class OfficialBotTests
{
    [Theory]
    [InlineData("private", "users")]
    [InlineData("group", "groups")]
    public async Task SegmentedMessagePreservesTextAndImagesThroughOfficialApi(string type, string resource)
    {
        var handler = new FakeHttpHandler();
        using var api = Api(handler);
        var sender = new OfficialBotSender(api, Options(), NullLogger<OfficialBotSender>.Instance);
        var message = new OneBotMessage { Items = new()
        {
            Segment("reply", new() { Id = "onebot-message-id" }),
            Segment("text", new() { Text = "标题\n" }),
            Segment("text", new() { Text = "正文" }),
            Segment("image", new() { Url = "//images.example/first.png" }),
            Segment("text", new() { Text = "结尾" }),
            Segment("image", new() { Url = "https://images.example/second.png" })
        } };
        Assert.True(await sender.SendMessage("target", type, message));
        Assert.Equal(2, handler.Uploads.Count);
        Assert.All(handler.Uploads, upload =>
        {
            Assert.Equal($"/v2/{resource}/target/files", upload.Uri.AbsolutePath);
            Assert.Equal("QQBot token-1", upload.Authorization);
            Assert.Equal(1, upload.Body.GetProperty("file_type").GetInt32());
            Assert.False(upload.Body.GetProperty("srv_send_msg").GetBoolean());
        });
        Assert.Equal("https://images.example/first.png", handler.Uploads.First().Body.GetProperty("url").GetString());
        var sent = handler.Messages.ToArray();
        Assert.Equal(new[] { 0, 7, 0, 7 }, sent.Select(x => x.Body.GetProperty("msg_type").GetInt32()));
        Assert.Equal("标题\n正文", sent[0].Body.GetProperty("content").GetString());
        Assert.Equal("结尾", sent[2].Body.GetProperty("content").GetString());
        Assert.Equal("uploaded-file", sent[1].Body.GetProperty("media").GetProperty("file_info").GetString());
        Assert.All(sent, x =>
        {
            Assert.Equal($"/v2/{resource}/target/messages", x.Uri.AbsolutePath);
            Assert.False(x.Body.TryGetProperty("msg_id", out _));
        });
    }

    [Theory]
    [InlineData("{}", 200)]
    [InlineData("{\"err_code\":850026}", 200)]
    [InlineData("{\"file_info\":\"invalid\"}", 500)]
    public async Task ImageUploadFailureDoesNotSendTextOrReportSuccess(string body, int status)
    {
        var handler = new FakeHttpHandler { UploadBody = body, UploadStatus = (HttpStatusCode)status };
        using var api = Api(handler);
        var sender = new OfficialBotSender(api, Options(), NullLogger<OfficialBotSender>.Instance);
        Assert.False(await sender.SendMessage("target", "group", new OneBotMessage { Items = new()
        {
            Segment("text", new() { Text = "不要丢图" }),
            Segment("image", new() { Url = "https://images.example/image.png" })
        } }));
        Assert.Empty(handler.Messages);
    }

    [Fact]
    public async Task ImageUploadRefreshesRejectedToken()
    {
        var handler = new FakeHttpHandler { RejectFirstUpload = true };
        using var api = Api(handler);
        Assert.Equal("uploaded-file", await api.UploadImage("g", "group", "https://images.example/image.png"));
        Assert.Equal(2, handler.TokenCalls);
        Assert.Equal(new[] { "QQBot token-1", "QQBot token-2" }, handler.Uploads.Select(x => x.Authorization));
        Assert.Empty(handler.Messages);
    }

    [Fact]
    public async Task DisabledSenderAndInvalidImageDoNotSendRequests()
    {
        var handler = new FakeHttpHandler();
        using var api = Api(handler);
        var message = new OneBotMessage { Items = new() { Segment("image", new() { File = "file:///local.png" }) } };
        var sender = new OfficialBotSender(api, Options(), NullLogger<OfficialBotSender>.Instance);
        await Assert.ThrowsAsync<ArgumentException>(() => sender.SendMessage("g", "group", message));
        var disabled = new OfficialBotSender(api, Options(false), NullLogger<OfficialBotSender>.Instance);
        Assert.False(await disabled.SendMessage("g", "group", message));
        Assert.Equal(0, handler.TokenCalls);
    }

    [Theory]
    [InlineData("private", "users", "user-openid")]
    [InlineData("group", "groups", "group-openid")]
    public async Task ExistingRssTargetUsesExplicitMapping(string type, string resource, string expectedId)
    {
        var options = Options();
        options.Value.PrivateTargetMappings["12345"] = "user-openid";
        options.Value.GroupTargetMappings["12345"] = "group-openid";
        var handler = new FakeHttpHandler();
        using var api = Api(handler);
        var sender = new OfficialBotSender(api, options, NullLogger<OfficialBotSender>.Instance);
        Assert.Null(sender.GetMappedTarget("unmapped", type));
        Assert.True(await sender.SendMessage("12345", type, new OneBotMessage { Items = new() { Segment("text", new() { Text = "通知" }) } }));
        Assert.Equal($"/v2/{resource}/{expectedId}/messages", Assert.Single(handler.Messages).Uri.AbsolutePath);
    }

    [Theory]
    [InlineData(Consts.BilibiliDynamicChannel, "bilibili/user/dynamic/subscription", 1)]
    [InlineData(Consts.BilibiliLiveChannel, "bilibili/live/room/subscription", 0)]
    [InlineData(Consts.YoutubeLiveChannel, "youtube/live/subscription/noembed", 1)]
    [InlineData(Consts.CopymangaUpdateChannel, "copymanga/comic/subscription/0", 0)]
    public async Task RssNotificationsUseOfficialApiWithoutOneBotClient(string channel, string feedPath, int imageCount)
    {
        var directory = Path.Combine(Path.GetTempPath(), "notify-rss-test-" + Guid.NewGuid());
        var feed = Path.Combine(directory, feedPath);
        Directory.CreateDirectory(Path.GetDirectoryName(feed)!);
        try
        {
            await File.WriteAllTextAsync(feed, $"""
                <rss version="2.0"><channel>
                <title>Creator - sample channel 1234567890</title><link>https://example.com</link><description>Test</description>
                <item><title>New update 2026-11-12 12:00:00</title><link>https://example.com/update</link>
                <description><![CDATA[正文<img src="https://images.example/cover.png"/>]]></description>
                <author>Creator</author><guid>item-1</guid><pubDate>{DateTimeOffset.UtcNow.AddMinutes(-1):r}</pubDate>
                <enclosure url="https://images.example/cover.png" type="image/jpeg" length="10"/>
                </item></channel></rss>
                """);
            await using var db = new NotifyContext(":memory:");
            await db.Database.OpenConnectionAsync();
            await db.Database.EnsureCreatedAsync();
            var config = new RSSConfig { SubscribeChannel = channel, SubscribeId = "subscription", MsgTargetId = "official-group",
                MsgTargetType = "group", IsActive = true, CheckInterval = 600, Creator = "test", Comment = "test" };
            db.RSSConfig.Add(config);
            await db.SaveChangesAsync();
            var handler = new FakeHttpHandler();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["RSS:HubUrl"] = directory }).Build());
            services.AddSingleton<IHttpClientFactory>(handler);
            services.AddSingleton(Options());
            services.AddSingleton<OfficialBotApi>();
            services.AddSingleton<OfficialBotSender>();
            services.AddScoped(_ => db);
            services.AddSingleton<RSSNotifyBilibili>();
            services.AddSingleton<RSSNotifyYoutube>();
            services.AddSingleton<RSSNotifyCopymanga>();
            await using var sp = services.BuildServiceProvider();
            switch (channel)
            {
                case Consts.BilibiliDynamicChannel: await sp.GetRequiredService<RSSNotifyBilibili>().CheckNewDynamicAndSendMessage(true); break;
                case Consts.BilibiliLiveChannel: await sp.GetRequiredService<RSSNotifyBilibili>().CheckLiveStatusAndSendMessage(true); break;
                case Consts.YoutubeLiveChannel: await sp.GetRequiredService<RSSNotifyYoutube>().CheckLiveStatusAndSendMessage(true); break;
                case Consts.CopymangaUpdateChannel: await sp.GetRequiredService<RSSNotifyCopymanga>().CheckMangaUpdateAndSendMessage(true); break;
            }
            Assert.Equal(imageCount, handler.Uploads.Count);
            Assert.NotEmpty(handler.Messages);
            Assert.All(handler.Messages, x => Assert.Equal("/v2/groups/official-group/messages", x.Uri.AbsolutePath));
            Assert.True(config.LastMsgSendTime > 0);
        }
        finally
        {
            Assert.Equal(Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())),
                Path.GetDirectoryName(Path.GetFullPath(directory)));
            Directory.Delete(directory, true);
        }
    }

    [Theory]
    [InlineData("private", true, "OpenAI")]
    [InlineData("group", true, "OpenAI")]
    [InlineData("private", false, "OpenAI")]
    [InlineData("private", true, "OpenAIResponses")]
    [InlineData("group", true, "OpenAIResponses")]
    public async Task LegacyChatRequiresMappingAndSendsActiveOfficialMessage(string type, bool mapped, string provider)
    {
        await using var db = new NotifyContext(":memory:");
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();
        db.ChatConfig.Add(new ChatConfig { TargetId = "12345", TargetType = type, IsActive = true,
            Provider = provider, Model = "test-model", Comment = "test", Creator = "test" });
        await db.SaveChangesAsync();
        var options = Options();
        if (mapped)
        {
            options.Value.PrivateTargetMappings["12345"] = "official-user";
            options.Value.GroupTargetMappings["12345"] = "official-group";
        }
        var handler = new FakeHttpHandler();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMemoryCache();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton<IHttpClientFactory>(handler);
        services.AddTransient(_ => handler.CreateClient(""));
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
        services.AddSingleton(options);
        services.Configure<ChatBotOption>(o =>
        {
            if (provider == "OpenAIResponses")
                o.OpenAIResponsesEndpoint = "https://ai.example";
            else
            {
                o.OpenAIEndpoint = "https://ai.example";
                o.AnthropicEndpoint = "https://ai.example";
            }
        });
        services.AddScoped(_ => db);
        services.AddScoped<ChatBotManager>();
        services.AddScoped<ChatBotOpenAI>();
        services.AddScoped<ChatBotOpenAIResponses>();
        services.AddScoped<ChatBotAnthropic>();
        services.AddSingleton<OfficialBotApi>();
        services.AddSingleton<OfficialBotSender>();
        services.AddSingleton<OneBotApi>();
        services.AddScoped<OneBotEvent>();
        await using var sp = services.BuildServiceProvider();
        await using var scope = sp.CreateAsyncScope();
        var incoming = new OneBotEventMessage
        {
            PostType = "message", MessageType = type, MessageId = 67890, UserId = 12345, GroupId = 12345, SelfId = 999,
            Sender = new() { UserId = 12345, NickName = "test" },
            Message = new() { Segment("at", new() { QQ = "999" }), Segment("text", new() { Text = "hello" }) }
        };
        await scope.ServiceProvider.GetRequiredService<OneBotEvent>().HandleEvent(JsonSerializer.Serialize(incoming));
        if (!mapped)
            Assert.Empty(handler.Messages);
        else
        {
            var sent = Assert.Single(handler.Messages);
            Assert.Equal(type == "private" ? "/v2/users/official-user/messages" : "/v2/groups/official-group/messages", sent.Uri.AbsolutePath);
            Assert.False(sent.Body.TryGetProperty("msg_id", out _));
            Assert.Contains("AI reply", sent.Body.GetProperty("content").GetString());
        }
    }

    private static OneBotMessageItem Segment(string type, OneBotMessageItemData data) => new() { Type = type, Data = data };

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "Notify.Tests";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
