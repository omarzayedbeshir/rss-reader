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
        _sanitizer.AllowedTags.Add("img");
        _sanitizer.AllowedTags.Add("figure");
        _sanitizer.AllowedTags.Add("figcaption");
        _sanitizer.AllowedAttributes.Add("src");
        _sanitizer.AllowedAttributes.Add("alt");
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
            Title = SanitizeText(syndicationFeed.Title?.Text ?? url),
            FeedUrl = url,
            SiteUrl = SanitizeUrl(syndicationFeed.Links.FirstOrDefault()?.Uri?.ToString() ?? url),
            Description = Sanitize(syndicationFeed.Description?.Text ?? string.Empty),
            LastRefreshed = DateTime.UtcNow
        };

        feed.Articles = syndicationFeed.Items.Select(item =>
        {
            var summary = item.Summary?.Text;
            if (string.IsNullOrWhiteSpace(summary) && item.Content is TextSyndicationContent content)
                summary = content.Text;

            return new Article
            {
                Title = SanitizeText(item.Title?.Text ?? "Untitled"),
                Url = SanitizeUrl(item.Links.FirstOrDefault()?.Uri?.ToString() ?? string.Empty),
                Summary = Sanitize(summary ?? string.Empty),
                Published = GetPublishedDate(item),
                FeedId = feed.Id
            };
        }).ToList();

        return feed;
    }

    private static DateTime GetPublishedDate(SyndicationItem item)
    {
        var date = item.PublishDate;
        if (date == DateTimeOffset.MinValue)
            date = item.LastUpdatedTime;
        return date.UtcDateTime == default ? DateTime.UtcNow : date.UtcDateTime;
    }

    private string Sanitize(string html)
    {
        var decoded = WebUtility.HtmlDecode(html);
        return _sanitizer.Sanitize(decoded);
    }

    private string SanitizeText(string html)
    {
        var sanitized = Sanitize(html);
        var decoded = WebUtility.HtmlDecode(sanitized);
        return Regex.Replace(decoded, "<[^>]*>", "").Trim();
    }

    private string SanitizeUrl(string url)
    {
        var sanitized = Sanitize(url);
        if (Uri.TryCreate(sanitized, UriKind.Absolute, out var uri))
            return (uri.Scheme == "http" || uri.Scheme == "https") ? uri.ToString() : string.Empty;
        return sanitized;
    }

    public static string StripHtml(string html)
    {
        if (string.IsNullOrEmpty(html)) return html;
        var decoded = WebUtility.HtmlDecode(html);
        decoded = Regex.Replace(decoded, @"</(p|div|h[1-6]|li|tr|section|article|blockquote)>", "\n\n");
        decoded = Regex.Replace(decoded, @"<(br|hr)\s*/?>", "\n");
        decoded = Regex.Replace(decoded, "<[^>]*>", "");
        decoded = Regex.Replace(decoded, @"\n{3,}", "\n\n");
        return decoded.Trim();
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
