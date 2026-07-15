namespace RssReader.Models;

public class Feed
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];
    public string UserId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string FeedUrl { get; set; } = string.Empty;
    public string SiteUrl { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime LastRefreshed { get; set; } = DateTime.UtcNow;
    public List<Article> Articles { get; set; } = new();
}

public class User
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool EmailVerified { get; set; }
    public string VerificationToken { get; set; } = string.Empty;
}

public class Article
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public DateTime Published { get; set; } = DateTime.UtcNow;
    public string FeedId { get; set; } = string.Empty;
    public string EnclosureUrl { get; set; } = string.Empty;
    public string EnclosureType { get; set; } = string.Empty;
}

public class DailyDigest
{
    public string UserId { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
}
