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
    logger.LogWarning(ex, "Database initialization failed. API endpoints will not work until the database is reachable. Check your connection string in appsettings.json (Supabase pooler uses port 6543, not 5432).");
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.Use(async (context, next) =>
{
    try { await next(); }
    catch (Exception)
    {
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { error = "Internal server error." });
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

authApi.MapPost("/signout", async (HttpContext context, AuthService auth) =>
{
    var userId = await GetUserId(context, auth);
    if (userId is null) return Unauthorized("Not authenticated.");

    var token = ExtractToken(context);
    if (token is not null)
        await auth.SignOutAsync(token);

    return Results.Ok(new { ok = true });
});

authApi.MapGet("/me", async (HttpContext context, AuthService auth) =>
{
    var userId = await GetUserId(context, auth);
    if (userId is null) return Unauthorized("Not authenticated.");

    var user = await auth.GetUserAsync(userId);
    if (user is null) return Unauthorized("User not found.");

    return Results.Ok(new { user.Id, user.Email, user.EmailVerified });
});

var feedApi = app.MapGroup("/api/feeds");

feedApi.MapGet("/", async (HttpContext context, AuthService auth, FeedStorageService storage,
    int? page, int? pageSize, string? feedId) =>
{
    var userId = await GetUserId(context, auth);
    if (userId is null) return Unauthorized("Not authenticated.");

    var p = Math.Max(1, page ?? 1);
    var ps = Math.Clamp(pageSize ?? 20, 1, 100);

    var feeds = await storage.GetAllFeedsAsync(userId);
    var allArticles = feeds
        .SelectMany(f => f.Articles.Select(a => new ArticleResponse(
            a.Id, a.Title, a.Url, a.Summary, a.Published, a.FeedId)))
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
            a.Id, a.Title, a.Url, a.Summary, a.Published, a.FeedId)).ToList()
    )).ToList();

    return Results.Ok(new
    {
        feeds = feedResponses,
        articles = pagedArticles,
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
    var userId = await GetUserId(context, auth);
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
    var userId = await GetUserId(context, auth);
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
    var userId = await GetUserId(context, auth);
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

app.Run();

static FeedResponse MapFeedResponse(Feed feed)
{
    return new FeedResponse(
        feed.Id, feed.Title, feed.FeedUrl, feed.SiteUrl, feed.Description,
        feed.LastRefreshed,
        feed.Articles.Select(a => new ArticleResponse(
            a.Id, a.Title, a.Url, a.Summary, a.Published, a.FeedId
        )).ToList()
    );
}

static async Task<string?> GetUserId(HttpContext context, AuthService auth)
{
    var header = context.Request.Headers.Authorization.ToString();
    if (string.IsNullOrEmpty(header) || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        return null;
    var token = header["Bearer ".Length..];
    return await auth.ValidateSessionAsync(token);
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
