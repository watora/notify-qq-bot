using Notify.Domain.Models;
using Notify.Service.OneBot;
using System.Xml;
using System.ServiceModel.Syndication;
using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore;


namespace Notify.Service.RSS;

public abstract class RSSNotifyBase
{
    protected OneBotApi oneBotApi;
    protected IConfiguration configuration;
    protected string hubUrl;
    protected IServiceProvider serviceProvider;
    protected ILogger logger;

    public RSSNotifyBase(IServiceProvider serviceProvider, ILogger logger)
    {
        this.oneBotApi = serviceProvider.GetRequiredService<OneBotApi>();
        this.configuration = serviceProvider.GetRequiredService<IConfiguration>();
        this.hubUrl = configuration["RSS:HubUrl"] ?? "";
        this.serviceProvider = serviceProvider;
        this.logger = logger;
    }

    /// <summary>
    /// 开启事务
    /// </summary>
    /// <typeparam name="T">对应的DbContext</typeparam>
    /// <param name="func"></param>
    /// <returns></returns>
    protected async Task BeginTransaction<T>(Func<T, Task> func) where T : DbContext
    {
        try
        {
            using (var scope = this.serviceProvider.CreateAsyncScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<T>();
                await func(context);
                await context.SaveChangesAsync();
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "transaction process failed");
        }
    }

    /// <summary>
    /// 加载RSS
    /// </summary>
    /// <param name="uri"></param>
    /// <returns></returns>
    protected RSSChannel? LoadRSS(string uri)
    {
        try
        {
            using var reader = XmlReader.Create(uri);
            var feed = SyndicationFeed.Load(reader);
            if (feed == null)
            {
                return null;
            }
            var RSSChannel = new RSSChannel
            {
                Title = feed.Title.Text,
                Link = feed.Links[0].Uri.ToString(),
                Description = feed.Description.Text,
                Items = new List<RSSItem>()
            };
            foreach (var item in feed.Items)
            {
                var rssItem = new RSSItem
                {
                    Title = item.Title.Text,
                    Link = item.Links.FirstOrDefault()?.Uri.ToString() ?? "",
                    Description = item.Summary.Text,
                    Guid = item.Id,
                    Author = item.Authors.FirstOrDefault()?.Email ?? "",
                    Category = item.Categories.FirstOrDefault()?.Name ?? "",
                    PubDate = item.PublishDate.ToString()
                };
                foreach(var link in item.Links) 
                {
                    if (link.RelationshipType == "enclosure" && link.MediaType == "image/jpeg") 
                    {
                        rssItem.RelatedImg = link.Uri.ToString();
                    }
                }
                RSSChannel.Items.Add(rssItem);
            }
            return RSSChannel;
        }
        catch (Exception e)
        {
            logger.LogError(e, "load rss failed");
        }
        return null;
    }

    protected HtmlNode ParseHtml(string content)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(content);
        return doc.DocumentNode;
    }
}