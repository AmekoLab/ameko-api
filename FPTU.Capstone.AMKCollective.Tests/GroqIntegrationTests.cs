using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using FPTU.Capstone.AMKCollective.Application.DTOs.Settings;
using FPTU.Capstone.AMKCollective.Application.Interfaces.AI;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Application.Services;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Xunit.Abstractions;
using System.Collections.Generic;
using AutoMapper;

namespace FPTU.Capstone.AMKCollective.Tests
{
    /// <summary>
    /// Integration tests that call the REAL Groq API.
    /// These will use actual API keys from config.
    /// </summary>
    public class GroqIntegrationTests
    {
        private readonly ITestOutputHelper _output;

        // Groq API key - get from Environment Variable "GROQ_API_KEY"
        // or set in appsettings.Development.json (which is gitignored)
        private static readonly string GroqApiKey = Environment.GetEnvironmentVariable("GROQ_API_KEY") ?? "YOUR_KEY_HERE";
        private const string GroqUrl = "https://api.groq.com/openai/v1/chat/completions";

        public GroqIntegrationTests(ITestOutputHelper output)
        {
            _output = output;
        }

        // ===================================================================
        // Test 1: Raw HTTP call to Groq - verify connectivity + response
        // ===================================================================

        [Fact]
        public async Task Groq_RawHttp_ReturnsValidResponse()
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {GroqApiKey}");

            var requestBody = new
            {
                model = "llama-3.3-70b-versatile",
                messages = new[]
                {
                    new { role = "system", content = "You are a helpful assistant. Respond in JSON format." },
                    new { role = "user", content = "Say hello in JSON: {\"greeting\": \"...\"}" }
                },
                response_format = new { type = "json_object" },
                temperature = 0.1
            };

            var response = await httpClient.PostAsJsonAsync(GroqUrl, requestBody);
            var responseBody = await response.Content.ReadAsStringAsync();

            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Response: {responseBody}");

            Assert.True(response.IsSuccessStatusCode, $"Groq API returned {response.StatusCode}: {responseBody}");

            // Parse response
            var json = JsonSerializer.Deserialize<JsonElement>(responseBody);
            var content = json.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();

            _output.WriteLine($"AI Content: {content}");
            Assert.NotNull(content);
            Assert.Contains("greeting", content!, StringComparison.OrdinalIgnoreCase);
        }

        // ===================================================================
        // Test 2: AIService.AnalyzeOrderIssueAsync with REAL Groq API
        // ===================================================================

        [Fact]
        public async Task AnalyzeOrderIssueAsync_RealGroq_ReturnsStructuredAnalysis()
        {
            var settings = new AISettings
            {
                GroqApiKey = GroqApiKey,
                GroqUrl = GroqUrl,
                OpenAiApiKey = "", // Don't fallback - test Groq only
            };

            using var httpClient = new HttpClient();
            var service = new AIService(
                new Mock<IUnitOfWork>().Object,
                new Mock<IEmbeddingService>().Object,
                new Mock<IQdrantService>().Object,
                httpClient,
                Options.Create(settings),
                new Mock<ILogger<AIService>>().Object,
                new Mock<IMapper>().Object);

            var order = new Order
            {
                Id = Guid.NewGuid(),
                OrderStatus = OrderStatus.Processing,
                TotalAmount = 3500000m,
                OrderItems = new List<OrderItem>
                {
                    new() { ProductName = "AMK68 Wireless Keyboard - Gasket Mount", Quantity = 1 },
                    new() { ProductName = "Gateron Yellow Pro Switch x70", Quantity = 1 },
                    new() { ProductName = "GMK Olivia Keycap Set", Quantity = 1 }
                }
            };

            var issue = new OrderIssue
            {
                Type = OrderIssueType.CancelRequest,
                Reason = "Changed my mind",
                Description = "I just realized I already have a similar keyboard. I would like to cancel this order before it ships."
            };

            var result = await service.AnalyzeOrderIssueAsync(issue, order);

            _output.WriteLine("=== AI Analysis Result ===");
            _output.WriteLine(result ?? "(null)");

            // Verify we got a response
            Assert.NotNull(result);

            // Verify it's valid JSON
            var parsed = JsonSerializer.Deserialize<JsonElement>(result!);
            _output.WriteLine($"\nParsed JSON successfully.");

            // Verify expected fields exist
            Assert.True(parsed.TryGetProperty("Category", out var category), "Missing 'Category' field");
            Assert.True(parsed.TryGetProperty("Recommendation", out var recommendation), "Missing 'Recommendation' field");
            Assert.True(parsed.TryGetProperty("Summary", out var summary), "Missing 'Summary' field");

            _output.WriteLine($"Category: {category.GetString()}");
            _output.WriteLine($"Recommendation: {recommendation.GetString()}");
            _output.WriteLine($"Summary: {summary.GetString()}");

            // Recommendation should be one of: Approve, Reject, Escalate
            var rec = recommendation.GetString();
            Assert.True(
                rec == "Approve" || rec == "Reject" || rec == "Escalate",
                $"Unexpected recommendation: {rec}");
        }

        // ===================================================================
        // Test 3: Groq handles refund for shipped order (should Reject/Escalate)
        // ===================================================================

        [Fact]
        public async Task AnalyzeOrderIssue_ShippedOrder_ShouldEscalateOrReject()
        {
            var settings = new AISettings
            {
                GroqApiKey = GroqApiKey,
                GroqUrl = GroqUrl,
                OpenAiApiKey = "",
            };

            using var httpClient = new HttpClient();
            var service = new AIService(
                new Mock<IUnitOfWork>().Object,
                new Mock<IEmbeddingService>().Object,
                new Mock<IQdrantService>().Object,
                httpClient,
                Options.Create(settings),
                new Mock<ILogger<AIService>>().Object,
                new Mock<IMapper>().Object);

            var order = new Order
            {
                Id = Guid.NewGuid(),
                OrderStatus = OrderStatus.Shipped, // Already shipped!
                TotalAmount = 5000000m,
                OrderItems = new List<OrderItem>
                {
                    new() { ProductName = "Custom Acrylic Keyboard", Quantity = 1 }
                }
            };

            var issue = new OrderIssue
            {
                Type = OrderIssueType.CancelRequest,
                Reason = "Don't want it anymore",
                Description = "Cancel my order and give me a full refund."
            };

            var result = await service.AnalyzeOrderIssueAsync(issue, order);

            _output.WriteLine("=== Shipped Order Analysis ===");
            _output.WriteLine(result ?? "(null)");

            Assert.NotNull(result);

            var parsed = JsonSerializer.Deserialize<JsonElement>(result!);
            var rec = parsed.GetProperty("Recommendation").GetString();

            _output.WriteLine($"Recommendation for shipped order: {rec}");

            // For a shipped order, AI should NOT approve a simple cancellation
            Assert.True(
                rec == "Reject" || rec == "Escalate",
                $"AI incorrectly approved cancellation of shipped order. Got: {rec}");
        }

        // ===================================================================
        // Test 4: Verify Groq model and response latency
        // ===================================================================

        [Fact]
        public async Task Groq_ResponseLatency_IsAcceptable()
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {GroqApiKey}");

            var requestBody = new
            {
                model = "llama-3.3-70b-versatile",
                messages = new[]
                {
                    new { role = "user", content = "Reply with this exact json: {\"status\":\"ok\"}" }
                },
                response_format = new { type = "json_object" },
                temperature = 0
            };

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var response = await httpClient.PostAsJsonAsync(GroqUrl, requestBody);
            sw.Stop();

            var body = await response.Content.ReadAsStringAsync();
            _output.WriteLine($"Latency: {sw.ElapsedMilliseconds}ms");
            _output.WriteLine($"Status: {response.StatusCode}");
            _output.WriteLine($"Response: {body}");

            Assert.True(response.IsSuccessStatusCode, $"Failed: {body}");

            // Groq should respond within 10 seconds for a simple request
            Assert.True(sw.ElapsedMilliseconds < 10000, $"Groq took too long: {sw.ElapsedMilliseconds}ms");

            // Log the model used
            var json = JsonSerializer.Deserialize<JsonElement>(body);
            var model = json.GetProperty("model").GetString();
            _output.WriteLine($"Model used: {model}");

            var usage = json.GetProperty("usage");
            _output.WriteLine($"Tokens - prompt: {usage.GetProperty("prompt_tokens")}, completion: {usage.GetProperty("completion_tokens")}, total: {usage.GetProperty("total_tokens")}");
        }
    }
}
