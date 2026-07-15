using System.Net;
using System.ServiceModel.Syndication;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using Ganss.Xss;
using RssReader.Models;

namespace RssReader.Services;

public class FeedFetchService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly HtmlSanitizer _sanitizer;
    private readonly ILogger<FeedFetchService> _logger;

    public FeedFetchService(IHttpClientFactory httpClientFactory, ILogger<FeedFetchService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
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

        var raw = await response.Content.ReadAsStringAsync();
        raw = HtmlEntityFixer.Fix(raw);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(raw));
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

        var items = syndicationFeed.Items.Select(item =>
        {
            var summary = item.Summary?.Text;
            if (string.IsNullOrWhiteSpace(summary) && item.Content is TextSyndicationContent content)
                summary = content.Text;

            string articleUrl = "";
            string enclosureUrl = "";
            string enclosureType = "";

            foreach (var link in item.Links)
            {
                if (link.RelationshipType == "enclosure" &&
                    link.MediaType != null && link.MediaType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase))
                {
                    enclosureUrl = link.Uri?.ToString() ?? "";
                    enclosureType = link.MediaType;
                }
                else if (string.IsNullOrEmpty(articleUrl) && link.Uri is not null)
                {
                    articleUrl = link.Uri.ToString();
                }
            }

            return new Article
            {
                Title = SanitizeText(item.Title?.Text ?? "Untitled"),
                Url = SanitizeUrl(articleUrl),
                Summary = Sanitize(summary ?? string.Empty),
                Published = GetPublishedDate(item),
                FeedId = feed.Id,
                EnclosureUrl = enclosureUrl,
                EnclosureType = enclosureType
            };
        }).ToList();

        var totalCount = items.Count;
        feed.Articles = items.Where(a => a.Published >= DateTime.UtcNow.AddDays(-30)).ToList();
        _logger.LogInformation("Feed {Url}: {Total} parsed, {Kept} kept after 30-day filter", 
            url, totalCount, feed.Articles.Count);

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

internal static partial class HtmlEntityFixer
{
    private static readonly Dictionary<string, string> Replacements = new()
    {
        ["zwnj"] = "\u200C",
        ["zwj"] = "\u200D",
        ["lrm"] = "\u200E",
        ["rlm"] = "\u200F",
        ["nbsp"] = "\u00A0",
        ["shy"] = "\u00AD",
        ["ensp"] = "\u2002",
        ["emsp"] = "\u2003",
        ["thinsp"] = "\u2009",
        ["ndash"] = "\u2013",
        ["mdash"] = "\u2014",
        ["lsquo"] = "\u2018",
        ["rsquo"] = "\u2019",
        ["ldquo"] = "\u201C",
        ["rdquo"] = "\u201D",
        ["laquo"] = "\u00AB",
        ["raquo"] = "\u00BB",
        ["lsaquo"] = "\u2039",
        ["rsaquo"] = "\u203A",
        ["hellip"] = "\u2026",
        ["sbquo"] = "\u201A",
        ["bdquo"] = "\u201E",
        ["bull"] = "\u2022",
        ["middot"] = "\u00B7",
        ["deg"] = "\u00B0",
        ["dagger"] = "\u2020",
        ["Dagger"] = "\u2021",
        ["permil"] = "\u2030",
        ["euro"] = "\u20AC",
        ["trade"] = "\u2122",
        ["copy"] = "\u00A9",
        ["reg"] = "\u00AE",
        ["sect"] = "\u00A7",
        ["para"] = "\u00B6",
        ["micro"] = "\u00B5",
        ["plusmn"] = "\u00B1",
        ["sup1"] = "\u00B9",
        ["sup2"] = "\u00B2",
        ["sup3"] = "\u00B3",
        ["frac14"] = "\u00BC",
        ["frac12"] = "\u00BD",
        ["frac34"] = "\u00BE",
        ["times"] = "\u00D7",
        ["divide"] = "\u00F7",
    };

    [GeneratedRegex(@"&([a-zA-Z]+);")]
    private static partial Regex EntityRegex();

    public static string Fix(string html)
    {
        return EntityRegex().Replace(html, match =>
        {
            var name = match.Groups[1].Value.ToLowerInvariant();
            if (name is "amp" or "lt" or "gt" or "quot" or "apos")
                return match.Value;
            return Replacements.TryGetValue(name, out var c) ? c : match.Value;
        });
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
