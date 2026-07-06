namespace RssReader.Models;

public record FeedAddRequest(string Url);

public record FeedResponse(
    string Id,
    string Title,
    string FeedUrl,
    string SiteUrl,
    string Description,
    DateTime LastRefreshed,
    List<ArticleResponse> Articles
);

public record ArticleResponse(
    string Id,
    string Title,
    string Url,
    string Summary,
    DateTime Published,
    string FeedId
);
