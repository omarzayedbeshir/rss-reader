using Dapper;
using Microsoft.Data.Sqlite;
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
            "SELECT id, user_id AS UserId, title, feed_url AS FeedUrl, site_url AS SiteUrl, " +
            "description, last_refreshed AS LastRefreshed FROM feeds WHERE user_id = @UserId",
            new { UserId = userId })).ToList();

        foreach (var feed in feeds)
        {
            feed.Articles = (await conn.QueryAsync<Article>(
                "SELECT id, title, url, summary, published, feed_id AS FeedId, " +
                "enclosure_url AS EnclosureUrl, enclosure_type AS EnclosureType " +
                "FROM articles WHERE feed_id = @FeedId ORDER BY published DESC",
                new { FeedId = feed.Id })).ToList();
        }

        return feeds;
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
