using Dapper;
using RssReader.Models;

namespace RssReader.Services;

public class AuthService
{
    private readonly DatabaseService _db;
    private readonly EmailService _email;

    public AuthService(DatabaseService db, EmailService email)
    {
        _db = db;
        _email = email;
    }

    public async Task SignUpAsync(string email, string password, string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            throw new ArgumentException("A valid email is required.");
        if (string.IsNullOrWhiteSpace(password) || password.Length < 4)
            throw new ArgumentException("Password must be at least 4 characters.");

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);
        var verificationToken = Guid.NewGuid().ToString("N");
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var userId = Guid.NewGuid().ToString("N")[..12];

        using var conn = _db.OpenConnection();
        var exists = await conn.QueryFirstOrDefaultAsync<string>(
            "SELECT id FROM users WHERE email = @Email",
            new { Email = normalizedEmail });

        if (exists is not null)
            throw new InvalidOperationException("Email already registered.");

        await conn.ExecuteAsync(
            "INSERT INTO users (id, email, password_hash, email_verified, verification_token) " +
            "VALUES (@Id, @Email, @PasswordHash, 0, @VerificationToken)",
            new { Id = userId, Email = normalizedEmail, PasswordHash = passwordHash, VerificationToken = verificationToken });

        try
        {
            await _email.SendVerificationEmailAsync(email, verificationToken, baseUrl);
        }
        catch
        {
            await conn.ExecuteAsync("DELETE FROM users WHERE id = @Id", new { Id = userId });
            throw;
        }
    }

    public async Task<AuthResponse> SignInAsync(string email, string password)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Email and password are required.");

        using var conn = _db.OpenConnection();
        var row = await conn.QueryFirstOrDefaultAsync(
            "SELECT id, email, password_hash AS passwordhash, email_verified AS emailverified FROM users WHERE email = @Email",
            new { Email = email.Trim().ToLowerInvariant() });

        if (row is null)
            throw new UnauthorizedAccessException("Invalid email or password.");

        string userId = row.id;
        string userEmail = row.email;
        string passwordHash = row.passwordhash;
        bool emailVerified = (long)row.emailverified != 0;

        if (string.IsNullOrEmpty(passwordHash) || !BCrypt.Net.BCrypt.Verify(password, passwordHash))
            throw new UnauthorizedAccessException("Invalid email or password.");

        if (!emailVerified)
            throw new UnauthorizedAccessException("Email not verified. Check your inbox or request a new verification email.");

        var sessionId = Guid.NewGuid().ToString("N")[..12];
        var token = Guid.NewGuid().ToString("N");
        var expiresAt = DateTime.UtcNow.AddDays(30).ToString("O");
        await conn.ExecuteAsync(
            "INSERT INTO sessions (id, user_id, token, expires_at) VALUES (@Id, @UserId, @Token, @ExpiresAt)",
            new { Id = sessionId, UserId = userId, Token = token, ExpiresAt = expiresAt });

        return new AuthResponse(token, new UserResponse(userId, userEmail, emailVerified));
    }

    public async Task VerifyEmailAsync(string verificationToken)
    {
        using var conn = _db.OpenConnection();
        var row = await conn.QueryFirstOrDefaultAsync(
            "SELECT id, email FROM users WHERE verification_token = @Token",
            new { Token = verificationToken });

        if (row is null)
            throw new InvalidOperationException("Invalid or expired verification link.");

        await conn.ExecuteAsync(
            "UPDATE users SET email_verified = 1, verification_token = NULL WHERE id = @Id",
            new { Id = (string)row.id });
    }

    public async Task ResendVerificationAsync(string email, string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required.");

        using var conn = _db.OpenConnection();
        var row = await conn.QueryFirstOrDefaultAsync(
            "SELECT id, email, email_verified AS emailverified FROM users WHERE email = @Email",
            new { Email = email.Trim().ToLowerInvariant() });

        if (row is null)
            return;

        if ((long)row.emailverified != 0)
            throw new InvalidOperationException("Email is already verified.");

        var newToken = Guid.NewGuid().ToString("N");
        await conn.ExecuteAsync(
            "UPDATE users SET verification_token = @Token WHERE id = @Id",
            new { Token = newToken, Id = (string)row.id });

        await _email.SendVerificationEmailAsync((string)row.email, newToken, baseUrl);
    }

    public async Task<string?> ValidateSessionAsync(string token)
    {
        using var conn = _db.OpenConnection();
        return await conn.QueryFirstOrDefaultAsync<string>(
            "SELECT user_id FROM sessions WHERE token = @Token AND expires_at > datetime('now')",
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
        var row = await conn.QueryFirstOrDefaultAsync(
            "SELECT id, email, email_verified AS emailverified FROM users WHERE id = @UserId",
            new { UserId = userId });

        if (row is null) return null;

        return new User
        {
            Id = row.id,
            Email = row.email,
            EmailVerified = (long)row.emailverified != 0
        };
    }
}
