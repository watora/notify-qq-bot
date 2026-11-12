using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using notify.Controllers;
using Notify.Domain.Config.Options;
using Notify.Domain.Models;
using Notify.Service.OfficialBot;
using Xunit;

namespace Notify.Tests;

public partial class OfficialBotTests
{
    [Theory]
    [InlineData("private", "users")]
    [InlineData("group", "groups")]
    public async Task ExternalSendRoutesToOfficialApi(string type, string resource)
    {
        using var queue = new OfficialBotEventQueue();
        var handler = new FakeHttpHandler();
        using var api = Api(handler);
        var result = Assert.IsType<OkObjectResult>(await SendController(queue).SendMessage(new()
        {
            TargetId = "target-openid", MessageType = type,
            Message = new() { Content = "交易通知", MsgId = "reply-id", MsgSeq = 2 }
        }, api, default));
        Assert.True(JsonSerializer.SerializeToElement(result.Value).GetProperty("success").GetBoolean());
        var sent = Assert.Single(handler.Messages);
        Assert.Equal($"/v2/{resource}/target-openid/messages", sent.Uri.AbsolutePath);
        Assert.Equal("交易通知", sent.Body.GetProperty("content").GetString());
        Assert.Equal("reply-id", sent.Body.GetProperty("msg_id").GetString());
        Assert.Equal(2, sent.Body.GetProperty("msg_seq").GetInt32());
        Assert.Equal("QQBot token-1", sent.Authorization);
    }

    [Theory]
    [InlineData("private", "users", "123456", "private-openid")]
    [InlineData("group", "groups", "123456", "group-openid")]
    [InlineData("private", "users", "direct-openid", "direct-openid")]
    [InlineData("group", "groups", "direct-openid", "direct-openid")]
    [InlineData("private", "users", "group-only", "group-only")]
    [InlineData("group", "groups", "private-only", "private-only")]
    public async Task ExternalSendResolvesMappingBeforeUsingOriginalTarget(string type, string resource, string target, string expected)
    {
        using var queue = new OfficialBotEventQueue();
        var handler = new FakeHttpHandler();
        using var api = Api(handler);
        var controller = SendController(queue, configure: options =>
        {
            options.PrivateTargetMappings["123456"] = "private-openid";
            options.GroupTargetMappings["123456"] = "group-openid";
            options.PrivateTargetMappings["private-only"] = "private-mapped";
            options.GroupTargetMappings["group-only"] = "group-mapped";
            // Resolve only once, even when the mapped OpenID also appears as a key.
            options.PrivateTargetMappings["private-openid"] = "unexpected";
            options.GroupTargetMappings["group-openid"] = "unexpected";
        });
        var request = new OfficialBotSendRequest
        {
            TargetId = target, MessageType = type, Message = new() { Content = "通知" }
        };
        Assert.IsType<OkObjectResult>(await controller.SendMessage(request, api, default));
        Assert.Equal($"/v2/{resource}/{expected}/messages", Assert.Single(handler.Messages).Uri.AbsolutePath);
        Assert.Equal(target, request.TargetId);
    }

    [Theory]
    [InlineData("private", "")]
    [InlineData("group", " ")]
    public async Task ExternalSendRejectsEmptyMappingWithoutSending(string type, string mapped)
    {
        using var queue = new OfficialBotEventQueue();
        var handler = new FakeHttpHandler();
        using var api = Api(handler);
        var controller = SendController(queue, configure: options =>
        {
            options.PrivateTargetMappings["123456"] = mapped;
            options.GroupTargetMappings["123456"] = mapped;
        });
        Assert.IsType<BadRequestObjectResult>(await controller.SendMessage(new()
        {
            TargetId = "123456", MessageType = type, Message = new() { Content = "通知" }
        }, api, default));
        Assert.Empty(handler.Messages);
        Assert.Equal(0, handler.TokenCalls);
    }

    [Theory]
    [InlineData("", "send-secret", true, 401)]
    [InlineData("Bearer wrong", "send-secret", true, 401)]
    [InlineData("Basic send-secret", "send-secret", true, 401)]
    [InlineData("Bearer send-secret", "", true, 503)]
    [InlineData("Bearer send-secret", "send-secret", false, 503)]
    public async Task ExternalSendRejectsUnauthorizedOrDisabledRequests(string auth, string secret, bool enabled, int status)
    {
        using var queue = new OfficialBotEventQueue();
        var handler = new FakeHttpHandler();
        using var api = Api(handler);
        var result = Assert.IsAssignableFrom<ObjectResult>(await SendController(queue, auth, secret, enabled)
            .SendMessage(TextRequest(), api, default));
        Assert.Equal(status, result.StatusCode);
        Assert.Empty(handler.Messages);
        Assert.Equal(0, handler.TokenCalls);
    }

    [Theory]
    [InlineData("", "group", "text", 1, 0)]
    [InlineData("id", "channel", "text", 1, 0)]
    [InlineData("id", "group", "", 1, 0)]
    [InlineData("id", "group", "text", 0, 0)]
    [InlineData("id", "group", "text", 1, 99)]
    public async Task ExternalSendRejectsInvalidMessages(string id, string type, string content, int sequence, int msgType)
    {
        using var queue = new OfficialBotEventQueue();
        var handler = new FakeHttpHandler();
        using var api = Api(handler);
        Assert.IsType<BadRequestObjectResult>(await SendController(queue).SendMessage(new()
        {
            TargetId = id, MessageType = type,
            Message = new() { Content = content, MsgSeq = sequence, MsgType = msgType }
        }, api, default));
        Assert.Empty(handler.Messages);
        Assert.Equal(0, handler.TokenCalls);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, "{}")]
    [InlineData(HttpStatusCode.BadRequest, "{\"code\":123}")]
    public async Task ExternalSendReportsUpstreamRejection(HttpStatusCode status, string body)
    {
        using var queue = new OfficialBotEventQueue();
        var handler = new FakeHttpHandler { SendStatus = status, SendBody = body };
        using var api = Api(handler);
        var result = Assert.IsType<ObjectResult>(await SendController(queue).SendMessage(TextRequest(), api, default));
        Assert.Equal(502, result.StatusCode);
        Assert.False(JsonSerializer.SerializeToElement(result.Value).GetProperty("success").GetBoolean());
    }

    [Theory]
    [InlineData("network", 502)]
    [InlineData("token", 502)]
    [InlineData("json", 502)]
    [InlineData("timeout", 504)]
    public async Task ExternalSendReportsTransportFailures(string failure, int status)
    {
        using var queue = new OfficialBotEventQueue();
        using var handler = new SendFailureHandler(failure);
        using var api = new OfficialBotApi(handler, Options(), NullLogger<OfficialBotApi>.Instance);
        var result = Assert.IsType<ObjectResult>(await SendController(queue).SendMessage(TextRequest(), api, default));
        Assert.Equal(status, result.StatusCode);
    }

    private static OfficialBotSendRequest TextRequest() => new()
    {
        TargetId = "target-openid", MessageType = "group", Message = new() { Content = "test" }
    };

    private static OfficialBotController SendController(OfficialBotEventQueue queue,
        string auth = "Bearer send-secret", string secret = "send-secret", bool enabled = true,
        Action<OfficialBotOption>? configure = null)
    {
        var options = Options(enabled);
        options.Value.SendSecret = secret;
        configure?.Invoke(options.Value);
        var controller = new OfficialBotController(options, new OfficialBotSignature(options), queue,
            NullLogger<OfficialBotController>.Instance)
        {
            ControllerContext = new() { HttpContext = new DefaultHttpContext() }
        };
        controller.Request.Headers.Authorization = auth;
        return controller;
    }

    private sealed class SendFailureHandler(string failure) : HttpMessageHandler, IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(this, false);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw failure switch
            {
                "network" => new HttpRequestException("network failure"),
                "token" => new InvalidOperationException("invalid token response"),
                "json" => new JsonException("invalid token JSON"),
                _ => new TaskCanceledException("timeout")
            };
    }
}
