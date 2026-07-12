using Dapper;
using Npgsql;

namespace RssReader.Services;

public class DatabaseService
{
    private readonly NpgsqlDataSource _dataSource;

    public DatabaseService(IConfiguration configuration)
    {
        var connString = configuration.GetConnectionString("Default")
            ?? Environment.GetEnvironmentVariable("SUPABASE_CONNECTION_STRING")
            ?? throw new InvalidOperationException(
                "Database connection string not configured. Set ConnectionStrings:Default in appsettings.json" +
                " or SUPABASE_CONNECTION_STRING environment variable.");
        _dataSource = NpgsqlDataSource.Create(connString);
    }

    public NpgsqlConnection OpenConnection()
    {
        return _dataSource.OpenConnection();
    }

    public async Task InitializeAsync()
    {
        using var conn = OpenConnection();
        await conn.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS users (
                id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                email TEXT UNIQUE NOT NULL,
                password_hash TEXT NOT NULL,
                email_verified BOOLEAN NOT NULL DEFAULT FALSE,
                verification_token TEXT,
                created_at TIMESTAMPTZ DEFAULT NOW()
            );

            CREATE TABLE IF NOT EXISTS sessions (
                id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
                token TEXT UNIQUE NOT NULL,
                created_at TIMESTAMPTZ DEFAULT NOW(),
                expires_at TIMESTAMPTZ NOT NULL
            );

            CREATE TABLE IF NOT EXISTS feeds (
                id TEXT PRIMARY KEY,
                user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
                title TEXT NOT NULL,
                feed_url TEXT NOT NULL,
                site_url TEXT NOT NULL DEFAULT '',
                description TEXT NOT NULL DEFAULT '',
                last_refreshed TIMESTAMPTZ DEFAULT NOW()
            );

            CREATE TABLE IF NOT EXISTS articles (
                id TEXT PRIMARY KEY,
                feed_id TEXT NOT NULL REFERENCES feeds(id) ON DELETE CASCADE,
                title TEXT NOT NULL,
                url TEXT NOT NULL DEFAULT '',
                summary TEXT NOT NULL DEFAULT '',
                published TIMESTAMPTZ DEFAULT NOW()
            );

            CREATE INDEX IF NOT EXISTS idx_sessions_token ON sessions(token);
            CREATE INDEX IF NOT EXISTS idx_feeds_user_id ON feeds(user_id);
            CREATE INDEX IF NOT EXISTS idx_articles_feed_id ON articles(feed_id);
        ");

        await MigrateUsersTableAsync(conn);
    }

    private static async Task MigrateUsersTableAsync(NpgsqlConnection conn)
    {
        var hasUsername = await conn.QueryFirstOrDefaultAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='users' AND column_name='username')");

        if (hasUsername)
        {
            await conn.ExecuteAsync("ALTER TABLE users RENAME COLUMN username TO email");
        }

        var hasEmailVerified = await conn.QueryFirstOrDefaultAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='users' AND column_name='email_verified')");

        if (!hasEmailVerified)
        {
            await conn.ExecuteAsync("ALTER TABLE users ADD COLUMN email_verified BOOLEAN NOT NULL DEFAULT FALSE");
        }

        var hasVerificationToken = await conn.QueryFirstOrDefaultAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='users' AND column_name='verification_token')");

        if (!hasVerificationToken)
        {
            await conn.ExecuteAsync("ALTER TABLE users ADD COLUMN verification_token TEXT");
        }
    }
}
