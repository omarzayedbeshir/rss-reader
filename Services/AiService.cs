using System.Text;
using System.Text.Json;

namespace RssReader.Services;

public class AiService
{
    private readonly HttpClient _http;

    public AiService()
    {
        var apiKey = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY") ?? "";
        _http = new HttpClient { BaseAddress = new Uri("https://api.deepseek.com") };
        _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        _http.DefaultRequestHeaders.Add("User-Agent", "RssReader/1.0");
    }

    public async Task<string> SummarizeAsync(string prompt)
    {
        var body = new
        {
            model = "deepseek-chat",
            messages = new[]
            {
                new { role = "system", content = "You are a helpful assistant that creates daily news digests." },
                new { role = "user", content = prompt }
            },
            max_tokens = 600,
            temperature = 0.3
        };

        var json = JsonSerializer.Serialize(body);
        var response = await _http.PostAsync("/v1/chat/completions",
            new StringContent(json, Encoding.UTF8, "application/json"));

        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"DeepSeek error ({response.StatusCode}): {err}");
        }

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var contentText = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        return contentText?.Trim() ?? "";
    }
}
