using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using FPTU.Capstone.AMKCollective.Application.DTOs.AI;
using FPTU.Capstone.AMKCollective.Application.DTOs.Part;
using FPTU.Capstone.AMKCollective.Application.DTOs.Settings;
using FPTU.Capstone.AMKCollective.Application.Interfaces.AI;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Domain.Entities;

namespace FPTU.Capstone.AMKCollective.Application.Services
{
    public class AIService : IAIService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEmbeddingService _embeddingService;
        private readonly IQdrantService _qdrantService;
        private readonly HttpClient _httpClient;
        private readonly ILogger<AIService> _logger;
        private readonly AISettings _settings;

        public AIService(
            IUnitOfWork unitOfWork,
            IEmbeddingService embeddingService,
            IQdrantService qdrantService,
            HttpClient httpClient,
            IOptions<AISettings> aiOptions,
            ILogger<AIService> logger)
        {
            _unitOfWork = unitOfWork;
            _embeddingService = embeddingService;
            _qdrantService = qdrantService;
            _httpClient = httpClient;
            _logger = logger;
            _settings = aiOptions.Value;
        }

        public async Task<AIRecommendationResponseDTO> GetRecommendationAsync(AIRecommendationRequestDTO request)
        {
            // 1. Generate Embedding for User Prompt
            var promptVector = await _embeddingService.GenerateEmbeddingAsync(request.UserPrompt);

            // 2. Step 1: Intent Parsing - Did user mention a shop?
            Guid? targetShopId = request.ShopId;
            if (!targetShopId.HasValue)
            {
                // Try to find a shop mentioned in the prompt via semantic search
                var candidateShops = await _qdrantService.SearchAsync("shops", promptVector, limit: 1);
                if (candidateShops.Any())
                {
                    // Note: Here we could add a similarity threshold check
                    targetShopId = candidateShops.First();
                    _logger.LogInformation("Semantic search identified target shop: {ShopId}", targetShopId);
                }
            }

            // 3. Step 2: Retrieve relevant parts (RAG)
            // Filter by ShopId if identified or provided
            var kitIds = await _qdrantService.SearchAsync("parts", promptVector, limit: 5, shopId: targetShopId);
            
            var kits = await _unitOfWork.Models.GetByIdsAsync(kitIds);
            
            // 4. Construct Prompt for LLM
            var contextData = string.Join("\n", kits.Select(k => $"ID: {k.Id}, Name: {k.Name}, Price: {k.Price}, Type: {k.PartType}, Specs: {k.Specifications}"));
            
            var systemPrompt = _settings.RecommendationPrompt;

            var userMessage = $"User Request: {request.UserPrompt}\n\nAvailable Parts Context:\n{contextData}";

            // 5. Call LLM (Groq with OpenAI Fallback)
            string? aiJson = await CallLLMAsync(systemPrompt, userMessage);

            if (string.IsNullOrEmpty(aiJson))
                throw new Exception("AI failed to generate a recommendation.");

            _logger.LogInformation("AI Raw Response: {AiJson}", aiJson);

            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                return JsonSerializer.Deserialize<AIRecommendationResponseDTO>(aiJson, options) 
                       ?? throw new Exception("Failed to parse AI response.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize AI response: {RawJson}", aiJson);
                throw;
            }
        }

        public async Task<IEnumerable<Guid>> SearchShopsAsync(string query, int limit = 10)
        {
            var vector = await _embeddingService.GenerateEmbeddingAsync(query);
            return await _qdrantService.SearchAsync("shops", vector, limit);
        }

        public async Task<IEnumerable<Guid>> SearchBuildsAsync(string query, int limit = 10)
        {
            var vector = await _embeddingService.GenerateEmbeddingAsync(query);
            return await _qdrantService.SearchAsync("builds", vector, limit);
        }

        public async Task SyncAllEntitiesToQdrantAsync()
        {
            _logger.LogInformation("Full sync to Qdrant started.");
            
            // Sync Shops
            var (shops, _) = await _unitOfWork.Shops.GetShopsAsync(null, null, 1, 1000);
            foreach (var shop in shops) await SyncShopAsync(shop);

            // Sync Builds
            var (builds, _) = await _unitOfWork.AssembledProducts.GetAllPagedAsync(1, 1000);
            foreach (var build in builds) await SyncBuildAsync(build);

            // Sync Parts
            var (parts, _) = await _unitOfWork.Models.GetPagedAsync(new GetPartsFilterRequest { PageNumber = 1, PageSize = 1000 });
            foreach (var part in parts) await SyncPartAsync(part);
            
            _logger.LogInformation("Full sync to Qdrant completed.");
        }

        public async Task SyncShopAsync(ShopProfile shop)
        {
            var text = $"{shop.ShopName} {shop.Bio}";
            var vector = await _embeddingService.GenerateEmbeddingAsync(text);
            var payload = new Dictionary<string, object>
            {
                { "type", "shop" },
                { "name", shop.ShopName },
                { "rating", shop.Rating }
            };
            await _qdrantService.UpsertPointAsync("shops", shop.Id, vector, payload);
            
            shop.Embedding = JsonSerializer.Serialize(vector);
            await _unitOfWork.Shops.UpdateAsync(shop);
            await _unitOfWork.CommitAsync();
        }

        public async Task SyncBuildAsync(AssembledProduct build)
        {
            var text = $"{build.Name} {build.Description} {build.Layout} {build.Mounting} {build.PCB} {build.Connection} {build.Battery}";
            var vector = await _embeddingService.GenerateEmbeddingAsync(text);
            var payload = new Dictionary<string, object>
            {
                { "type", "build" },
                { "name", build.Name },
                { "price", (double)build.Price }
            };
            await _qdrantService.UpsertPointAsync("builds", build.Id, vector, payload);

            build.Embedding = JsonSerializer.Serialize(vector);
            await _unitOfWork.AssembledProducts.UpdateAsync(build);
            await _unitOfWork.CommitAsync();
        }

        public async Task SyncPartAsync(Model part)
        {
            var text = $"{part.Name} {part.Description} {part.PartType} {part.Specifications}";
            var vector = await _embeddingService.GenerateEmbeddingAsync(text);
            var payload = new Dictionary<string, object>
            {
                { "type", "part" },
                { "name", part.Name },
                { "shop_id", part.ShopId.ToString() },
                { "part_type", part.PartType ?? "" },
                { "price", (double)part.Price }
            };
            await _qdrantService.UpsertPointAsync("parts", part.Id, vector, payload);

            part.Embedding = JsonSerializer.Serialize(vector);
            await _unitOfWork.Models.UpdateAsync(part);
            await _unitOfWork.CommitAsync();
        }

        public async Task<string?> AnalyzeOrderIssueAsync(OrderIssue issue, Order order)
        {
            var context = $@"
Order ID: {order.Id}
Status: {order.OrderStatus}
Total Amount: {order.TotalAmount}
Items: {string.Join(", ", order.OrderItems.Select(i => i.ProductName + " x" + i.Quantity))}
Issue Type: {issue.Type}
Reason: {issue.Reason}
Description: {issue.Description}
";

            var systemPrompt = _settings.OrderIssuePrompt;

            return await CallLLMAsync(systemPrompt, $"Identify if this request is valid:\n{context}");
        }

        private async Task<string?> CallLLMAsync(string systemPrompt, string userMessage)
        {
            // Try Groq First
            if (!string.IsNullOrEmpty(_settings.GroqApiKey))
            {
                var groqResponse = await PostToOpenAICompatibleApiAsync(_settings.GroqUrl, _settings.GroqApiKey, _settings.GroqModelName, systemPrompt, userMessage);
                if (groqResponse != null) return groqResponse;
            }

            // Fallback to OpenAI
            if (!string.IsNullOrEmpty(_settings.OpenAiApiKey))
            {
                _logger.LogWarning("Groq failed or not configured, falling back to OpenAI.");
                return await PostToOpenAICompatibleApiAsync(_settings.OpenAiUrl, _settings.OpenAiApiKey, _settings.OpenAiModelName, systemPrompt, userMessage);
            }

            return null;
        }

        private async Task<string?> PostToOpenAICompatibleApiAsync(string url, string apiKey, string model, string systemPrompt, string userMessage)
        {
            try
            {
                var requestBody = new
                {
                    model = model,
                    messages = new[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = userMessage }
                    },
                    response_format = new { type = "json_object" },
                    temperature = 0.7
                };

                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Add("Authorization", $"Bearer {apiKey}");
                request.Content = JsonContent.Create(requestBody);

                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<JsonElement>();
                    return result.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
                }
                
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("LLM API error ({Url}): {Error}", url, error);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception calling LLM API ({Url})", url);
            }
            return null;
        }
    }
}
