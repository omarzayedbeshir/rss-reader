using System.Net;
using System.Text;
using RssReader.Models;
using RssReader.Services;

DotNetEnv.Env.Load();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<DatabaseService>();
builder.Services.AddSingleton<EmailService>();
builder.Services.AddSingleton<AuthService>();
builder.Services.AddSingleton<FeedStorageService>();

builder.Services.AddHttpClient("RssClient", client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", "RssReader/1.0");
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddSingleton<AiService>();
builder.Services.AddSingleton<FeedFetchService>();

var app = builder.Build();

var db = app.Services.GetRequiredService<DatabaseService>();
try
{
    await db.InitializeAsync();
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogWarning(ex, "Database initialization failed.");
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.Use(async (context, next) =>
{
    try { await next(); }
    catch (Exception ex)
    {
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { error = ex.Message });
    }
});

var authApi = app.MapGroup("/api/auth");

authApi.MapPost("/signup", async (SignUpRequest request, HttpContext context, AuthService auth) =>
{
    if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        return BadRequest("Email and password are required.");

    var baseUrl = $"{context.Request.Scheme}://{context.Request.Host}";

    try
    {
        await auth.SignUpAsync(request.Email.Trim(), request.Password, baseUrl);
        return Results.Ok(new { message = "Verification email sent. Check your inbox." });
    }
    catch (InvalidOperationException ex)
    {
        return Conflict(ex.Message);
    }
    catch (ArgumentException ex)
    {
        return BadRequest(ex.Message);
    }
});

authApi.MapPost("/signin", async (SignInRequest request, AuthService auth) =>
{
    if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        return BadRequest("Email and password are required.");

    try
    {
        var result = await auth.SignInAsync(request.Email.Trim(), request.Password);
        return Results.Ok(result);
    }
    catch (UnauthorizedAccessException ex)
    {
        return Unauthorized(ex.Message);
    }
    catch (ArgumentException ex)
    {
        return BadRequest(ex.Message);
    }
});

authApi.MapGet("/verify-email", async (string token, HttpContext context, AuthService auth) =>
{
    if (string.IsNullOrWhiteSpace(token))
        return BadRequest("Verification token is required.");

    try
    {
        await auth.VerifyEmailAsync(token);
        var baseUrl = $"{context.Request.Scheme}://{context.Request.Host}";
        context.Response.Redirect($"{baseUrl}/?verified=1");
        return Results.Empty;
    }
    catch (InvalidOperationException)
    {
        var baseUrl = $"{context.Request.Scheme}://{context.Request.Host}";
        context.Response.Redirect($"{baseUrl}/?verified=0");
        return Results.Empty;
    }
});

authApi.MapPost("/resend-verification", async (ResendVerificationRequest request, HttpContext context, AuthService auth) =>
{
    if (string.IsNullOrWhiteSpace(request.Email))
        return BadRequest("Email is required.");

    var baseUrl = $"{context.Request.Scheme}://{context.Request.Host}";

    try
    {
        await auth.ResendVerificationAsync(request.Email.Trim(), baseUrl);
        return Results.Ok(new { message = "Verification email resent." });
    }
    catch (InvalidOperationException ex)
    {
        return BadRequest(ex.Message);
    }
    catch (ArgumentException ex)
    {
        return BadRequest(ex.Message);
    }
});

authApi.MapPost("/signout", async (HttpContext context, AuthService auth, FeedStorageService storage) =>
{
    var userId = await GetUserId(context, auth, storage, requireAuth: true);
    if (userId is null) return Unauthorized("Not authenticated.");

    var token = ExtractToken(context);
    if (token is not null)
        await auth.SignOutAsync(token);

    return Results.Ok(new { ok = true });
});

authApi.MapGet("/me", async (HttpContext context, AuthService auth, FeedStorageService storage) =>
{
    var userId = await GetUserId(context, auth, storage, requireAuth: true);
    if (userId is null) return Unauthorized("Not authenticated.");

    var user = await auth.GetUserAsync(userId);
    if (user is null) return Unauthorized("User not found.");

    if (string.IsNullOrEmpty(user.Handle))
        user.Handle = await storage.EnsureUserHandleAsync(userId, user.Email);

    return Results.Ok(new { user.Id, user.Email, user.EmailVerified, user.Handle });
});

var feedApi = app.MapGroup("/api/feeds");

feedApi.MapGet("/", async (HttpContext context, AuthService auth, FeedStorageService storage,
    int? page, int? pageSize, string? feedId, bool? bookmarked) =>
{
    var userId = await GetUserId(context, auth, storage, requireAuth: false);
    if (userId is null) return Unauthorized("Not authenticated.");

    var p = Math.Max(1, page ?? 1);
    var ps = Math.Clamp(pageSize ?? 20, 1, 100);

    var feeds = await storage.GetAllFeedsAsync(userId, bookmarked == true);
    var digest = bookmarked == true ? null : await storage.GetDailyDigestAsync(userId);
    var allArticles = feeds
        .SelectMany(f => f.Articles.Select(a => new ArticleResponse(
            a.Id, a.Title, a.Url, a.Summary, a.Published, a.FeedId,
            a.EnclosureUrl, a.EnclosureType, a.IsBookmarked)))
        .OrderByDescending(a => a.Published)
        .ToList();

    if (!string.IsNullOrWhiteSpace(feedId))
        allArticles = allArticles.Where(a => a.FeedId == feedId).ToList();

    var totalCount = allArticles.Count;
    var totalPages = (int)Math.Ceiling(totalCount / (double)ps);
    var pagedArticles = allArticles.Skip((p - 1) * ps).Take(ps).ToList();

    var feedResponses = feeds.Select(f => new FeedResponse(
        f.Id, f.Title, f.FeedUrl, f.SiteUrl, f.Description, f.LastRefreshed,
        f.Articles.Select(a => new ArticleResponse(
            a.Id, a.Title, a.Url, a.Summary, a.Published, a.FeedId,
            a.EnclosureUrl, a.EnclosureType, a.IsBookmarked)).ToList()
    )).ToList();

    return Results.Ok(new
    {
        feeds = feedResponses,
        articles = pagedArticles,
        digest,
        page = p,
        pageSize = ps,
        totalCount,
        totalPages,
        hasMore = p < totalPages
    });
});

feedApi.MapPost("/", async (FeedAddRequest request, HttpContext context,
    AuthService auth, FeedStorageService storage, FeedFetchService fetcher) =>
{
    var userId = await GetUserId(context, auth, storage, requireAuth: false);
    if (userId is null) return Unauthorized("Not authenticated.");

    if (string.IsNullOrWhiteSpace(request.Url) || !Uri.TryCreate(request.Url, UriKind.Absolute, out var uri)
        || (uri.Scheme != "http" && uri.Scheme != "https"))
    {
        return BadRequest("Invalid URL. Must be an http or https URL.");
    }

    try
    {
        var feed = await fetcher.FetchFeedAsync(request.Url);
        feed = await storage.AddFeedAsync(feed, userId);
        return Results.Ok(MapFeedResponse(feed));
    }
    catch (Exception ex)
    {
        return BadRequest($"Failed to fetch or parse feed: {ex.Message}");
    }
});

feedApi.MapDelete("/{id}", async (string id, HttpContext context,
    AuthService auth, FeedStorageService storage) =>
{
    var userId = await GetUserId(context, auth, storage, requireAuth: false);
    if (userId is null) return Unauthorized("Not authenticated.");

    var feed = await storage.GetFeedAsync(id, userId);
    if (feed is null)
        return Results.NotFound(new { error = "Feed not found." });

    await storage.RemoveFeedAsync(id, userId);
    return Results.Ok(new { deleted = true });
});

feedApi.MapPost("/{id}/refresh", async (string id, HttpContext context,
    AuthService auth, FeedStorageService storage, FeedFetchService fetcher) =>
{
    var userId = await GetUserId(context, auth, storage, requireAuth: false);
    if (userId is null) return Unauthorized("Not authenticated.");

    var existingFeed = await storage.GetFeedAsync(id, userId);
    if (existingFeed is null)
        return Results.NotFound(new { error = "Feed not found." });

    try
    {
        var updatedFeed = await fetcher.FetchFeedAsync(existingFeed.FeedUrl);
        updatedFeed.Id = existingFeed.Id;
        foreach (var article in updatedFeed.Articles)
            article.FeedId = existingFeed.Id;
        updatedFeed = await storage.UpdateFeedAsync(updatedFeed, userId);
        return Results.Ok(MapFeedResponse(updatedFeed));
    }
    catch (Exception ex)
    {
        return BadRequest($"Failed to refresh feed: {ex.Message}");
    }
});

var articleApi = app.MapGroup("/api/articles");

articleApi.MapPost("/summarize-today", async (HttpContext context,
    AuthService auth, FeedStorageService storage, AiService ai) =>
{
    var userId = await GetUserId(context, auth, storage, requireAuth: true);
    if (userId is null) return Unauthorized("Sign in to use AI features.");

    var articles = await storage.GetTodayArticlesAsync(userId);
    if (articles.Count == 0)
        return Results.Ok(new { digest = (string?)null, message = "No articles from today." });

    var prompt = "Summarize today's RSS feed articles in 4-6 bullet points. Cover the key stories. Respond in the language the articles are written in.\n\n";
    for (int i = 0; i < articles.Count; i++)
    {
        var a = articles[i];
        prompt += $"{i + 1}. Title: {a.Title}\n   Content: {a.Summary}\n\n";
    }

    var summary = await ai.SummarizeAsync(prompt);
    await storage.SaveDailyDigestAsync(userId, summary);

    return Results.Ok(new { digest = summary });
});

articleApi.MapPost("/{id}/toggle-bookmark", async (string id, HttpContext context,
    AuthService auth, FeedStorageService storage) =>
{
    var userId = await GetUserId(context, auth, storage, requireAuth: false);
    if (userId is null) return Unauthorized("Not authenticated.");

    var bookmarked = await storage.ToggleBookmarkAsync(userId, id);
    return Results.Ok(new { bookmarked });
});

var postApi = app.MapGroup("/api/posts");

postApi.MapGet("/", async (HttpContext context, AuthService auth, FeedStorageService storage) =>
{
    var userId = await GetUserId(context, auth, storage, requireAuth: true);
    if (userId is null) return Unauthorized("Not authenticated.");

    var posts = await storage.GetUserPostsAsync(userId);
    return Results.Ok(posts.Select(p => new PostResponse(p.Id, p.Title, p.Content, p.PublishedAt, p.UpdatedAt)));
});

postApi.MapPost("/", async (CreatePostRequest request, HttpContext context,
    AuthService auth, FeedStorageService storage) =>
{
    var userId = await GetUserId(context, auth, storage, requireAuth: true);
    if (userId is null) return Unauthorized("Not authenticated.");

    if (string.IsNullOrWhiteSpace(request.Title))
        return BadRequest("Title is required.");

    var post = new Post
    {
        UserId = userId,
        Title = request.Title.Trim(),
        Content = request.Content?.Trim() ?? ""
    };

    post = await storage.CreatePostAsync(post);
    return Results.Ok(new PostResponse(post.Id, post.Title, post.Content, post.PublishedAt, post.UpdatedAt));
});

postApi.MapPut("/{id}", async (string id, UpdatePostRequest request, HttpContext context,
    AuthService auth, FeedStorageService storage) =>
{
    var userId = await GetUserId(context, auth, storage, requireAuth: true);
    if (userId is null) return Unauthorized("Not authenticated.");

    var existing = await storage.GetPostAsync(id);
    if (existing is null || existing.UserId != userId)
        return Results.NotFound(new { error = "Post not found." });

    if (string.IsNullOrWhiteSpace(request.Title))
        return BadRequest("Title is required.");

    existing.Title = request.Title.Trim();
    existing.Content = request.Content?.Trim() ?? "";

    await storage.UpdatePostAsync(existing, userId);
    return Results.Ok(new PostResponse(existing.Id, existing.Title, existing.Content, existing.PublishedAt, existing.UpdatedAt));
});

postApi.MapDelete("/{id}", async (string id, HttpContext context,
    AuthService auth, FeedStorageService storage) =>
{
    var userId = await GetUserId(context, auth, storage, requireAuth: true);
    if (userId is null) return Unauthorized("Not authenticated.");

    var deleted = await storage.DeletePostAsync(id, userId);
    if (!deleted) return Results.NotFound(new { error = "Post not found." });
    return Results.Ok(new { deleted = true });
});

app.MapGet("/api/feed/{handle}", async (string handle, FeedStorageService storage) =>
{
    var userId = await storage.GetUserIdByHandleAsync(handle);
    if (userId is null) return Results.NotFound("User not found.");

    var posts = await storage.GetUserPostsAsync(userId);

    var sb = new StringBuilder();
    sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
    sb.AppendLine("<rss version=\"2.0\">");
    sb.AppendLine("<channel>");
    sb.AppendLine($"<title>{WebUtility.HtmlEncode(handle + "'s Posts")}</title>");
    sb.AppendLine($"<link>https://rss-reader-production-e719.up.railway.app/api/feed/{WebUtility.HtmlEncode(handle)}</link>");
    sb.AppendLine($"<description>RSS feed for {WebUtility.HtmlEncode(handle)}</description>");

    foreach (var post in posts)
    {
        sb.AppendLine("<item>");
        sb.AppendLine($"<title>{WebUtility.HtmlEncode(post.Title)}</title>");
        sb.AppendLine($"<description>{WebUtility.HtmlEncode(post.Content)}</description>");
        sb.AppendLine($"<pubDate>{post.PublishedAt:r}</pubDate>");
        sb.AppendLine($"<guid>{post.Id}</guid>");
        sb.AppendLine("</item>");
    }

    sb.AppendLine("</channel>");
    sb.AppendLine("</rss>");

    return Results.Content(sb.ToString(), "application/rss+xml", Encoding.UTF8);
});

app.Run();

static FeedResponse MapFeedResponse(Feed feed)
{
    return new FeedResponse(
        feed.Id, feed.Title, feed.FeedUrl, feed.SiteUrl, feed.Description,
        feed.LastRefreshed,
        feed.Articles.Select(a => new ArticleResponse(
            a.Id, a.Title, a.Url, a.Summary, a.Published, a.FeedId,
            a.EnclosureUrl, a.EnclosureType, a.IsBookmarked
        )).ToList()
    );
}

static async Task<string?> GetUserId(HttpContext context, AuthService auth,
    FeedStorageService storage, bool requireAuth)
{
    var header = context.Request.Headers.Authorization.ToString();
    if (!string.IsNullOrEmpty(header) && header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
    {
        var token = header["Bearer ".Length..];
        return await auth.ValidateSessionAsync(token);
    }

    if (requireAuth) return null;

    var anonId = context.Request.Headers["X-User-Id"].ToString();
    if (!string.IsNullOrEmpty(anonId))
        return await storage.GetOrCreateAnonymousUserAsync(anonId);

    return null;
}

static string? ExtractToken(HttpContext context)
{
    var header = context.Request.Headers.Authorization.ToString();
    if (string.IsNullOrEmpty(header) || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        return null;
    return header["Bearer ".Length..];
}

static IResult BadRequest(string message) => Results.BadRequest(new { error = message });
static IResult Conflict(string message) => Results.Conflict(new { error = message });
static IResult Unauthorized(string message) => Results.Json(new { error = message }, statusCode: 401);
