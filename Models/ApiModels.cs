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
    string FeedId
);

public record SignUpRequest(string Email, string Password);
public record SignInRequest(string Email, string Password);
public record AuthResponse(string Token, UserResponse User);
public record UserResponse(string Id, string Email, bool EmailVerified);
public record ResendVerificationRequest(string Email);
