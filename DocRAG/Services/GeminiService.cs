using System.Net.Http.Json;
using System.Text.Json;

namespace DocRAG.Services;

public class GeminiService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1/models/gemini-2.0-flash:generateContent";

    public GeminiService(string apiKey)
    {
        _apiKey = apiKey;
        _httpClient = new HttpClient();
    }

    public async Task<string> GetSummary(string text)
    {
        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new
                        {
                            text = $"Please summarize the following text concisely:\n\n{text}"
                        }
                    }
                }
            },
            generationConfig = new
            {
                temperature = 0.7,
                topP = 1,
                topK = 32,
                maxOutputTokens = 1024
            }
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"{BaseUrl}?key={_apiKey}",
            requestBody
        );

        response.EnsureSuccessStatusCode();
        
        var responseContent = await response.Content.ReadAsStringAsync();
        var jsonDocument = JsonDocument.Parse(responseContent);
        
        return jsonDocument.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString() ?? string.Empty;
    }
}
