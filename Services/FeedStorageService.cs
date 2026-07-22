using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Data.Sqlite;
using RssReader.Models;

namespace RssReader.Services;

public class FeedStorageService
{
    private static readonly string[] DefaultFeeds = ["https://feeds.bbci.co.uk/news/rss.xml"];

    private readonly DatabaseService _db;
    private readonly FeedFetchService _fetchService;

    public FeedStorageService(DatabaseService db, FeedFetchService fetchService)
    {
        _db = db;
        _fetchService = fetchService;
    }

    public async Task<List<Feed>> GetAllFeedsAsync(string userId, bool bookmarksOnly = false)
    {
        using var conn = _db.OpenConnection();

        string articleQuery;
        if (bookmarksOnly)
        {
            articleQuery =
                "SELECT a.id, a.title, a.url, a.summary, a.published, a.feed_id AS FeedId, " +
                "a.enclosure_url AS EnclosureUrl, a.enclosure_type AS EnclosureType, 1 AS IsBookmarked " +
                "FROM articles a " +
                "JOIN bookmarks b ON a.id = b.article_id AND b.user_id = @UserId " +
                "JOIN feeds f ON a.feed_id = f.id AND f.user_id = @UserId " +
                "ORDER BY a.published DESC";
        }
        else
        {
            articleQuery =
                "SELECT a.id, a.title, a.url, a.summary, a.published, a.feed_id AS FeedId, " +
                "a.enclosure_url AS EnclosureUrl, a.enclosure_type AS EnclosureType, " +
                "CASE WHEN b.article_id IS NOT NULL THEN 1 ELSE 0 END AS IsBookmarked " +
                "FROM articles a " +
                "JOIN feeds f ON a.feed_id = f.id AND f.user_id = @UserId " +
                "LEFT JOIN bookmarks b ON a.id = b.article_id AND b.user_id = @UserId " +
                "ORDER BY a.published DESC";
        }

        var articles = (await conn.QueryAsync<Article>(articleQuery, new { UserId = userId })).ToList();

        var feeds = articles.GroupBy(a => a.FeedId).Select(g => new Feed
        {
            Id = g.Key,
            UserId = userId,
            Title = "",
            FeedUrl = "",
            Articles = g.ToList()
        }).ToList();

        foreach (var feed in feeds)
        {
            var feedData = await conn.QueryFirstOrDefaultAsync<Feed>(
                "SELECT id, user_id AS UserId, title, feed_url AS FeedUrl, site_url AS SiteUrl, " +
                "description, last_refreshed AS LastRefreshed FROM feeds WHERE id = @Id AND user_id = @UserId",
                new { Id = feed.Id, UserId = userId });
            if (feedData is not null)
            {
                feed.Title = feedData.Title;
                feed.FeedUrl = feedData.FeedUrl;
                feed.SiteUrl = feedData.SiteUrl;
                feed.Description = feedData.Description;
                feed.LastRefreshed = feedData.LastRefreshed;
            }
        }

        return feeds.OrderBy(f => f.Title).ToList();
    }

    public async Task<Feed?> GetFeedAsync(string id, string userId)
    {
        using var conn = _db.OpenConnection();
        var feed = await conn.QueryFirstOrDefaultAsync<Feed>(
            "SELECT id, user_id AS UserId, title, feed_url AS FeedUrl, site_url AS SiteUrl, " +
            "description, last_refreshed AS LastRefreshed FROM feeds WHERE id = @Id AND user_id = @UserId",
            new { Id = id, UserId = userId });

        if (feed is not null)
        {
            feed.Articles = (await conn.QueryAsync<Article>(
                "SELECT id, title, url, summary, published, feed_id AS FeedId, " +
                "enclosure_url AS EnclosureUrl, enclosure_type AS EnclosureType " +
                "FROM articles WHERE feed_id = @FeedId ORDER BY published DESC",
                new { FeedId = feed.Id })).ToList();
        }

        return feed;
    }

    public async Task<Feed> AddFeedAsync(Feed feed, string userId)
    {
        feed.UserId = userId;

        using var conn = _db.OpenConnection();
        using var tx = conn.BeginTransaction();

        await conn.ExecuteAsync(
            "INSERT INTO feeds (id, user_id, title, feed_url, site_url, description, last_refreshed) " +
            "VALUES (@Id, @UserId, @Title, @FeedUrl, @SiteUrl, @Description, @LastRefreshed)",
            feed, tx);

        if (feed.Articles.Count > 0)
        {
            await InsertArticlesAsync(conn, feed.Articles, tx);
        }

        tx.Commit();
        return feed;
    }

    public async Task RemoveFeedAsync(string id, string userId)
    {
        using var conn = _db.OpenConnection();
        await conn.ExecuteAsync(
            "DELETE FROM feeds WHERE id = @Id AND user_id = @UserId",
            new { Id = id, UserId = userId });
    }

    public async Task<Feed> UpdateFeedAsync(Feed feed, string userId)
    {
        feed.UserId = userId;

        using var conn = _db.OpenConnection();
        using var tx = conn.BeginTransaction();

        var existing = await conn.QueryFirstOrDefaultAsync<Feed>(
            "SELECT id FROM feeds WHERE id = @Id AND user_id = @UserId",
            new { feed.Id, UserId = userId }, tx);

        if (existing is null)
            throw new KeyNotFoundException($"Feed with id {feed.Id} not found.");

        await conn.ExecuteAsync(
            "DELETE FROM articles WHERE feed_id = @FeedId", new { FeedId = feed.Id }, tx);

        await conn.ExecuteAsync(
            "UPDATE feeds SET title = @Title, site_url = @SiteUrl, description = @Description, " +
            "last_refreshed = @LastRefreshed WHERE id = @Id AND user_id = @UserId",
            feed, tx);

        if (feed.Articles.Count > 0)
        {
            foreach (var article in feed.Articles)
                article.FeedId = feed.Id;
            await InsertArticlesAsync(conn, feed.Articles, tx);
        }

        tx.Commit();
        return feed;
    }

    public async Task<string?> GetDailyDigestAsync(string userId)
    {
        using var conn = _db.OpenConnection();
        return await conn.QueryFirstOrDefaultAsync<string>(
            "SELECT summary FROM daily_summaries WHERE user_id = @UserId AND date = date('now')",
            new { UserId = userId });
    }

    public async Task SaveDailyDigestAsync(string userId, string summary)
    {
        using var conn = _db.OpenConnection();
        await conn.ExecuteAsync(
            "INSERT OR REPLACE INTO daily_summaries (user_id, date, summary) VALUES (@UserId, date('now'), @Summary)",
            new { UserId = userId, Summary = summary });
    }

    public async Task<string> GetOrCreateAnonymousUserAsync(string anonymousId)
    {
        using var conn = _db.OpenConnection();
        var exists = await conn.QueryFirstOrDefaultAsync<string>(
            "SELECT id FROM users WHERE id = @Id", new { Id = anonymousId });
        if (exists is not null)
        {
            await conn.ExecuteAsync(
                "UPDATE users SET last_accessed_at = datetime('now') WHERE id = @Id",
                new { Id = anonymousId });
            return exists;
        }
        await conn.ExecuteAsync(
            "INSERT INTO users (id, email, password_hash, email_verified, last_accessed_at) " +
            "VALUES (@Id, @Email, '', 0, datetime('now'))",
            new { Id = anonymousId, Email = $"anon_{anonymousId}@demo.local" });

        await AddDefaultFeedsAsync(anonymousId);
        return anonymousId;
    }

    private async Task AddDefaultFeedsAsync(string userId)
    {
        foreach (var url in DefaultFeeds)
        {
            try
            {
                var feed = await _fetchService.FetchFeedAsync(url);
                await AddFeedAsync(feed, userId);
            }
            catch
            {
                // ignore failed feed
            }
        }
    }

    public async Task<List<Article>> GetTodayArticlesAsync(string userId)
    {
        using var conn = _db.OpenConnection();
        return (await conn.QueryAsync<Article>(
            "SELECT a.id, a.title, a.summary FROM articles a " +
            "JOIN feeds f ON a.feed_id = f.id " +
            "WHERE f.user_id = @UserId AND date(a.published) = date('now') " +
            "ORDER BY a.published DESC",
            new { UserId = userId })).ToList();
    }

    public async Task<bool> ToggleBookmarkAsync(string userId, string articleId)
    {
        using var conn = _db.OpenConnection();
        var exists = await conn.QueryFirstOrDefaultAsync<string>(
            "SELECT article_id FROM bookmarks WHERE user_id = @UserId AND article_id = @ArticleId",
            new { UserId = userId, ArticleId = articleId });

        if (exists is not null)
        {
            await conn.ExecuteAsync(
                "DELETE FROM bookmarks WHERE user_id = @UserId AND article_id = @ArticleId",
                new { UserId = userId, ArticleId = articleId });
            return false;
        }

        await conn.ExecuteAsync(
            "INSERT INTO bookmarks (user_id, article_id) VALUES (@UserId, @ArticleId)",
            new { UserId = userId, ArticleId = articleId });
        return true;
    }

    public async Task<List<Post>> GetUserPostsAsync(string userId)
    {
        using var conn = _db.OpenConnection();
        return (await conn.QueryAsync<Post>(
            "SELECT id, user_id AS UserId, title, content, published_at AS PublishedAt, updated_at AS UpdatedAt FROM posts WHERE user_id = @UserId ORDER BY published_at DESC",
            new { UserId = userId })).ToList();
    }

    public async Task<Post?> GetPostAsync(string postId)
    {
        using var conn = _db.OpenConnection();
        return await conn.QueryFirstOrDefaultAsync<Post>(
            "SELECT id, user_id AS UserId, title, content, published_at AS PublishedAt, updated_at AS UpdatedAt FROM posts WHERE id = @Id",
            new { Id = postId });
    }

    public async Task<Post> CreatePostAsync(Post post)
    {
        using var conn = _db.OpenConnection();
        await conn.ExecuteAsync(
            "INSERT INTO posts (id, user_id, title, content, published_at, updated_at) VALUES (@Id, @UserId, @Title, @Content, @PublishedAt, @UpdatedAt)",
            post);
        return post;
    }

    public async Task<Post> UpdatePostAsync(Post post, string userId)
    {
        using var conn = _db.OpenConnection();
        post.UpdatedAt = DateTime.UtcNow;
        await conn.ExecuteAsync(
            "UPDATE posts SET title = @Title, content = @Content, updated_at = @UpdatedAt WHERE id = @Id AND user_id = @UserId",
            new { post.Id, post.UserId, post.Title, post.Content, post.UpdatedAt });
        return post;
    }

    public async Task<bool> DeletePostAsync(string postId, string userId)
    {
        using var conn = _db.OpenConnection();
        var rows = await conn.ExecuteAsync(
            "DELETE FROM posts WHERE id = @Id AND user_id = @UserId",
            new { Id = postId, UserId = userId });
        return rows > 0;
    }

    public async Task<string?> GetUserIdByHandleAsync(string handle)
    {
        using var conn = _db.OpenConnection();
        return await conn.QueryFirstOrDefaultAsync<string>(
            "SELECT id FROM users WHERE handle = @Handle",
            new { Handle = handle });
    }

    public async Task<string?> GetUserHandleAsync(string userId)
    {
        using var conn = _db.OpenConnection();
        return await conn.QueryFirstOrDefaultAsync<string>(
            "SELECT handle FROM users WHERE id = @Id",
            new { Id = userId });
    }

    public async Task<string> EnsureUserHandleAsync(string userId, string email)
    {
        using var conn = _db.OpenConnection();
        var existing = await conn.QueryFirstOrDefaultAsync<string>(
            "SELECT handle FROM users WHERE id = @Id",
            new { Id = userId });
        if (!string.IsNullOrEmpty(existing)) return existing;

        var baseHandle = Regex.Replace(email.Split('@')[0].ToLowerInvariant(), "[^a-z0-9_-]", "");
        if (string.IsNullOrEmpty(baseHandle)) baseHandle = "user";

        var candidate = baseHandle;
        var suffix = 0;
        while (await conn.QueryFirstOrDefaultAsync<string>(
            "SELECT id FROM users WHERE handle = @Handle AND id != @UserId",
            new { Handle = candidate, UserId = userId }) is not null)
        {
            suffix++;
            candidate = $"{baseHandle}-{suffix:X3}";
        }

        await conn.ExecuteAsync(
            "UPDATE users SET handle = @Handle WHERE id = @Id",
            new { Handle = candidate, Id = userId });
        return candidate;
    }

    public async Task<List<UserDiscoveryResponse>> DiscoverUsersAsync(string? userId, int limit = 5)
    {
        using var conn = _db.OpenConnection();

        if (userId is not null)
        {
            return (await conn.QueryAsync<UserDiscoveryResponse>(@"
                SELECT u.handle,
                       COUNT(DISTINCT f2.feed_url) AS SharedFeeds,
                       (SELECT COUNT(*) FROM feeds WHERE user_id = u.id) AS TotalFeeds,
                       (SELECT COUNT(*) FROM posts WHERE user_id = u.id) AS PostCount
                FROM users u
                JOIN feeds f1 ON f1.user_id = @UserId
                JOIN feeds f2 ON f2.user_id = u.id AND f2.feed_url = f1.feed_url
                WHERE u.id != @UserId AND u.handle IS NOT NULL
                GROUP BY u.id
                ORDER BY SharedFeeds DESC
                LIMIT @Limit",
                new { UserId = userId, Limit = limit })).AsList();
        }

        return (await conn.QueryAsync<UserDiscoveryResponse>(@"
            SELECT u.handle, 0 AS SharedFeeds, 0 AS TotalFeeds,
                   (SELECT COUNT(*) FROM posts WHERE user_id = u.id) AS PostCount
            FROM users u
            WHERE u.handle IS NOT NULL
              AND EXISTS (SELECT 1 FROM posts WHERE user_id = u.id)
            ORDER BY PostCount DESC
            LIMIT @Limit",
            new { Limit = limit })).AsList();
    }

    private static async Task InsertArticlesAsync(SqliteConnection conn, List<Article> articles, SqliteTransaction tx)
    {
        if (articles.Count == 0) return;

        var rows = new List<string>(articles.Count);
        var parameters = new DynamicParameters();

        for (int i = 0; i < articles.Count; i++)
        {
            var a = articles[i];
            rows.Add($"(@Id{i}, @FeedId{i}, @Title{i}, @Url{i}, @Summary{i}, @Published{i}, @EnclosureUrl{i}, @EnclosureType{i})");
            parameters.Add($"Id{i}", a.Id);
            parameters.Add($"FeedId{i}", a.FeedId);
            parameters.Add($"Title{i}", a.Title);
            parameters.Add($"Url{i}", a.Url);
            parameters.Add($"Summary{i}", a.Summary);
            parameters.Add($"Published{i}", a.Published);
            parameters.Add($"EnclosureUrl{i}", a.EnclosureUrl);
            parameters.Add($"EnclosureType{i}", a.EnclosureType);
        }

        var sql = "INSERT INTO articles (id, feed_id, title, url, summary, published, enclosure_url, enclosure_type) VALUES " +
                  string.Join(", ", rows);
        await conn.ExecuteAsync(sql, parameters, tx);
    }
}
