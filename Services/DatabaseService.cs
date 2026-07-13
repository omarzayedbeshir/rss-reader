using Dapper;
using Microsoft.Data.Sqlite;

namespace RssReader.Services;

public class DatabaseService
{
    private readonly string _connectionString;

    public DatabaseService(IConfiguration configuration)
    {
        var dbPath = Environment.GetEnvironmentVariable("SQLITE_PATH");
        if (string.IsNullOrEmpty(dbPath))
        {
            var rail = Environment.GetEnvironmentVariable("RAILWAY_VOLUME_MOUNT_PATH");
            dbPath = !string.IsNullOrEmpty(rail)
                ? Path.Combine(rail, "rssreader.db")
                : Path.Combine(AppContext.BaseDirectory, "Data", "rssreader.db");
        }
        var dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        _connectionString = $"Data Source={dbPath}";
    }

    public SqliteConnection OpenConnection()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var pragmaCmd = conn.CreateCommand();
        pragmaCmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON; PRAGMA busy_timeout=5000;";
        pragmaCmd.ExecuteNonQuery();
        return conn;
    }

    public async Task InitializeAsync()
    {
        using var conn = OpenConnection();
        await conn.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS users (
                id TEXT PRIMARY KEY,
                email TEXT UNIQUE NOT NULL,
                password_hash TEXT NOT NULL,
                email_verified INTEGER NOT NULL DEFAULT 0,
                verification_token TEXT,
                created_at TEXT DEFAULT (datetime('now'))
            );

            CREATE TABLE IF NOT EXISTS sessions (
                id TEXT PRIMARY KEY,
                user_id TEXT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
                token TEXT UNIQUE NOT NULL,
                created_at TEXT DEFAULT (datetime('now')),
                expires_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS feeds (
                id TEXT PRIMARY KEY,
                user_id TEXT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
                title TEXT NOT NULL,
                feed_url TEXT NOT NULL,
                site_url TEXT NOT NULL DEFAULT '',
                description TEXT NOT NULL DEFAULT '',
                last_refreshed TEXT DEFAULT (datetime('now'))
            );

            CREATE TABLE IF NOT EXISTS articles (
                id TEXT PRIMARY KEY,
                feed_id TEXT NOT NULL REFERENCES feeds(id) ON DELETE CASCADE,
                title TEXT NOT NULL,
                url TEXT NOT NULL DEFAULT '',
                summary TEXT NOT NULL DEFAULT '',
                published TEXT DEFAULT (datetime('now'))
            );

            CREATE INDEX IF NOT EXISTS idx_sessions_token ON sessions(token);
            CREATE INDEX IF NOT EXISTS idx_feeds_user_id ON feeds(user_id);
            CREATE INDEX IF NOT EXISTS idx_articles_feed_id ON articles(feed_id);
        ");
    }
}
