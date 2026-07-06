using System.Text.Json;
using RssReader.Models;

namespace RssReader.Services;

public class FeedStorageService
{
    private readonly string _filePath;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ReaderWriterLockSlim _lock = new();
    private List<Feed> _feeds = new();

    public FeedStorageService(string filePath)
    {
        _filePath = filePath;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        if (File.Exists(_filePath))
        {
            var json = File.ReadAllText(_filePath);
            _feeds = JsonSerializer.Deserialize<List<Feed>>(json, _jsonOptions) ?? new();
        }
    }

    public List<Feed> GetAllFeeds()
    {
        _lock.EnterReadLock();
        try
        {
            return _feeds.Select(f => CloneFeed(f)).ToList();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public Feed? GetFeed(string id)
    {
        _lock.EnterReadLock();
        try
        {
            var feed = _feeds.FirstOrDefault(f => f.Id == id);
            return feed is not null ? CloneFeed(feed) : null;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public Feed AddFeed(Feed feed)
    {
        _lock.EnterWriteLock();
        try
        {
            _feeds.Add(feed);
            SaveUnsafe();
            return CloneFeed(feed);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public void RemoveFeed(string id)
    {
        _lock.EnterWriteLock();
        try
        {
            _feeds.RemoveAll(f => f.Id == id);
            SaveUnsafe();
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public Feed UpdateFeed(Feed feed)
    {
        _lock.EnterWriteLock();
        try
        {
            var index = _feeds.FindIndex(f => f.Id == feed.Id);
            if (index == -1)
                throw new KeyNotFoundException($"Feed with id {feed.Id} not found.");

            _feeds[index] = feed;
            SaveUnsafe();
            return CloneFeed(feed);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    private void SaveUnsafe()
    {
        var json = JsonSerializer.Serialize(_feeds, _jsonOptions);
        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(_filePath, json);
    }

    private static Feed CloneFeed(Feed feed)
    {
        return new Feed
        {
            Id = feed.Id,
            Title = feed.Title,
            FeedUrl = feed.FeedUrl,
            SiteUrl = feed.SiteUrl,
            Description = feed.Description,
            LastRefreshed = feed.LastRefreshed,
            Articles = feed.Articles.Select(a => new Article
            {
                Id = a.Id,
                Title = a.Title,
                Url = a.Url,
                Summary = a.Summary,
                Published = a.Published,
                FeedId = a.FeedId
            }).ToList()
        };
    }
}
