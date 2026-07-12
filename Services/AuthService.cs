using Dapper;
using RssReader.Models;

namespace RssReader.Services;

public class AuthService
{
    private readonly DatabaseService _db;

    public AuthService(DatabaseService db)
    {
        _db = db;
    }

    public async Task<AuthResponse> SignUpAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || username.Length < 2)
            throw new ArgumentException("Username must be at least 2 characters.");
        if (string.IsNullOrWhiteSpace(password) || password.Length < 4)
            throw new ArgumentException("Password must be at least 4 characters.");

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);

        using var conn = _db.OpenConnection();
        var existingUser = await conn.QueryFirstOrDefaultAsync<User>(
            "SELECT id::text, username FROM users WHERE username = @Username",
            new { Username = username });

        if (existingUser is not null)
            throw new InvalidOperationException("Username already taken.");

        var userId = await conn.QuerySingleAsync<string>(
            "INSERT INTO users (username, password_hash) VALUES (@Username, @PasswordHash) RETURNING id::text",
            new { Username = username, PasswordHash = passwordHash });

        var token = Guid.NewGuid().ToString("N");
        await conn.ExecuteAsync(
            "INSERT INTO sessions (user_id, token, expires_at) VALUES (@UserId::uuid, @Token, @ExpiresAt)",
            new { UserId = userId, Token = token, ExpiresAt = DateTime.UtcNow.AddDays(30) });

        return new AuthResponse(token, new UserResponse(userId, username));
    }

    public async Task<AuthResponse> SignInAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Username and password are required.");

        using var conn = _db.OpenConnection();
        var user = await conn.QueryFirstOrDefaultAsync<User>(
            "SELECT id::text, username, password_hash FROM users WHERE username = @Username",
            new { Username = username });

        if (user is null || string.IsNullOrEmpty(user.PasswordHash)
            || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid username or password.");

        var token = Guid.NewGuid().ToString("N");
        await conn.ExecuteAsync(
            "INSERT INTO sessions (user_id, token, expires_at) VALUES (@UserId::uuid, @Token, @ExpiresAt)",
            new { UserId = user.Id, Token = token, ExpiresAt = DateTime.UtcNow.AddDays(30) });

        return new AuthResponse(token, new UserResponse(user.Id, user.Username));
    }

    public async Task<string?> ValidateSessionAsync(string token)
    {
        using var conn = _db.OpenConnection();
        return await conn.QueryFirstOrDefaultAsync<string>(
            "SELECT user_id::text FROM sessions WHERE token = @Token AND expires_at > NOW()",
            new { Token = token });
    }

    public async Task SignOutAsync(string token)
    {
        using var conn = _db.OpenConnection();
        await conn.ExecuteAsync("DELETE FROM sessions WHERE token = @Token", new { Token = token });
    }

    public async Task<User?> GetUserAsync(string userId)
    {
        using var conn = _db.OpenConnection();
        return await conn.QueryFirstOrDefaultAsync<User>(
            "SELECT id::text, username FROM users WHERE id = @UserId::uuid",
            new { UserId = userId });
    }
}
