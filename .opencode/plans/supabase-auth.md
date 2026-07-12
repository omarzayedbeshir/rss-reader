# Supabase + User Auth Implementation Plan

## Overview
Replace local JSON file storage with Supabase PostgreSQL. Add username+password auth with simple token-based sessions. Scope all feeds per user.

---

## 1. Database Schema (run in Supabase SQL Editor)

```sql
CREATE TABLE users (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    username TEXT UNIQUE NOT NULL,
    password_hash TEXT NOT NULL,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE sessions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    token TEXT UNIQUE NOT NULL,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    expires_at TIMESTAMPTZ NOT NULL
);

CREATE TABLE feeds (
    id TEXT PRIMARY KEY,
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    title TEXT NOT NULL,
    feed_url TEXT NOT NULL,
    site_url TEXT NOT NULL DEFAULT '',
    description TEXT NOT NULL DEFAULT '',
    last_refreshed TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE articles (
    id TEXT PRIMARY KEY,
    feed_id TEXT NOT NULL REFERENCES feeds(id) ON DELETE CASCADE,
    title TEXT NOT NULL,
    url TEXT NOT NULL DEFAULT '',
    summary TEXT NOT NULL DEFAULT '',
    published TIMESTAMPTZ DEFAULT NOW()
);

CREATE INDEX idx_sessions_token ON sessions(token);
CREATE INDEX idx_feeds_user_id ON feeds(user_id);
CREATE INDEX idx_articles_feed_id ON articles(feed_id);
```

## 2. Files to Create

### `Services/DatabaseService.cs`
- Wraps Npgsql data source using `NpgsqlDataSource.Create(connectionString)`
- Exposes `NpgsqlConnection OpenConnection()` or DataSource property
- Reads connection string from `IConfiguration` (`ConnectionStrings:Default` or env var `SUPABASE_CONNECTION_STRING`)

### `Services/AuthService.cs`
- Inject `DatabaseService`
- `SignUpAsync(username, password)` → BCrypt hash → INSERT users → create session → return `{ token, user }`
- `SignInAsync(username, password)` → SELECT user → BCrypt verify → create session → return `{ token, user }`  
- `ValidateSessionAsync(token)` → SELECT session WHERE token AND expires_at > NOW() → return userId or null
- `SignOutAsync(token)` → DELETE FROM sessions WHERE token
- Session token = `Guid.NewGuid().ToString("N")`, expires = `DateTime.UtcNow.AddDays(30)`

## 3. Files to Modify

### `RssReader.csproj`
Add packages:
- `Npgsql` Version="9.*"
- `Dapper` Version="2.*"
- `BCrypt.Net-Next` Version="4.*"

### `appsettings.json`
Add:
```json
"ConnectionStrings": {
    "Default": ""
}
```
User sets this to their Supabase connection string.

### `Models/Feed.cs`
- Add `public string UserId` to `Feed` class
- Move `User` class here (wasn't needed before):
  ```csharp
  public class User
  {
      public string Id { get; set; } = string.Empty;
      public string Username { get; set; } = string.Empty;
  }
  ```

### `Models/ApiModels.cs`
Add records:
```csharp
public record SignUpRequest(string Username, string Password);
public record SignInRequest(string Username, string Password);
public record AuthResponse(string Token, UserResponse User);
public record UserResponse(string Id, string Username);
```

### `Services/FeedStorageService.cs` — Complete rewrite for PostgreSQL
- Remove JSON file logic entirely
- Inject `DatabaseService` instead of file path
- Every method takes `string userId` parameter
- Use Dapper queries:
  - `GetAllFeeds(userId)`: SELECT feeds WHERE user_id, then SELECT articles per feed
  - `GetFeed(id, userId)`: SELECT feed WHERE id AND user_id
  - `AddFeed(feed, userId)`: INSERT feed + batch INSERT articles
  - `RemoveFeed(id, userId)`: DELETE feed WHERE id AND user_id (articles cascade)
  - `UpdateFeed(feed, userId)`: DELETE old articles, INSERT new, UPDATE feed row

### `Program.cs`
**DI Registration changes:**
```csharp
builder.Services.AddSingleton<DatabaseService>();
builder.Services.AddSingleton<AuthService>();
builder.Services.AddSingleton<FeedStorageService>();
```

**New auth endpoints:**
- `POST /api/auth/signup` → `SignUpRequest` → `AuthResponse` or 409 (username taken) or 400
- `POST /api/auth/signin` → `SignInRequest` → `AuthResponse` or 401
- `POST /api/auth/signout` → (read token from header) → `{ ok: true }` or 401
- `GET /api/auth/me` → (read token) → `{ id, username }` or 401

**Helper function for auth validation:**
```csharp
static async Task<string?> GetUserId(HttpContext ctx, AuthService auth)
{
    var header = ctx.Request.Headers.Authorization.ToString();
    if (string.IsNullOrEmpty(header) || !header.StartsWith("Bearer "))
        return null;
    return await auth.ValidateSessionAsync(header["Bearer ".Length..]);
}
```

**Modified feed endpoints:** All require auth now. At top of each handler:
```csharp
var userId = await GetUserId(context, authService);
if (userId is null) return Results.Unauthorized();
// ... all storage calls pass userId
```

### `wwwroot/index.html`
Restructure: two views (auth view + app view).

**Auth view** (`#auth-view`):
```html
<div id="auth-view" class="auth-view">
    <div class="auth-card">
        <h1 class="auth-title">RSS Reader</h1>
        <p class="auth-subtitle">Sign in or create an account</p>
        <form id="auth-form" class="auth-form">
            <div class="auth-fields">
                <input id="auth-username" type="text" class="input" placeholder="Username" required autocomplete="username">
                <input id="auth-password" type="password" class="input" placeholder="Password" required autocomplete="current-password">
            </div>
            <div id="auth-error" class="auth-error hidden"></div>
            <button id="auth-submit" type="submit" class="btn btn-primary auth-submit">Sign In</button>
            <p class="auth-toggle">
                <button id="auth-toggle-btn" type="button" class="btn-link">Create an account</button>
            </p>
        </form>
    </div>
</div>
```

**App view** (`#app-view`, initially `hidden`): Same as existing layout but header adds:
```html
<span id="current-user" class="current-user"></span>
<button id="signout-btn" class="btn btn-secondary">Sign Out</button>
```

### `wwwroot/js/app.js`
**State additions:**
- `token`, `username`, `userId` (loaded from localStorage on init)

**New functions:**
- `checkAuth()` → calls `GET /api/auth/me` with stored token → if valid, show app; if invalid, show auth
- `signUp(username, password)` / `signIn(username, password)` → POST to auth endpoint, store token/user in localStorage, show app
- `signOut()` → POST `/api/auth/signout`, clear localStorage, show auth
- `authHeaders()` → returns headers object with Bearer token
- Toggle between `#auth-view` and `#app-view`

**Modified functions:**
- `fetchFeeds()`, `addFeed()`, `removeFeed()`, `refreshFeed()`, `refreshAllFeeds()` → use `authHeaders()` in fetch calls
- `init()` → calls `checkAuth()` first, then only loads feeds if authenticated

**Auth form event handling:**
- Toggle button switches form between Sign In / Sign Up mode (changes button text and submit handler)
- Form submit → calls signIn or signUp based on mode

### `wwwroot/css/style.css`
Add auth form styles:
```css
/* Auth */
.auth-view {
    display: flex;
    align-items: center;
    justify-content: center;
    min-height: 100vh;
    padding: 24px;
}

.auth-card {
    width: 100%;
    max-width: 380px;
    text-align: center;
}

.auth-title {
    font-family: var(--font-serif);
    font-size: 2rem;
    margin-bottom: 8px;
}

.auth-subtitle { ... }
.auth-form { ... }
.auth-fields { display: flex; flex-direction: column; gap: 12px; }
.auth-submit { width: 100%; }
.auth-error { color: var(--color-danger); font-size: 0.85rem; margin-top: 8px; }
.auth-toggle { margin-top: 16px; font-size: 0.85rem; color: var(--color-text-muted); }
.btn-link { background: none; border: none; cursor: pointer; ... }

/* User display in header */
.current-user { font-size: 0.85rem; color: var(--color-text-muted); }
```

---

## 4. API Endpoints Summary

| Method | Path | Auth | Request Body | Response |
|--------|------|------|-------------|----------|
| POST | /api/auth/signup | No | `{ username, password }` | `{ token, user: { id, username } }` |
| POST | /api/auth/signin | No | `{ username, password }` | `{ token, user: { id, username } }` |
| POST | /api/auth/signout | Yes | — | `{ ok: true }` |
| GET | /api/auth/me | Yes | — | `{ id, username }` |
| GET | /api/feeds | Yes | — | `{ feeds, articles, page, ... }` |
| POST | /api/feeds | Yes | `{ url }` | `FeedResponse` |
| DELETE | /api/feeds/{id} | Yes | — | `{ deleted: true }` |
| POST | /api/feeds/{id}/refresh | Yes | — | `FeedResponse` |

---

## 5. Notes
- Existing `feeds.json` data is NOT migrated — users start fresh after signing up
- Connection string: set `ConnectionStrings:Default` in appsettings.json OR `SUPABASE_CONNECTION_STRING` env var
- Supabase PostgreSQL conn string format: `Host=aws-0-us-east-1.pooler.supabase.com;Port=6543;Database=postgres;Username=postgres.xxxxx;Password=xxxxx;SSL Mode=Require`
- Session tokens expire after 30 days
