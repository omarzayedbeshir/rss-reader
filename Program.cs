using RssReader.Models;
using RssReader.Services;

var builder = WebApplication.CreateBuilder(args);

var storagePath = Path.Combine(builder.Environment.ContentRootPath, "Data", "feeds.json");
builder.Services.AddSingleton(new FeedStorageService(storagePath));

builder.Services.AddHttpClient("RssClient", client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", "RssReader/1.0");
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddSingleton<FeedFetchService>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

var api = app.MapGroup("/api/feeds");

api.MapGet("/", (FeedStorageService storage) =>
{
    var feeds = storage.GetAllFeeds();
    var articles = feeds
        .SelectMany(f => f.Articles.Select(a => new ArticleResponse(
            a.Id,
            a.Title,
            a.Url,
            a.Summary,
            a.Published,
            a.FeedId
        )))
        .OrderByDescending(a => a.Published)
        .ToList();

    var feedResponses = feeds.Select(f => new FeedResponse(
        f.Id,
        f.Title,
        f.FeedUrl,
        f.SiteUrl,
        f.Description,
        f.LastRefreshed,
        f.Articles.Select(a => new ArticleResponse(
            a.Id,
            a.Title,
            a.Url,
            a.Summary,
            a.Published,
            a.FeedId
        )).ToList()
    )).ToList();

    return Results.Ok(new { feeds = feedResponses, articles });
});

api.MapPost("/", async (FeedAddRequest request, FeedStorageService storage, FeedFetchService fetcher) =>
{
    if (string.IsNullOrWhiteSpace(request.Url) || !Uri.TryCreate(request.Url, UriKind.Absolute, out var uri)
        || (uri.Scheme != "http" && uri.Scheme != "https"))
    {
        return Results.BadRequest(new { error = "Invalid URL. Must be an http or https URL." });
    }

    try
    {
        var feed = await fetcher.FetchFeedAsync(request.Url);
        feed = storage.AddFeed(feed);
        var response = MapFeedResponse(feed);
        return Results.Ok(response);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = $"Failed to fetch or parse feed: {ex.Message}" });
    }
});

api.MapDelete("/{id}", (string id, FeedStorageService storage) =>
{
    var feed = storage.GetFeed(id);
    if (feed is null)
        return Results.NotFound(new { error = "Feed not found." });

    storage.RemoveFeed(id);
    return Results.Ok(new { deleted = true });
});

api.MapPost("/{id}/refresh", async (string id, FeedStorageService storage, FeedFetchService fetcher) =>
{
    var existingFeed = storage.GetFeed(id);
    if (existingFeed is null)
        return Results.NotFound(new { error = "Feed not found." });

    try
    {
        var updatedFeed = await fetcher.FetchFeedAsync(existingFeed.FeedUrl);
        updatedFeed.Id = existingFeed.Id;
        updatedFeed = storage.UpdateFeed(updatedFeed);
        var response = MapFeedResponse(updatedFeed);
        return Results.Ok(response);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = $"Failed to refresh feed: {ex.Message}" });
    }
});

app.Run();

static FeedResponse MapFeedResponse(Feed feed)
{
    return new FeedResponse(
        feed.Id,
        feed.Title,
        feed.FeedUrl,
        feed.SiteUrl,
        feed.Description,
        feed.LastRefreshed,
        feed.Articles.Select(a => new ArticleResponse(
            a.Id,
            a.Title,
            a.Url,
            a.Summary,
            a.Published,
            a.FeedId
        )).ToList()
    );
}
