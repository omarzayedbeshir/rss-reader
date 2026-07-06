# RSS Reader

A lightweight web application for aggregating and reading RSS/Atom feeds. Built with ASP.NET Core Minimal API and vanilla frontend.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

## Run

```bash
dotnet run
```

Open **http://localhost:5090** in your browser.

## API Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| `GET` | `/` | Serves the single-page app |
| `GET` | `/api/feeds` | Returns all feeds and articles (reverse chronological) |
| `POST` | `/api/feeds` | Subscribe to a feed. Body: `{ "url": "https://..." }` |
| `DELETE` | `/api/feeds/{id}` | Remove a feed and its articles |
| `POST` | `/api/feeds/{id}/refresh` | Manually refetch a feed's latest articles |

## How It Works

### Backend (`Program.cs`, `Services/`)

- **Minimal API** — All routes defined in `Program.cs` with no controllers.
- **`FeedStorageService`** — Loads and persists feeds to `Data/feeds.json` as a single JSON file. Thread-safe with `ReaderWriterLockSlim`.
- **`FeedFetchService`** — Fetches and parses RSS/Atom feeds using `System.ServiceModel.Syndication`. Content is sanitized with `HtmlSanitizer` to prevent XSS.

### Frontend (`wwwroot/`)

- **Vanilla JavaScript** — No frameworks. Fetches data from the JSON API, renders articles as cards. Uses `textContent` everywhere to avoid XSS.
- **River of News** — All articles from all feeds displayed in one unified reverse-chronological list. Click a feed in the sidebar to filter by that feed only.
- **Responsive** — Sidebar becomes a horizontal chip list on mobile.

### Data Flow

```
Browser ──GET /──→ index.html (static file)
        ──GET /api/feeds──→ JSON { feeds, articles }
        ──POST /api/feeds──→ validate url → fetch & parse RSS → persist to JSON → return feed
```

## Project Structure

```
RssReader/
├── Program.cs                     # Minimal API endpoints
├── RssReader.csproj               # .NET 10 project
├── Models/
│   ├── Feed.cs                    # Feed + Article models
│   └── ApiModels.cs               # Request/response DTOs
├── Services/
│   ├── FeedStorageService.cs      # JSON file persistence
│   └── FeedFetchService.cs        # RSS/Atom fetching + parsing + sanitization
├── Data/
│   └── feeds.json                 # Subscriptions (auto-created)
└── wwwroot/
    ├── index.html                 # Single-page app
    ├── css/style.css              # Styling
    └── js/app.js                  # Client-side logic
```
