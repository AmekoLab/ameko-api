using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using FPTU.Capstone.AMKCollective.Application.DTOs.Settings;
using FPTU.Capstone.AMKCollective.Application.Interfaces.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FPTU.Capstone.AMKCollective.Infrastructure.ThirdParty.AI
{
    public class TavilySearchService : IWebSearchService
    {
        private readonly HttpClient _httpClient;
        private readonly TavilySettings _settings;
        private readonly ILogger<TavilySearchService> _logger;

        public TavilySearchService(
            HttpClient httpClient,
            IOptions<AISettings> options,
            ILogger<TavilySearchService> logger)
        {
            _httpClient = httpClient;
            _settings = options.Value.Tavily;
            _logger = logger;
        }

        public async Task<WebSearchResult> SearchAsync(string query, int maxResults = 5)
        {
            if (string.IsNullOrWhiteSpace(_settings.ApiKey))
            {
                _logger.LogWarning("Tavily API key not configured, skipping web search.");
                return new WebSearchResult();
            }

            try
            {
                // Request more than needed so we have room to filter out articles/tutorials.
                var requestBody = new
                {
                    query,
                    search_depth = "basic",
                    include_images = true,
                    include_image_descriptions = true,
                    max_results = Math.Max(maxResults * 2, 10),
                    topic = "general"
                };

                using var request = new HttpRequestMessage(HttpMethod.Post, _settings.Url);
                request.Headers.Add("Authorization", $"Bearer {_settings.ApiKey}");
                request.Content = JsonContent.Create(requestBody);

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Tavily search failed ({StatusCode}): {Error}", response.StatusCode, error);
                    return new WebSearchResult();
                }

                var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                return ParseResponse(json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Tavily web search exception for query: {Query}", query);
                return new WebSearchResult();
            }
        }

        private static WebSearchResult ParseResponse(JsonElement root)
        {
            var result = new WebSearchResult();

            if (root.TryGetProperty("results", out var resultsEl))
            {
                foreach (var item in resultsEl.EnumerateArray())
                {
                    result.Sources.Add(new WebSearchSource
                    {
                        Title = item.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "",
                        Url = item.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "",
                        Content = item.TryGetProperty("content", out var c) ? c.GetString() ?? "" : "",
                        Score = item.TryGetProperty("score", out var s) && s.ValueKind == JsonValueKind.Number
                            ? s.GetDouble()
                            : 0.0
                    });
                }
            }

            if (root.TryGetProperty("images", out var imagesEl))
            {
                foreach (var img in imagesEl.EnumerateArray())
                {
                    // Tavily images can be a string or an object with { url, description }
                    if (img.ValueKind == JsonValueKind.String)
                    {
                        var url = img.GetString();
                        if (!string.IsNullOrWhiteSpace(url))
                            result.Images.Add(url);
                    }
                    else if (img.ValueKind == JsonValueKind.Object
                             && img.TryGetProperty("url", out var imgUrl))
                    {
                        var url = imgUrl.GetString();
                        if (!string.IsNullOrWhiteSpace(url))
                            result.Images.Add(url);
                    }
                }
            }

            return result;
        }
    }
}
