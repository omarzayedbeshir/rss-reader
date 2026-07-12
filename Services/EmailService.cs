using System.Text;
using System.Text.Json;

namespace RssReader.Services;

public class EmailService
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _fromEmail;

    public EmailService()
    {
        var apiKey = Environment.GetEnvironmentVariable("RESEND_API_KEY") ?? "";
        _apiKey = apiKey;
        _http = new HttpClient { BaseAddress = new Uri("https://api.resend.com") };
        _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        _http.DefaultRequestHeaders.Add("User-Agent", "RssReader/1.0");
        _fromEmail = Environment.GetEnvironmentVariable("RESEND_FROM_EMAIL") ?? "RSS Reader <onboarding@resend.dev>";
    }

    public async Task SendVerificationEmailAsync(string toEmail, string token, string baseUrl)
    {
        var verifyUrl = $"{baseUrl.TrimEnd('/')}/api/auth/verify-email?token={token}";

        var body = new
        {
            from = _fromEmail,
            to = new[] { toEmail },
            subject = "Verify your email — RSS Reader",
            html = $"<p>Welcome to RSS Reader!</p>" +
                   $"<p>Click the link below to verify your email address:</p>" +
                   $"<p><a href=\"{verifyUrl}\">Verify Email</a></p>" +
                   $"<p>If you did not create this account, you can ignore this email.</p>",
            click_tracking = false
        };

        var json = JsonSerializer.Serialize(body);
        var response = await _http.PostAsync("/emails", new StringContent(json, Encoding.UTF8, "application/json"));
        response.EnsureSuccessStatusCode();
    }
}
