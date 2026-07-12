using Dapper;
using Npgsql;
using RssReader.Models;

namespace RssReader.Services;

public class FeedStorageService
{
    private readonly DatabaseService _db;

    public FeedStorageService(DatabaseService db)
    {
        _db = db;
    }

    public async Task<List<Feed>> GetAllFeedsAsync(string userId)
    {
        using var conn = _db.OpenConnection();
        var feeds = (await conn.QueryAsync<Feed>(
            "SELECT id, user_id::text AS UserId, title, feed_url AS FeedUrl, site_url AS SiteUrl, " +
            "description, last_refreshed AS LastRefreshed FROM feeds WHERE user_id = @UserId::uuid",
            new { UserId = userId })).ToList();

        foreach (var feed in feeds)
        {
            feed.Articles = (await conn.QueryAsync<Article>(
                "SELECT id, title, url, summary, published, feed_id AS FeedId " +
                "FROM articles WHERE feed_id = @FeedId ORDER BY published DESC",
                new { FeedId = feed.Id })).ToList();
        }

        return feeds;
    }

    public async Task<Feed?> GetFeedAsync(string id, string userId)
    {
        using var conn = _db.OpenConnection();
        var feed = await conn.QueryFirstOrDefaultAsync<Feed>(
            "SELECT id, user_id::text AS UserId, title, feed_url AS FeedUrl, site_url AS SiteUrl, " +
            "description, last_refreshed AS LastRefreshed FROM feeds WHERE id = @Id AND user_id = @UserId::uuid",
            new { Id = id, UserId = userId });

        if (feed is not null)
        {
            feed.Articles = (await conn.QueryAsync<Article>(
                "SELECT id, title, url, summary, published, feed_id AS FeedId " +
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
            "VALUES (@Id, @UserId::uuid, @Title, @FeedUrl, @SiteUrl, @Description, @LastRefreshed)",
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
            "DELETE FROM feeds WHERE id = @Id AND user_id = @UserId::uuid",
            new { Id = id, UserId = userId });
    }

    public async Task<Feed> UpdateFeedAsync(Feed feed, string userId)
    {
        using var conn = _db.OpenConnection();
        using var tx = conn.BeginTransaction();

        var existing = await conn.QueryFirstOrDefaultAsync<Feed>(
            "SELECT id FROM feeds WHERE id = @Id AND user_id = @UserId::uuid",
            new { feed.Id, UserId = userId }, tx);

        if (existing is null)
            throw new KeyNotFoundException($"Feed with id {feed.Id} not found.");

        await conn.ExecuteAsync(
            "DELETE FROM articles WHERE feed_id = @FeedId", new { FeedId = feed.Id }, tx);

        await conn.ExecuteAsync(
            "UPDATE feeds SET title = @Title, site_url = @SiteUrl, description = @Description, " +
            "last_refreshed = @LastRefreshed WHERE id = @Id AND user_id = @UserId::uuid",
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

    private static async Task InsertArticlesAsync(NpgsqlConnection conn, List<Article> articles, NpgsqlTransaction tx)
    {
        foreach (var article in articles)
        {
            await conn.ExecuteAsync(
                "INSERT INTO articles (id, feed_id, title, url, summary, published) " +
                "VALUES (@Id, @FeedId, @Title, @Url, @Summary, @Published)",
                article, tx);
        }
    }
}
