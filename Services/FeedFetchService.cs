using System.Net;
using System.ServiceModel.Syndication;
using System.Text.RegularExpressions;
using System.Xml;
using Ganss.Xss;
using RssReader.Models;

namespace RssReader.Services;

public class FeedFetchService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly HtmlSanitizer _sanitizer;

    public FeedFetchService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
        _sanitizer = new HtmlSanitizer();
    }

    public async Task<Feed> FetchFeedAsync(string url)
    {
        var client = _httpClientFactory.CreateClient("RssClient");

        using var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = XmlReader.Create(stream);
        var syndicationFeed = SyndicationFeed.Load(reader);

        var feed = new Feed
        {
            Title = Sanitize(syndicationFeed.Title?.Text ?? url),
            FeedUrl = url,
            SiteUrl = Sanitize(syndicationFeed.Links.FirstOrDefault()?.Uri?.ToString() ?? url),
            Description = Sanitize(syndicationFeed.Description?.Text ?? string.Empty),
            LastRefreshed = DateTime.UtcNow
        };

        feed.Articles = syndicationFeed.Items.Select(item => new Article
        {
            Title = Sanitize(item.Title?.Text ?? "Untitled"),
            Url = Sanitize(item.Links.FirstOrDefault()?.Uri?.ToString() ?? string.Empty),
            Summary = StripHtml((item.Summary?.Text ?? string.Empty).Truncate(500)),
            Published = item.PublishDate.UtcDateTime == default
                ? DateTime.UtcNow
                : item.PublishDate.UtcDateTime,
            FeedId = feed.Id
        }).ToList();

        return feed;
    }

    private string Sanitize(string html)
    {
        var sanitized = _sanitizer.Sanitize(html);
        return WebUtility.HtmlDecode(sanitized);
    }

    private static string StripHtml(string html)
    {
        if (string.IsNullOrEmpty(html)) return html;
        var decoded = WebUtility.HtmlDecode(html);
        var noTags = Regex.Replace(decoded, "<[^>]*>", "");
        return Regex.Replace(noTags, @"\s+", " ").Trim();
    }
}

public static class StringExtensions
{
    public static string Truncate(this string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= maxLength ? value : value[..maxLength] + "...";
    }
}
