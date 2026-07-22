namespace RssReader.Models;

public record FeedAddRequest(string Url);

public record FeedResponse(
    string Id,
    string Title,
    string FeedUrl,
    string SiteUrl,
    string Description,
    DateTime LastRefreshed,
    List<ArticleResponse> Articles
);

public record ArticleResponse(
    string Id,
    string Title,
    string Url,
    string Summary,
    DateTime Published,
    string FeedId,
    string? EnclosureUrl,
    string? EnclosureType,
    bool IsBookmarked
);

public record SummarizeResponse(string? Digest, string Error);

public record SignUpRequest(string Email, string Password);
public record SignInRequest(string Email, string Password);
public record AuthResponse(string Token, UserResponse User);
public record UserResponse(string Id, string Email, bool EmailVerified, string? Handle);
public record ResendVerificationRequest(string Email);

public record CreatePostRequest(string Title, string Content);
public record UpdatePostRequest(string Title, string Content);
public record PostResponse(string Id, string Title, string Content, DateTime PublishedAt, DateTime UpdatedAt);

public class UserDiscoveryResponse
{
    public string Handle { get; set; } = string.Empty;
    public int SharedFeeds { get; set; }
    public int TotalFeeds { get; set; }
    public int PostCount { get; set; }
}
