using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using FPTU.Capstone.AMKCollective.Application.Contracts.AI;
using FPTU.Capstone.AMKCollective.Application.DTOs.Part;
using FPTU.Capstone.AMKCollective.Application.DTOs.Settings;
using FPTU.Capstone.AMKCollective.Application.Interfaces.AI;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Domain.Entities;

namespace FPTU.Capstone.AMKCollective.Application.Services
{
    public class AIService : IAIService
    {
        private enum RecommendationMode
        {
            PartBundle,
            Assembled
        }

        private enum ResponseLanguage
        {
            English,
            Vietnamese
        }

        // Chat message intent — determines RAG + web search strategy
        private enum MessageIntent
        {
            Greeting,          // chào, hello, xin chào → skip RAG + web search
            WebSearchRequest,  // tìm mẫu, reference, đặt làm riêng → force web search
            KeyboardQuery      // general keyboard question → normal RAG flow
        }

        private readonly IUnitOfWork _unitOfWork;
        private readonly IEmbeddingService _embeddingService;
        private readonly IQdrantService _qdrantService;
        private readonly IWebSearchService? _webSearchService;
        private readonly HttpClient _httpClient;
        private readonly ILogger<AIService> _logger;
        private readonly AISettings _settings;
        private readonly AutoMapper.IMapper _mapper;

        public AIService(
            IUnitOfWork unitOfWork,
            IEmbeddingService embeddingService,
            IQdrantService qdrantService,
            HttpClient httpClient,
            IOptions<AISettings> aiOptions,
            ILogger<AIService> logger,
            AutoMapper.IMapper mapper,
            IWebSearchService? webSearchService = null)
        {
            _unitOfWork = unitOfWork;
            _embeddingService = embeddingService;
            _qdrantService = qdrantService;
            _webSearchService = webSearchService;
            _httpClient = httpClient;
            _logger = logger;
            _settings = aiOptions.Value;
            _mapper = mapper;
        }

        public async Task<AIRecommendationResponseDTO> GetRecommendationAsync(AIRecommendationRequestDTO request)
        {
            // 1. Generate Embedding for User Prompt
            var promptVector = await _embeddingService.GenerateEmbeddingAsync(request.UserPrompt);

            var recommendationMode = DetermineRecommendationMode(request);
            var preferAssembledRecommendation = recommendationMode == RecommendationMode.Assembled;
            var responseLanguage = DetermineResponseLanguage(request.UserPrompt);

            // 2. Step 1: Intent Parsing - Did user mention a shop?
            Guid? targetShopId = request.ShopId;
            if (!targetShopId.HasValue && recommendationMode == RecommendationMode.PartBundle)
            {
                // Try to find a shop mentioned in the prompt via semantic search
                var candidateShops = await SearchQdrantSafelyAsync("shops", promptVector, limit: 1);
                if (candidateShops.Any())
                {
                    // Note: Here we could add a similarity threshold check
                    targetShopId = candidateShops.First();
                    _logger.LogInformation("Semantic search identified target shop: {ShopId}", targetShopId);
                }
            }

            // 3. Step 2: Retrieve relevant candidates (RAG)
            // If caller is in assembled flow, prioritize assembled-product candidates.
            var partCandidates = new List<Model>();
            var assembledCandidates = new List<AssembledProduct>();

            string contextData;
            if (preferAssembledRecommendation)
            {
                var buildIds = await SearchQdrantSafelyAsync("builds", promptVector, limit: 5, shopId: targetShopId);
                if (buildIds.Any())
                {
                    assembledCandidates = (await _unitOfWork.AssembledProducts.GetByIdsAsync(buildIds)).ToList();
                }

                if (!assembledCandidates.Any())
                {
                    // Fallback path when vector search is unavailable or returns empty.
                    assembledCandidates = await LoadFallbackAssembledCandidatesAsync(limit: 5);
                }

                contextData = string.Join(
                    "\n",
                    assembledCandidates.Select(b =>
                        $"ID: {b.Id}, Name: {b.Name}, Price: {b.Price}, Layout: {b.Layout}, Mounting: {b.Mounting}, Connection: {b.Connection}, Description: {b.Description}"));
            }
            else
            {
                // Filter by ShopId if identified or provided
                var partIds = await SearchQdrantSafelyAsync("parts", promptVector, limit: 5, shopId: targetShopId);
                if (partIds.Any())
                {
                    partCandidates = (await _unitOfWork.Models.GetByIdsAsync(partIds)).ToList();
                }

                if (!partCandidates.Any())
                {
                    // Fallback path when vector search is unavailable or returns empty.
                    partCandidates = await LoadFallbackPartCandidatesAsync(targetShopId, limit: 5);
                }

                contextData = string.Join(
                    "\n",
                    partCandidates.Select(k =>
                        $"ID: {k.Id}, Name: {k.Name}, Price: {k.Price}, Type: {k.PartType}, Specs: {k.Specifications}"));
            }

            if (string.IsNullOrWhiteSpace(contextData))
            {
                contextData = "(no candidate data found)";
            }

            // 4. Construct Prompt for LLM
            var languageRule = responseLanguage == ResponseLanguage.Vietnamese
                ? " LANGUAGE RULE: Write the 'Reasoning' field in Vietnamese only."
                : " LANGUAGE RULE: Write the 'Reasoning' field in English only.";

            var systemPrompt =
                _settings.RecommendationPrompt +
                " IMPORTANT OUTPUT RULES: Return ONLY valid JSON. Any id field must be a valid UUID string or null. " +
                "For part recommendation use fields KitId, SwitchId, KeycapId. " +
                "For assembled recommendation use AssembledProductId (or BuildId)." +
                languageRule +
                (preferAssembledRecommendation
                    ? " You are now in assembled-product mode: prioritize returning AssembledProductId."
                    : " You are now in part-bundle mode: prioritize returning KitId/SwitchId/KeycapId.");

            var responseLanguageHint = responseLanguage == ResponseLanguage.Vietnamese ? "Vietnamese" : "English";
            var userMessage =
                $"User Request: {request.UserPrompt}\n" +
                $"Response Language: {responseLanguageHint}\n\n" +
                $"Available Recommendation Context:\n{contextData}";

            // 5. Call LLM (Groq with OpenAI Fallback)
            string? aiJson = await CallLLMAsync(systemPrompt, userMessage);

            if (string.IsNullOrEmpty(aiJson))
                throw new Exception("AI failed to generate a recommendation.");

            _logger.LogInformation("AI Raw Response: {AiJson}", aiJson);

            try
            {
                var recommendation = ParseRecommendationResponse(aiJson);

                // If AI did not explicitly return ids but caller gives assembled context,
                // keep that id as a fallback to avoid returning an empty payload.
                if (!recommendation.AssembledProductId.HasValue
                    && !recommendation.KitId.HasValue
                    && !recommendation.SwitchId.HasValue
                    && !recommendation.KeycapId.HasValue
                    && request.AssembledProductId.HasValue)
                {
                    recommendation.AssembledProductId = request.AssembledProductId;
                }

                if (!recommendation.AssembledProductId.HasValue
                    && preferAssembledRecommendation
                    && assembledCandidates.Count > 0)
                {
                    recommendation.AssembledProductId = assembledCandidates[0].Id;
                }

                recommendation.Items = await BuildRecommendationItemsAsync(recommendation, partCandidates);
                return recommendation;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize AI response: {RawJson}", aiJson);
                throw;
            }
        }

        public async Task<IEnumerable<FPTU.Capstone.AMKCollective.Application.DTOs.Shop.ShopResponse>> SearchShopsAsync(string query, int limit = 10)
        {
            var vector = await _embeddingService.GenerateEmbeddingAsync(query);
            var ids = await SearchQdrantSafelyAsync("shops", vector, limit);
            if (!ids.Any()) return Enumerable.Empty<FPTU.Capstone.AMKCollective.Application.DTOs.Shop.ShopResponse>();
            var shops = await _unitOfWork.Shops.GetByIdsAsync(ids);
            return _mapper.Map<IEnumerable<FPTU.Capstone.AMKCollective.Application.DTOs.Shop.ShopResponse>>(shops);
        }

        public async Task<IEnumerable<FPTU.Capstone.AMKCollective.Application.DTOs.AssembledProduct.AssembledProductResponse>> SearchBuildsAsync(string query, int limit = 10)
        {
            var vector = await _embeddingService.GenerateEmbeddingAsync(query);
            var ids = await SearchQdrantSafelyAsync("builds", vector, limit);
            if (!ids.Any()) return Enumerable.Empty<FPTU.Capstone.AMKCollective.Application.DTOs.AssembledProduct.AssembledProductResponse>();
            var builds = await _unitOfWork.AssembledProducts.GetByIdsAsync(ids);
            return _mapper.Map<IEnumerable<FPTU.Capstone.AMKCollective.Application.DTOs.AssembledProduct.AssembledProductResponse>>(builds);
        }

        private async Task<List<Guid>> SearchQdrantSafelyAsync(string collectionName, float[] vector, int limit, Guid? shopId = null, float? scoreThreshold = null)
        {
            try
            {
                return await _qdrantService.SearchAsync(collectionName, vector, limit, shopId, scoreThreshold);
            }
            catch (Exception ex) when (IsQdrantAccessOrAvailabilityIssue(ex))
            {
                _logger.LogWarning(
                    ex,
                    "Qdrant search failed for collection {Collection}. Falling back to DB candidates.",
                    collectionName);
                return new List<Guid>();
            }
        }

        private async Task<List<Model>> LoadFallbackPartCandidatesAsync(Guid? shopId, int limit)
        {
            var filter = new GetPartsFilterRequest
            {
                ShopId = shopId,
                IsActive = true,
                PageNumber = 1,
                PageSize = limit
            };

            var (items, _) = await _unitOfWork.Models.GetPagedAsync(filter);
            var candidates = (items ?? Enumerable.Empty<Model>()).ToList();

            // If shop-constrained fallback returns nothing, relax the shop filter.
            if (!candidates.Any() && shopId.HasValue)
            {
                filter.ShopId = null;
                var (relaxedItems, _) = await _unitOfWork.Models.GetPagedAsync(filter);
                candidates = (relaxedItems ?? Enumerable.Empty<Model>()).ToList();
            }

            return candidates;
        }

        private async Task<List<AssembledProduct>> LoadFallbackAssembledCandidatesAsync(int limit)
        {
            var (items, _) = await _unitOfWork.AssembledProducts.GetAllPagedAsync(1, limit);
            return items.Take(limit).ToList();
        }

        private static bool IsQdrantAccessOrAvailabilityIssue(Exception ex)
        {
            var raw = ex.ToString();
            return raw.Contains("PermissionDenied", StringComparison.OrdinalIgnoreCase)
                || raw.Contains("Unauthenticated", StringComparison.OrdinalIgnoreCase)
                || raw.Contains("HTTP status code: 401", StringComparison.OrdinalIgnoreCase)
                || raw.Contains("HTTP status code: 403", StringComparison.OrdinalIgnoreCase)
                || raw.Contains("Unavailable", StringComparison.OrdinalIgnoreCase)
                || raw.Contains("DeadlineExceeded", StringComparison.OrdinalIgnoreCase)
                || raw.Contains("Connection refused", StringComparison.OrdinalIgnoreCase);
        }

        public async Task SyncAllEntitiesToQdrantAsync()
        {
            _logger.LogInformation("Full sync to Qdrant started.");
            
            // Detect correct dimension from a sample embedding
            var sampleVector = await _embeddingService.GenerateEmbeddingAsync("dimension_check");
            ulong dimension = (ulong)sampleVector.Length;
            _logger.LogInformation("Detected embedding dimension: {Dimension}", dimension);

            // Force recreate collections to handle dimension mismatch
            await _qdrantService.DeleteCollectionAsync("shops");
            await _qdrantService.DeleteCollectionAsync("builds");
            await _qdrantService.DeleteCollectionAsync("parts");

            await _qdrantService.EnsureCollectionExistsAsync("shops", dimension);
            await _qdrantService.EnsureCollectionExistsAsync("builds", dimension);
            await _qdrantService.EnsureCollectionExistsAsync("parts", dimension);
            
            // Sync Shops
            var (shops, _) = await _unitOfWork.Shops.GetShopsAsync(null, null, 1, 1000);
            foreach (var shop in shops) await SyncShopAsync(shop);
            
            // Clear tracker to avoid "already being tracked" conflicts for navigation properties
            _unitOfWork.ClearChangeTracker();

            // Sync Builds
            var (builds, _) = await _unitOfWork.AssembledProducts.GetAllPagedAsync(1, 1000);
            foreach (var build in builds) await SyncBuildAsync(build);
            
            _unitOfWork.ClearChangeTracker();

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
            
            var serializedEmbedding = JsonSerializer.Serialize(vector);
            shop.Embedding = serializedEmbedding;
            
            // Fix: Use scalar ExecuteUpdateAsync to update ONLY the Embedding column
            // avoiding navigation property duplicate tracking issues.
            await _unitOfWork.Shops.UpdateEmbeddingAsync(shop.Id, serializedEmbedding);
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

            // Fix: Use scalar ExecuteUpdateAsync to update ONLY the Embedding column.
            // Previously this method set ProductAssembledDetails = null! on the tracked entity,
            // then called CommitAsync() — causing EF Core to cascade-delete all child detail rows.
            var serializedEmbedding = JsonSerializer.Serialize(vector);
            build.Embedding = serializedEmbedding;
            await _unitOfWork.AssembledProducts.UpdateEmbeddingAsync(build.Id, serializedEmbedding);
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

            var serializedEmbedding = JsonSerializer.Serialize(vector);
            part.Embedding = serializedEmbedding;

            // Use dedicated scalar update to avoid attaching navigation graph
            // (which can cause duplicate tracking conflicts during sync-all).
            await _unitOfWork.Models.UpdateEmbeddingAsync(part.Id, serializedEmbedding);
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
            var rawResponse = await CallLLMAsync(systemPrompt, $"Analyze this support request:\n{context}");
            
            if (string.IsNullOrEmpty(rawResponse))
            {
                _logger.LogWarning("AI service returned empty response for order issue analysis.");
                return null;
            }

            // Validate and normalize the response to ensure correct field names
            var normalizedResponse = NormalizeOrderIssueAnalysisResponse(rawResponse);
            return normalizedResponse;
        }

        private string? NormalizeOrderIssueAnalysisResponse(string rawResponse)
        {
            try
            {
                // Try to parse the response
                using var doc = JsonDocument.Parse(rawResponse);
                var root = doc.RootElement;

                // Extract fields with case-insensitive key matching
                var category = ReadStringCaseInsensitive(root, "Category") ?? "Unknown";
                var sentiment = ReadStringCaseInsensitive(root, "Sentiment") ?? "Neutral";
                var summary = ReadStringCaseInsensitive(root, "Summary") ?? "No summary provided";
                var recommendation = ReadStringCaseInsensitive(root, "Recommendation") ?? "Escalate";
                var confidenceScore = ReadDecimalCaseInsensitive(root, "ConfidenceScore") ?? 0.5m;

                // Normalize field names to match FE expectations (PascalCase)
                var normalized = new
                {
                    Category = category,
                    Sentiment = sentiment,
                    Summary = summary,
                    Recommendation = recommendation,
                    ConfidenceScore = Math.Round(confidenceScore, 2)
                };

                return JsonSerializer.Serialize(normalized);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse AI response as JSON. Raw response: {Response}", rawResponse);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error normalizing AI response: {Response}", rawResponse);
                return null;
            }
        }

        private static string? ReadStringCaseInsensitive(JsonElement element, string fieldName)
        {
            if (element.TryGetProperty(fieldName, out var prop)) 
                return prop.GetString();
            
            // Try lowercase
            var lowerFieldName = fieldName.ToLower();
            foreach (var field in element.EnumerateObject())
            {
                if (field.Name.Equals(lowerFieldName, StringComparison.OrdinalIgnoreCase))
                    return field.Value.GetString();
            }
            
            return null;
        }

        private static decimal? ReadDecimalCaseInsensitive(JsonElement element, string fieldName)
        {
            if (element.TryGetProperty(fieldName, out var prop))
            {
                if (prop.TryGetDecimal(out var value)) return value;
                if (prop.TryGetDouble(out var doubleValue)) return (decimal)doubleValue;
            }
            
            // Try lowercase
            var lowerFieldName = fieldName.ToLower();
            foreach (var field in element.EnumerateObject())
            {
                if (field.Name.Equals(lowerFieldName, StringComparison.OrdinalIgnoreCase))
                {
                    if (field.Value.TryGetDecimal(out var value)) return value;
                    if (field.Value.TryGetDouble(out var doubleValue)) return (decimal)doubleValue;
                }
            }
            
            return null;
        }

        private async Task<string?> CallLLMAsync(string systemPrompt, string userMessage)
        {
            var messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userMessage }
            };
            return await CallLLMWithMessagesAsync(messages);
        }

        // Gọi LLM với toàn bộ lịch sử hội thoại — dùng cho chatbot multi-turn.
        // history: danh sách (role, content) theo thứ tự cũ → mới, không bao gồm system prompt.
        // ragUserMessage: câu hỏi hiện tại của user đã được ghép với RAG context.
        private async Task<string?> CallLLMWithHistoryAsync(
            string systemPrompt,
            IEnumerable<(string role, string content)> history,
            string ragUserMessage)
        {
            var messages = new List<object>
            {
                new { role = "system", content = systemPrompt }
            };

            foreach (var (role, content) in history)
                messages.Add(new { role, content });

            messages.Add(new { role = "user", content = ragUserMessage });

            return await CallLLMWithMessagesAsync(messages.ToArray());
        }

        private async Task<string?> CallLLMWithMessagesAsync(object[] messages)
        {
            // OpenAI primary — better instruction following for complex prompts
            if (!string.IsNullOrEmpty(_settings.OpenAiApiKey))
            {
                var openAiResponse = await PostMessagesToLLMAsync(_settings.OpenAiUrl, _settings.OpenAiApiKey, _settings.OpenAiModelName, messages);
                if (openAiResponse != null) return openAiResponse;
            }

            // Groq fallback
            if (!string.IsNullOrEmpty(_settings.GroqApiKey))
            {
                _logger.LogWarning("OpenAI failed or not configured, falling back to Groq.");
                return await PostMessagesToLLMAsync(_settings.GroqUrl, _settings.GroqApiKey, _settings.GroqModelName, messages);
            }

            return null;
        }

        private async Task<string?> PostToOpenAICompatibleApiAsync(string url, string apiKey, string model, string systemPrompt, string userMessage)
        {
            var messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userMessage }
            };
            return await PostMessagesToLLMAsync(url, apiKey, model, messages);
        }

        private async Task<string?> PostMessagesToLLMAsync(string url, string apiKey, string model, object[] messages)
        {
            try
            {
                var requestBody = new
                {
                    model,
                    messages,
                    response_format = new { type = "json_object" },
                    temperature = 0.1
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

        private static AIRecommendationResponseDTO ParseRecommendationResponse(string aiJson)
        {
            using var doc = JsonDocument.Parse(aiJson);
            var root = doc.RootElement;

            return new AIRecommendationResponseDTO
            {
                KitId = ReadNullableGuid(root, "KitId"),
                SwitchId = ReadNullableGuid(root, "SwitchId"),
                KeycapId = ReadNullableGuid(root, "KeycapId"),
                AssembledProductId = ReadNullableGuid(root, "AssembledProductId")
                                     ?? ReadNullableGuid(root, "BuildId"),
                Reasoning = ReadString(root, "Reasoning") ?? string.Empty,
                TotalEstimatedPrice = ReadDecimal(root, "TotalEstimatedPrice") ?? 0m
            };
        }

        private async Task<List<AIRecommendationItemDTO>> BuildRecommendationItemsAsync(
            AIRecommendationResponseDTO recommendation,
            IEnumerable<Model> candidateParts)
        {
            var items = new List<AIRecommendationItemDTO>();
            candidateParts ??= Enumerable.Empty<Model>();

            // Build part cards from selected ids.
            var selectedPartIds = new[]
            {
                recommendation.KitId,
                recommendation.SwitchId,
                recommendation.KeycapId
            }
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Distinct()
            .ToList();

            var partById = candidateParts
                .Where(x => x != null)
                .ToDictionary(x => x.Id, x => x);
            var missingPartIds = selectedPartIds.Where(id => !partById.ContainsKey(id)).ToList();

            if (missingPartIds.Count > 0)
            {
                var fetched = await _unitOfWork.Models.GetByIdsAsync(missingPartIds);
                foreach (var model in fetched ?? Enumerable.Empty<Model>())
                {
                    if (model == null)
                    {
                        continue;
                    }

                    partById[model.Id] = model;
                }
            }

            var missingPartShopIds = partById.Values
                .Where(model => model.Shop == null)
                .Select(model => model.ShopId)
                .Distinct()
                .ToList();

            var shopsById = new Dictionary<Guid, ShopProfile>();
            if (missingPartShopIds.Count > 0 && _unitOfWork.Shops != null)
            {
                var fetchedShops = await _unitOfWork.Shops.GetByIdsAsync(missingPartShopIds);
                shopsById = (fetchedShops ?? Enumerable.Empty<ShopProfile>())
                    .Where(shop => shop != null)
                    .ToDictionary(shop => shop.Id, shop => shop);
            }

            foreach (var id in selectedPartIds)
            {
                if (!partById.TryGetValue(id, out var model))
                {
                    continue;
                }

                items.Add(MapPartItem(model, shopsById));
            }

            // Build assembled-product card if returned by AI.
            if (recommendation.AssembledProductId.HasValue)
            {
                var assembled = await _unitOfWork.AssembledProducts.GetByIdWithDetailsAsync(recommendation.AssembledProductId.Value);
                if (assembled != null && !assembled.IsDeleted)
                {
                    items.Insert(0, await MapAssembledItemAsync(assembled));
                }
            }

            return items;
        }

        // Builds item cards directly from pre-loaded fallback candidates (no AI IDs needed).
        // Used in web search mode where AI returns null IDs but we still want platform products shown.
        private async Task<List<AIRecommendationItemDTO>> BuildFallbackItemsAsync(
            List<Model>? partCandidates,
            List<AssembledProduct>? assembledCandidates)
        {
            var items = new List<AIRecommendationItemDTO>();

            if (assembledCandidates != null)
            {
                foreach (var assembled in assembledCandidates.Take(5))
                    items.Add(await MapAssembledItemAsync(assembled));
            }

            if (partCandidates != null && partCandidates.Count > 0)
            {
                var shopIds = partCandidates
                    .Where(p => p.Shop == null)
                    .Select(p => p.ShopId)
                    .Distinct()
                    .ToList();

                var shopsById = new Dictionary<Guid, ShopProfile>();
                if (shopIds.Count > 0 && _unitOfWork.Shops != null)
                {
                    var fetched = await _unitOfWork.Shops.GetByIdsAsync(shopIds);
                    shopsById = (fetched ?? Enumerable.Empty<ShopProfile>())
                        .Where(s => s != null)
                        .ToDictionary(s => s.Id, s => s);
                }

                foreach (var part in partCandidates.Take(5))
                    items.Add(MapPartItem(part, shopsById));
            }

            return items;
        }

        private static AIRecommendationItemDTO MapPartItem(Model model, IReadOnlyDictionary<Guid, ShopProfile> fallbackShops)
        {
            fallbackShops.TryGetValue(model.ShopId, out var fallbackShop);
            var shop = model.Shop ?? fallbackShop;

            return new AIRecommendationItemDTO
            {
                Id = model.Id,
                RecommendationKind = "part",
                Name = model.Name,
                Price = model.Price,
                ImageUrl = FirstNonEmpty(model.ThumbnailURL, model.DefaultLayerImageUrl),
                DetailPath = $"/shop/product/{model.Id}",
                ShopId = model.ShopId,
                ShopName = shop?.ShopName,
                ShopAvatarUrl = shop?.LogoUrl
            };
        }

        private async Task<AIRecommendationItemDTO> MapAssembledItemAsync(AssembledProduct assembled)
        {
            var shop = assembled.ProductAssembledDetails?
                .Select(d => d.BaseKit?.Shop)
                .FirstOrDefault(s => s != null)
                ?? assembled.ProductAssembledDetails?
                    .Select(d => d.Component?.Shop)
                    .FirstOrDefault(s => s != null);

            if (shop == null && assembled.CreatedBy.HasValue)
            {
                shop = await _unitOfWork.Shops.GetByUserIdAsync(assembled.CreatedBy.Value);
            }

            return new AIRecommendationItemDTO
            {
                Id = assembled.Id,
                RecommendationKind = "assembled",
                Name = assembled.Name,
                Price = assembled.Price,
                ImageUrl = FirstNonEmpty(assembled.Image1, assembled.Image2, assembled.Image3),
                DetailPath = $"/shop/assembled-product/{assembled.Id}",
                ShopId = shop?.Id,
                ShopName = shop?.ShopName,
                ShopAvatarUrl = shop?.LogoUrl
            };
        }

        private static string? FirstNonEmpty(params string?[] values)
        {
            return values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
        }

        private static Guid? ReadNullableGuid(JsonElement root, string propertyName)
        {
            if (!TryGetPropertyIgnoreCase(root, propertyName, out var value))
            {
                return null;
            }

            if (value.ValueKind == JsonValueKind.Null || value.ValueKind == JsonValueKind.Undefined)
            {
                return null;
            }

            if (value.ValueKind == JsonValueKind.String)
            {
                var raw = value.GetString();
                return Guid.TryParse(raw, out var guid) ? guid : null;
            }

            return null;
        }

        private static decimal? ReadDecimal(JsonElement root, string propertyName)
        {
            if (!TryGetPropertyIgnoreCase(root, propertyName, out var value))
            {
                return null;
            }

            if (value.ValueKind == JsonValueKind.Null || value.ValueKind == JsonValueKind.Undefined)
            {
                return null;
            }

            if (value.ValueKind == JsonValueKind.Number)
            {
                if (value.TryGetDecimal(out var decimalValue))
                {
                    return decimalValue;
                }

                if (value.TryGetDouble(out var doubleValue))
                {
                    return Convert.ToDecimal(doubleValue, CultureInfo.InvariantCulture);
                }

                return null;
            }

            if (value.ValueKind == JsonValueKind.String)
            {
                var raw = value.GetString();
                if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                {
                    return parsed;
                }
            }

            return null;
        }

        private static string? ReadString(JsonElement root, string propertyName)
        {
            if (!TryGetPropertyIgnoreCase(root, propertyName, out var value))
            {
                return null;
            }

            return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        }

        private static bool TryGetPropertyIgnoreCase(JsonElement root, string propertyName, out JsonElement value)
        {
            foreach (var property in root.EnumerateObject())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }

            value = default;
            return false;
        }

        private static RecommendationMode DetermineRecommendationMode(AIRecommendationRequestDTO request)
        {
            // Explicit context has highest priority.
            if (request.AssembledProductId.HasValue)
            {
                return RecommendationMode.Assembled;
            }

            if (request.BaseKitId.HasValue)
            {
                return RecommendationMode.PartBundle;
            }

            // Keep backward compatibility for existing FE flows already sending ShopId
            // to request part-level recommendations.
            if (request.ShopId.HasValue)
            {
                return RecommendationMode.PartBundle;
            }

            // Default behavior for generic users: assembled recommendation first.
            return IsAdvancedCustomPrompt(request.UserPrompt)
                ? RecommendationMode.PartBundle
                : RecommendationMode.Assembled;
        }

        private static bool IsAdvancedCustomPrompt(string? prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt))
            {
                return false;
            }

            var normalized = prompt.Trim().ToLowerInvariant();

            // Expert/custom cues. If none match, we consider the user general and
            // prioritize assembled products for a faster purchase path.
            string[] customKeywords =
            {
                "custom",
                "kit",
                "switch",
                "keycap",
                "stabilizer",
                "lube",
                "mod",
                "mods",
                "barebone",
                "barebones",
                "hot swap",
                "hot-swap",
                "gasket",
                "pcb",
                "plate",
                "linear",
                "tactile",
                "clicky",
                "profile",
                "stem",
                "spring",
                "builder",
                "build my",
                "tu build",
                "tu lap",
                "linh kien",
                "do phim",
                "switches"
            };

            return customKeywords.Any(k => normalized.Contains(k, StringComparison.Ordinal));
        }

        // Dùng cho chat flow — chỉ true khi user nói RÕ RÀNG muốn part/kit/component
        // (không phải bàn phím assembled hoàn chỉnh).
        // Default assembled trừ khi user thật sự hỏi linh kiện riêng.
        private static bool IsExplicitBuilderIntent(string? message)
        {
            if (string.IsNullOrWhiteSpace(message)) return false;

            var m = message.Trim().ToLowerInvariant();

            string[] builderPhrases =
            {
                // Build/lắp ráp intent
                "tự build", "tu build", "tự lắp", "tu lap",
                "mua linh kiện", "mua linh kien",
                "linh kiện riêng", "linh kien rieng",
                "build riêng", "build tay",
                "custom kit", "barebone",
                "build my own", "build my keyboard",
                "want to build", "muốn build", "muon build",
                "tự ráp", "tu rap", "ráp máy",

                // User hỏi rõ ràng về parts/components (kit, switch, keycap)
                "kit nào", "kit nao", "kit gì", "kit gi",
                "có kit", "co kit", "có bộ kit", "co bo kit",
                "switch nào", "switch nao", "switch gì", "switch gi",
                "có switch", "co switch",
                "keycap nào", "keycap nao", "keycap gì", "keycap gi",
                "có keycap", "co keycap",
                "linh kiện nào", "linh kien nao",
                "chọn switch", "chon switch",
                "chọn keycap", "chon keycap",
                "what kit", "which kit", "any kit",
                "what switch", "which switch",
            };

            return builderPhrases.Any(k => m.Contains(k));
        }

        // Detect cụ thể user đang hỏi loại part nào để filter Qdrant results.
        // Trả về null nếu user không nói rõ → giữ tất cả parts.
        private static string? DetectPreferredPartType(string? message)
        {
            if (string.IsNullOrWhiteSpace(message)) return null;
            var m = message.ToLowerInvariant();

            if (m.Contains("kit")) return "Kit";
            if (m.Contains("switch")) return "Switch";
            if (m.Contains("keycap")) return "Keycap";
            return null;
        }

        // ════════════════════════════════════════════════════════════════
        // AI CHATBOT — stateful multi-turn
        // ════════════════════════════════════════════════════════════════

        public async Task<AIChatResponseDTO> ChatAsync(Guid userId, AIChatRequestDTO request)
        {
            // 1. Load hoặc tạo mới conversation
            AIChatConversation conversation;
            bool isNew = false;

            if (request.ConversationId.HasValue)
            {
                conversation = await _unitOfWork.AIChat.GetConversationByIdAsync(request.ConversationId.Value, userId)
                    ?? throw new KeyNotFoundException($"Conversation {request.ConversationId} not found.");
            }
            else
            {
                conversation = new AIChatConversation { UserId = userId };
                await _unitOfWork.AIChat.AddConversationAsync(conversation);
                await _unitOfWork.CommitAsync(); // flush để có Id trước khi add message
                isNew = true;
            }

            // 2. Load lịch sử N tin gần nhất TRƯỚC khi lưu tin mới (để không tính tin hiện tại vào history)
            var recentHistory = isNew
                ? new List<AIChatMessage>()
                : (await _unitOfWork.AIChat.GetRecentMessagesAsync(conversation.Id, limit: 10)).ToList();

            // Extract IDs sản phẩm đã hiển thị trong các assistant messages trước.
            // Khi user hỏi alternatives, ta sẽ exclude những ID này khỏi candidates → AI không repeat.
            var shownProductIds = ExtractShownProductIds(recentHistory);
            bool wantsAlternatives = !isNew
                && IsAskingForAlternatives(request.Message)
                && recentHistory.Any(m => m.Role == "assistant");

            // 3. Lưu user message
            var userMsg = new AIChatMessage
            {
                ConversationId = conversation.Id,
                Role = "user",
                Content = request.Message
            };
            await _unitOfWork.AIChat.AddMessageAsync(userMsg);
            await _unitOfWork.CommitAsync();

            // 4. Classify message intent before doing any expensive work
            var messageIntent = ClassifyMessageIntent(request.Message);

            var partCandidates = new List<Model>();
            var assembledCandidates = new List<AssembledProduct>();
            string contextData;
            bool qdrantFoundResults = false;
            var sourceLinks = new List<WebSearchSourceLink>();
            bool usedWebSearch = false;
            bool preferAssembled = false;
            bool autoWebSearch = false;
            CustomBuildContext? customBuildContext = null;

            if (messageIntent == MessageIntent.Greeting)
            {
                // Skip RAG and web search entirely for greetings
                contextData = string.Empty;
            }
            else
            {
                // 4a. Embedding + Qdrant search — only for keyboard-related queries
                // Use conversation-aware query so follow-up messages ("cái đó", "có gì khác không")
                // produce meaningful embeddings instead of embedding a context-free short phrase.
                var qdrantQuery = BuildEffectiveQdrantQuery(request.Message, recentHistory);
                var promptVector = await _embeddingService.GenerateEmbeddingAsync(qdrantQuery);
                // Chat dùng mode selection riêng — conservative hơn recommendation endpoint.
                // Chỉ PartBundle khi user nói rõ muốn tự build, không phải vì mention "switch" hay "gasket".
                preferAssembled = !IsExplicitBuilderIntent(request.Message);

                // Cosine similarity threshold: below 0.65 = not relevant enough → consider web search
                const float chatScoreThreshold = 0.65f;
                bool forceWebSearch = messageIntent == MessageIntent.WebSearchRequest;

                if (preferAssembled)
                {
                    // Tăng limit lên 10 để có room re-rank theo shop reputation
                    var buildIds = await SearchQdrantSafelyAsync("builds", promptVector, limit: 10, scoreThreshold: chatScoreThreshold);
                    qdrantFoundResults = buildIds.Any();
                    Dictionary<Guid, ShopProfile?> assembledShops = new();

                    if (qdrantFoundResults)
                    {
                        var fetched = (await _unitOfWork.AssembledProducts.GetByIdsAsync(buildIds)).ToList();
                        // Preserve Qdrant order
                        var orderMap = buildIds.Select((id, idx) => (id, idx)).ToDictionary(x => x.id, x => x.idx);
                        assembledCandidates = fetched.OrderBy(c => orderMap.TryGetValue(c.Id, out var i) ? i : int.MaxValue).ToList();

                        // Load shops cho từng assembled (qua CreatedBy)
                        foreach (var ap in assembledCandidates)
                        {
                            ShopProfile? shop = null;
                            if (ap.CreatedBy.HasValue)
                            {
                                try { shop = await _unitOfWork.Shops.GetByUserIdAsync(ap.CreatedBy.Value); }
                                catch { shop = null; }
                            }
                            assembledShops[ap.Id] = shop;
                        }

                        // Re-rank: composite score = 60% Qdrant position + 40% shop reputation
                        // KHÔNG hard-filter shop quality — new shops cũng cần được tiếp cận user.
                        // Soft signal: shop quality thấp → rank thấp, nhưng vẫn xuất hiện.
                        var ranked = assembledCandidates
                            .Select((p, idx) => new { Product = p, Shop = assembledShops[p.Id], Position = idx })
                            .OrderByDescending(x => ComputeRankScore(x.Position, assembledCandidates.Count, x.Shop))
                            .Take(5)
                            .Select(x => x.Product)
                            .ToList();
                        assembledCandidates = ranked;
                    }

                    // PRE-VALIDATION: lọc bỏ candidates rõ ràng không phù hợp TRƯỚC khi gửi LLM.
                    // Ý đồ kiến trúc: KHÔNG bao giờ gửi sản phẩm out-of-budget/wrong-layout cho LLM,
                    // vì LLM sẽ bịa lý do reject hoặc spin context để fit. Filter ở code mới deterministic.
                    if (qdrantFoundResults && assembledCandidates.Any())
                    {
                        var constraints = ExtractUserConstraints(request.Message);
                        var validCandidates = assembledCandidates
                            .Where(p => !IsLayoutMismatch(request.Message, p.Layout))
                            .Where(p => p.Price >= 200_000) // dưới 200k = test data
                            .Where(p => !constraints.BudgetMin.HasValue || p.Price >= constraints.BudgetMin.Value)
                            .Where(p => !constraints.BudgetMax.HasValue || p.Price <= constraints.BudgetMax.Value)
                            .Where(p => !HasSwitchTypeRequirement(request.Message) || ProductHasSwitchInfo(p.Description))
                            .Where(p => !wantsAlternatives || !shownProductIds.Contains(p.Id)) // exclude đã shown khi user xin khác
                            .ToList();

                        if (wantsAlternatives && shownProductIds.Count > 0)
                        {
                            _logger.LogInformation(
                                "User asked alternatives. Excluded {Count} previously-shown product IDs.",
                                shownProductIds.Count);
                        }

                        if (constraints.HasBudget)
                        {
                            _logger.LogInformation(
                                "User budget detected: {Min} - {Max} VND. Filtered {Before} → {After} candidates.",
                                constraints.BudgetMin, constraints.BudgetMax,
                                assembledCandidates.Count, validCandidates.Count);
                        }

                        if (validCandidates.Any())
                        {
                            assembledCandidates = validCandidates;
                        }
                        else
                        {
                            _logger.LogInformation(
                                "Pre-validation removed all {Count} Qdrant candidates — treating as no-match for downstream logic.",
                                assembledCandidates.Count);
                            qdrantFoundResults = false;
                            assembledCandidates.Clear();
                        }
                    }

                    // Pool expansion: nếu Qdrant + filter trả về ÍT HƠN 3 candidates,
                    // expand pool bằng broad DB scan để user luôn có ≥3 lựa chọn (nếu DB còn).
                    // Lý do: Qdrant similarity threshold có thể loại bỏ products tương đối phù hợp
                    // chỉ vì description không khớp ngữ nghĩa cao — nhưng user vẫn cần variety.
                    const int targetPoolSize = 3;
                    if (assembledCandidates.Count < targetPoolSize)
                    {
                        var existingIds = assembledCandidates.Select(c => c.Id).ToHashSet();
                        var broadBatch = await LoadFallbackAssembledCandidatesAsync(limit: 30);
                        var expandConstraints = ExtractUserConstraints(request.Message);
                        var additional = broadBatch
                            .Where(p => !existingIds.Contains(p.Id))
                            .Where(p => p.Price >= 200_000)
                            .Where(p => !IsLayoutMismatch(request.Message, p.Layout))
                            .Where(p => !expandConstraints.BudgetMin.HasValue || p.Price >= expandConstraints.BudgetMin.Value)
                            .Where(p => !expandConstraints.BudgetMax.HasValue || p.Price <= expandConstraints.BudgetMax.Value)
                            .Where(p => !HasSwitchTypeRequirement(request.Message) || ProductHasSwitchInfo(p.Description))
                            .Where(p => !wantsAlternatives || !shownProductIds.Contains(p.Id))
                            .Take(5 - assembledCandidates.Count)
                            .ToList();

                        if (additional.Any())
                        {
                            foreach (var ap in additional)
                            {
                                if (!assembledShops.ContainsKey(ap.Id))
                                {
                                    ShopProfile? shop = null;
                                    if (ap.CreatedBy.HasValue)
                                    {
                                        try { shop = await _unitOfWork.Shops.GetByUserIdAsync(ap.CreatedBy.Value); }
                                        catch { shop = null; }
                                    }
                                    assembledShops[ap.Id] = shop;
                                }
                            }
                            assembledCandidates.AddRange(additional);
                            // Pool đã có ít nhất 1 sản phẩm hợp lệ → treat as Qdrant hit để LLM recommend
                            if (assembledCandidates.Any()) qdrantFoundResults = true;
                            _logger.LogInformation(
                                "Pool expansion: {Before} (Qdrant) + {Add} (DB scan) = {After} candidates. (alternatives={Alts}, shown={ShownCount})",
                                existingIds.Count, additional.Count, assembledCandidates.Count, wantsAlternatives, shownProductIds.Count);
                        }
                        else if (!assembledCandidates.Any())
                        {
                            _logger.LogInformation(
                                "Pool exhausted: 0 candidates from Qdrant + 0 from broad DB scan ({Total} broad). POOL EXHAUSTED path.",
                                broadBatch.Count);
                        }
                    }

                    var header = qdrantFoundResults
                        ? "--- CANDIDATE ASSEMBLED KEYBOARDS (semantically close, ranked by shop reputation — you must verify they actually fit the user's requirements) ---"
                        : "--- PLATFORM ASSEMBLED KEYBOARDS (no semantic match — for general reference only, do NOT recommend unless genuinely relevant) ---";
                    contextData = header + "\n" + string.Join("\n", assembledCandidates.Select(b =>
                    {
                        var shopInfo = assembledShops.TryGetValue(b.Id, out var s) ? FormatShopInfo(s) : "Shop: (unknown)";
                        return $"ID: {b.Id}, Name: {b.Name}, Price: {b.Price} VND, Layout: {b.Layout}, Mounting: {b.Mounting}, Connection: {b.Connection}, {shopInfo}, Description: {b.Description}";
                    }));
                }
                else
                {
                    // Tăng limit để có room ranking + filter theo type
                    var partIds = await SearchQdrantSafelyAsync("parts", promptVector, limit: 10, scoreThreshold: chatScoreThreshold);
                    qdrantFoundResults = partIds.Any();
                    if (qdrantFoundResults)
                        partCandidates = (await _unitOfWork.Models.GetByIdsAsync(partIds)).ToList();

                    // Filter theo type user hỏi (kit/switch/keycap) nếu rõ ràng
                    var preferredType = DetectPreferredPartType(request.Message);
                    if (preferredType != null && partCandidates.Any())
                    {
                        var typeFiltered = partCandidates
                            .Where(p => string.Equals(p.PartType, preferredType, StringComparison.OrdinalIgnoreCase))
                            .ToList();
                        if (typeFiltered.Any()) partCandidates = typeFiltered;
                    }

                    // Exclude IDs đã shown nếu user xin alternatives
                    if (wantsAlternatives && shownProductIds.Count > 0 && partCandidates.Any())
                    {
                        var beforeExclude = partCandidates.Count;
                        partCandidates = partCandidates.Where(p => !shownProductIds.Contains(p.Id)).ToList();
                        _logger.LogInformation(
                            "Part alternatives request: {Before} → {After} after excluding shown IDs.",
                            beforeExclude, partCandidates.Count);
                        if (!partCandidates.Any()) qdrantFoundResults = false;
                    }

                    // Hard filter theo budget — chỉ áp dụng khi user hỏi về 1 part type cụ thể
                    // (nếu user hỏi chung chung "bàn phím 1 triệu" thì budget áp cho assembled, không cho parts)
                    if (preferredType != null && partCandidates.Any())
                    {
                        var partConstraints = ExtractUserConstraints(request.Message);
                        if (partConstraints.HasBudget)
                        {
                            var budgetFiltered = partCandidates
                                .Where(p => !partConstraints.BudgetMin.HasValue || p.Price >= partConstraints.BudgetMin.Value)
                                .Where(p => !partConstraints.BudgetMax.HasValue || p.Price <= partConstraints.BudgetMax.Value)
                                .ToList();
                            _logger.LogInformation(
                                "Part budget filter ({Type}): {Min} - {Max} VND. {Before} → {After}.",
                                preferredType, partConstraints.BudgetMin, partConstraints.BudgetMax,
                                partCandidates.Count, budgetFiltered.Count);
                            if (budgetFiltered.Any())
                            {
                                partCandidates = budgetFiltered;
                            }
                            else
                            {
                                qdrantFoundResults = false;
                                partCandidates.Clear();
                            }
                        }
                    }

                    // Re-rank theo shop reputation
                    Dictionary<Guid, ShopProfile?> partShops = new();
                    if (partCandidates.Any())
                    {
                        var partShopIds = partCandidates.Select(p => p.ShopId).Distinct().ToList();
                        var shops = (await _unitOfWork.Shops.GetByIdsAsync(partShopIds)).ToDictionary(s => s.Id);
                        foreach (var p in partCandidates)
                            partShops[p.Id] = shops.TryGetValue(p.ShopId, out var sh) ? sh : null;

                        var orderMap = partIds.Select((id, idx) => (id, idx)).ToDictionary(x => x.id, x => x.idx);
                        partCandidates = partCandidates
                            .OrderBy(p => orderMap.TryGetValue(p.Id, out var i) ? i : int.MaxValue)
                            .Select((p, idx) => new { Part = p, Position = idx, Shop = partShops[p.Id] })
                            .OrderByDescending(x => ComputeRankScore(x.Position, partCandidates.Count, x.Shop))
                            .Take(5)
                            .Select(x => x.Part)
                            .ToList();
                    }

                    // Pool expansion cho parts: tương tự assembled, đảm bảo ≥3 candidates nếu DB còn.
                    if (partCandidates.Count < 3)
                    {
                        var existingPartIds = partCandidates.Select(c => c.Id).ToHashSet();
                        var fallbackParts = await LoadFallbackPartCandidatesAsync(null, limit: 30);
                        var partExpandConstraints = ExtractUserConstraints(request.Message);
                        var additionalParts = fallbackParts
                            .Where(p => !existingPartIds.Contains(p.Id))
                            .Where(p => preferredType == null || string.Equals(p.PartType, preferredType, StringComparison.OrdinalIgnoreCase))
                            .Where(p => preferredType == null
                                        || !partExpandConstraints.HasBudget
                                        || (!partExpandConstraints.BudgetMin.HasValue || p.Price >= partExpandConstraints.BudgetMin.Value)
                                          && (!partExpandConstraints.BudgetMax.HasValue || p.Price <= partExpandConstraints.BudgetMax.Value))
                            .Where(p => !wantsAlternatives || !shownProductIds.Contains(p.Id))
                            .Take(5 - partCandidates.Count)
                            .ToList();

                        if (additionalParts.Any())
                        {
                            partCandidates.AddRange(additionalParts);
                            if (partCandidates.Any()) qdrantFoundResults = true;
                            _logger.LogInformation(
                                "Parts pool expansion: {Before} + {Add} = {After}.",
                                existingPartIds.Count, additionalParts.Count, partCandidates.Count);
                        }
                    }

                    var typeHint = preferredType != null ? $" (filtered to {preferredType})" : "";
                    var header = qdrantFoundResults
                        ? $"--- CANDIDATE PARTS{typeHint} (semantically close, ranked by shop reputation) ---"
                        : "--- PLATFORM PARTS (no semantic match — for general reference only) ---";
                    contextData = header + "\n" + string.Join("\n", partCandidates.Select(k =>
                    {
                        var shopInfo = partShops.TryGetValue(k.Id, out var s) ? FormatShopInfo(s) : "Shop: (unknown)";
                        return $"ID: {k.Id}, Name: {k.Name}, Price: {k.Price} VND, Type: {k.PartType}, {shopInfo}, Specs: {k.Specifications}";
                    }));
                }

                if (string.IsNullOrWhiteSpace(contextData))
                    contextData = "(no candidate data found)";

                // CUSTOM BUILD SUGGESTION: khi assembled không match + user có yêu cầu component cụ thể
                // → tìm kit + compatible parts từ Builder ecosystem để gợi ý 1 build có thể tự lắp.
                // Web không bán linh kiện rời → đây là cách duy nhất user mua được combo specific.
                if (preferAssembled
                    && !qdrantFoundResults
                    && (HasSwitchTypeRequirement(request.Message) || HasKeycapRequirement(request.Message)))
                {
                    var customBuild = await TryBuildCustomBuildContextAsync(request.Message, promptVector);
                    if (customBuild != null)
                    {
                        customBuildContext = customBuild;
                        contextData = customBuild.Context + "\n\n--- Original Platform Reference ---\n" + contextData;
                        _logger.LogInformation(
                            "Custom Build Suggestion built for kit {KitId} ({SwitchCount} switches, {KeycapCount} keycaps).",
                            customBuild.KitId, customBuild.ValidSwitchIds.Count, customBuild.ValidKeycapIds.Count);
                    }
                }

                // Auto-trigger web search: Qdrant không có kết quả + query mang tính style/aesthetic
                // Không trigger cho off-topic queries (LLM tự xử lý từ chối)
                // Custom Build Suggestion đã có → không cần fallback ra web search
                autoWebSearch = customBuildContext == null
                    && !qdrantFoundResults
                    && messageIntent == MessageIntent.KeyboardQuery
                    && HasAestheticStyleQuery(request.Message);

                // 4b. Web search trigger: explicit (user nói "tìm mẫu") hoặc auto (Qdrant miss + style query)
                if ((forceWebSearch || autoWebSearch) && _webSearchService != null)
                {
                    try
                    {
                        var tavilyQuery = BuildTavilyQuery(request.Message, autoWebSearch);
                        _logger.LogInformation(
                            "Web search triggered [{Mode}] for conv {ConvId}. Query: {Query}",
                            forceWebSearch ? "explicit" : "auto", conversation.Id, tavilyQuery);

                        var webResult = await _webSearchService.SearchAsync(tavilyQuery, maxResults: 5);
                        _logger.LogInformation(
                            "Tavily raw results: {Count}. URLs: {Urls}",
                            webResult.Sources.Count,
                            string.Join(", ", webResult.Sources.Select(s => s.Url)));

                        // Keep only build-showcase results; discard how-to articles, subreddit homepages,
                        // tutorial videos, and generic info pages. Take best 5 after filtering.
                        var showcaseSources = webResult.Sources
                            .Where(IsKeyboardShowcaseResult)
                            .Take(5)
                            .ToList();

                        // Fall back to all results if filtering removed everything (edge case)
                        var finalSources = showcaseSources.Count > 0 ? showcaseSources : webResult.Sources.Take(5).ToList();

                        _logger.LogInformation(
                            "After filter: {Filtered}/{Raw} sources kept.",
                            finalSources.Count, webResult.Sources.Count);

                        if (finalSources.Count > 0)
                        {
                            usedWebSearch = true;

                            var filteredResult = new WebSearchResult
                            {
                                Sources = finalSources,
                                Images = webResult.Images
                            };

                            var webContext = BuildWebSearchRagContext(filteredResult, request.Message);
                            contextData = webContext + "\n\n--- Platform Products (for reference) ---\n" + contextData;

                            for (int i = 0; i < finalSources.Count; i++)
                            {
                                var src = finalSources[i];
                                sourceLinks.Add(new WebSearchSourceLink
                                {
                                    Title = src.Title,
                                    Url = src.Url,
                                    ImageUrl = i < webResult.Images.Count ? webResult.Images[i] : null,
                                    Snippet = src.Content.Length > 250 ? src.Content[..247] + "..." : src.Content
                                });
                            }
                        }
                        else
                        {
                            _logger.LogWarning("Web search returned 0 usable results for query: {Query}", tavilyQuery);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Tavily web search failed, continuing with DB context only.");
                    }
                }
            }

            // 5. Build system prompt — base persona + runtime note matching the situation
            string runtimeNote;
            if (messageIntent == MessageIntent.Greeting)
            {
                runtimeNote =
                    "\n\n[RUNTIME — GREETING OR OFF-TOPIC]: The user sent a greeting, social message, or a question unrelated to keyboards. " +
                    "If it is a greeting → respond warmly and briefly, introduce yourself as AMK Advisor, ask how you can help with keyboards. " +
                    "If it is clearly off-topic (weather, date, food, sports, etc.) → politely say in ONE sentence that you can only help with mechanical keyboards and AMK Collective, then ask if they have a keyboard question. " +
                    "In both cases: set all IDs to null, TotalEstimatedPrice to 0.";
            }
            else if (usedWebSearch && autoWebSearch)
            {
                // Auto-triggered: user không hỏi "tìm mẫu" — AI tự tìm vì Qdrant không có gì phù hợp
                runtimeNote = BuildWebSearchInspirationRuntimeNote(isAuto: true);
            }
            else if (usedWebSearch)
            {
                // Explicit: user chủ động hỏi "tìm mẫu" / "tham khảo"
                runtimeNote = BuildWebSearchInspirationRuntimeNote(isAuto: false);
            }
            else if (customBuildContext != null)
            {
                // Có combo custom build phù hợp từ Builder ecosystem
                runtimeNote =
                    "\n\n[RUNTIME — CUSTOM BUILD SUGGESTION]: Platform khong co assembled keyboard khop yeu cau, " +
                    "nhung tim duoc 1 KIT cung cac SWITCH + KEYCAP compatible thoa man yeu cau cua user. " +
                    "Output rules: " +
                    "(1) Set KitId = exact ID from CUSTOM BUILD SUGGESTION context. " +
                    "(2) Set SwitchId = pick ONE switch ID from the SWITCH OPTIONS list (must match user requirement). " +
                    "(3) Set KeycapId = pick ONE keycap ID from the KEYCAP OPTIONS list (must match user requirement). " +
                    "(4) Set AssembledProductId = null. " +
                    "(5) TotalEstimatedPrice = sum of kit price + chosen switch price + chosen keycap price (in VND). System will recompute to ensure accuracy, but provide best estimate. " +
                    "Reasoning content (in Vietnamese, NO IDs): " +
                    "- Giai thich ngan: platform khong co san phim hoan chinh khop, nhung co the tu lap qua Builder Tool. " +
                    "- BAT BUOC neu ten KIT va ten SHOP (vi du: 'Kit X cua shop Y'). Day la thong tin quan trong de user biet vao dau de mua. " +
                    "- Mo ta combo: ten kit + shop, ten switch (vi sao yen tinh/clicky/tactile/linear), ten keycap (vi sao xuyen LED/PBT/etc). " +
                    "- Ghi ro tong gia tien VND. " +
                    "- Huong dan user: vao Builder Tool, chon kit nay (cua shop X), chon switch + keycap nay de hoan tat build. " +
                    "NEVER paste IDs into the Reasoning text — use only product names + shop names.";
            }
            else if (qdrantFoundResults)
            {
                runtimeNote = preferAssembled
                    ? "\n\n[RUNTIME — RECOMMEND FROM CONTEXT]: Context contains 2-5 platform candidates that already passed code-side filtering. " +
                      "Hệ thống sẽ HIỂN THỊ TẤT CẢ candidates lên FE dưới dạng product cards — Reasoning của bạn KHÔNG cần và KHÔNG được list lại tên / giá / shop của từng sản phẩm (sẽ duplicate với cards). " +
                      "PRINCIPLES: " +
                      "(1) Reasoning là 1 đoạn văn NGẮN (2-3 câu, không quá 60 từ), KHÔNG xuống dòng, KHÔNG dùng bullet hay đánh số. " +
                      "(2) Câu mở: tổng hợp / nhận xét chung về các option phía dưới (vd: 'Mình gợi ý {N} mẫu phía dưới, có cả option giá tốt và cao cấp tùy ngân sách.'). " +
                      "(3) Câu giữa (optional): 1 lưu ý hữu ích — vd các mẫu khác nhau ở layout/switch/budget tier để user dễ chọn. " +
                      "(4) Câu kết: mời user chọn hoặc thu hẹp tiêu chí (vd: 'Bạn thích mẫu nào? Hoặc nói rõ ngân sách/layout để mình lọc kỹ hơn.'). " +
                      "(5) TUYỆT ĐỐI KHÔNG: list tên sản phẩm, không mention giá cụ thể, không mention tên shop trong Reasoning. " +
                      "(6) Set AssembledProductId = null. TotalEstimatedPrice = 0 (system tự tính)."
                    : "\n\n[RUNTIME — RECOMMEND FROM CONTEXT (parts)]: Context contains parts already filtered. " +
                      "PRINCIPLES: " +
                      "(1) Nếu user hỏi 1 part type cụ thể → cards hiện ở dưới, Reasoning chỉ cần 2-3 câu nhận xét chung không xuống dòng, KHÔNG list lại tên/giá. " +
                      "(2) Nếu user hỏi build / combo → pick 1 ID phù hợp nhất (KitId/SwitchId/KeycapId). Mention name + shop ngắn gọn. " +
                      "(3) Khi recommend Kit: nhắc Kit = vỏ + PCB + plate, cần thêm switch + keycap, suggest Builder Tool.";
            }
            else
            {
                runtimeNote =
                    "\n\n[PRINCIPLES WHEN NOTHING FITS]: Platform has no eligible product after filtering. Set all ID fields to null. " +
                    "(1) If the question is off-topic (not about keyboards), politely decline in one sentence. Stop. " +
                    "(2) Otherwise give GENERIC keyboard guidance: discuss layout tradeoffs (60% / 65% / 75% / TKL / full-size), " +
                    "switch feel (linear / tactile / clicky / silent), pre-built vs kit-based, and a realistic VND budget tier for what they want. " +
                    "(3) Do NOT name specific external brands or model numbers — naming brands without web search context is a hallucination. " +
                    "(4) Offer two next steps: " +
                    "  • say 'tìm mẫu trên web' to get real product references via web search, OR " +
                    "  • create a Commission Request describing layout/switch/budget so shops can quote. " +
                    "(5) Be honest about WHY platform has no match (price tier, niche layout, etc.) — use the actual reason from the user's request, never invent constraints they did not mention.";
            }

            // Detect follow-up "alternatives" pattern — yêu cầu LLM đa dạng hóa, không lặp lại.
            if (wantsAlternatives)
            {
                bool poolExhausted = !qdrantFoundResults || (preferAssembled
                    ? !assembledCandidates.Any()
                    : !partCandidates.Any());

                if (poolExhausted)
                {
                    // DB đã hết sản phẩm khác trong tầm yêu cầu → surface commission/custom path mạnh.
                    runtimeNote +=
                        "\n\n[FOLLOW-UP — POOL EXHAUSTED]: User asked for alternatives but the platform has no more products matching their requirements (after excluding what was already shown). " +
                        "Acknowledge honestly that AMK Collective hiện không còn sản phẩm khác khớp yêu cầu. " +
                        "Then offer THREE paths clearly: " +
                        "(1) **Commission Request** — mô tả layout, switch, keycap theme, ngân sách → các shop sẽ build riêng. " +
                        "(2) **Builder Tool** — tự lắp ráp custom build từ kit + switch + keycap có sẵn trên platform. " +
                        "(3) **Tìm mẫu trên web** — nói câu này để xem các build từ cộng đồng làm cảm hứng thiết kế cho Commission. " +
                        "All ID fields MUST be null. DO NOT repeat any product previously shown. DO NOT name external brands.";
                }
                else
                {
                    runtimeNote +=
                        "\n\n[FOLLOW-UP — DIFFERENT OPTIONS]: User asked for an alternative. Context has been pre-filtered to EXCLUDE products you already showed in this conversation. " +
                        "Pick a DIFFERENT product from the current context — never repeat. " +
                        "If user expressed dissatisfaction (chán/xấu/không thích), acknowledge briefly before recommending. " +
                        "Mention name + shop. DO NOT name external brands without web search.";
                }
            }

            var systemPrompt = _settings.ChatbotPrompt + runtimeNote;

            var ragUserMessage =
                $"User Request: {request.Message}\n\n" +
                $"Available Recommendation Context:\n{contextData}";

            // 6. Build history cho LLM từ các tin đã lưu
            var historyPairs = recentHistory.Select(m => (m.Role, m.Content));

            // 7. Gọi LLM với toàn bộ lịch sử
            string? aiJson = await CallLLMWithHistoryAsync(systemPrompt, historyPairs, ragUserMessage);

            if (string.IsNullOrEmpty(aiJson))
                throw new Exception("AI failed to generate a response.");

            _logger.LogInformation("AI Chat Raw Response: {AiJson}", aiJson);

            // 8. Parse + build items
            var recommendation = ParseRecommendationResponse(aiJson);

            // Track AI's original picks — nếu validation clear hết, ta phải override Reasoning
            // vì LLM đã đề cập tên sản phẩm trong text (mà sản phẩm đó vừa bị reject).
            var originalAssembledId = recommendation.AssembledProductId;
            bool aiPickedAssembled = originalAssembledId.HasValue;
            bool aiPickedCustomBuild = recommendation.KitId.HasValue
                                       && recommendation.SwitchId.HasValue
                                       && recommendation.KeycapId.HasValue;

            // Server-side validation — đây là lớp bảo vệ cuối cùng, không phụ thuộc vào LLM.
            // Check 3 điều kiện: layout mismatch, price sanity, switch requirement mismatch.
            if (recommendation.AssembledProductId.HasValue)
            {
                var selectedProduct = assembledCandidates.FirstOrDefault(p => p.Id == recommendation.AssembledProductId.Value);
                if (selectedProduct != null)
                {
                    string? clearReason = null;

                    // (A) Layout mismatch
                    if (IsLayoutMismatch(request.Message, selectedProduct.Layout))
                        clearReason = $"layout mismatch (user wants different layout, product is {selectedProduct.Layout})";

                    // (B) Price sanity — dưới 200,000 VND là test data, không phải bàn phím thật
                    else if (selectedProduct.Price < 200_000)
                        clearReason = $"unrealistic price {selectedProduct.Price} VND (likely test data)";

                    // (C) Switch type requirement — user hỏi silent/clicky/tactile nhưng product không có switch info
                    else if (HasSwitchTypeRequirement(request.Message) && !ProductHasSwitchInfo(selectedProduct.Description))
                        clearReason = $"user requires specific switch type but product '{selectedProduct.Name}' has no switch info in description";

                    if (clearReason != null)
                    {
                        _logger.LogWarning("Server-side validation cleared AssembledProductId: {Reason}", clearReason);
                        recommendation.AssembledProductId = null;
                    }
                }
            }

            // CUSTOM BUILD validation: nếu LLM trả về kit + switch + keycap → check compatibility thực tế qua DB.
            // Ngăn LLM bịa ID hoặc combine sai (chọn switch không thuộc allowed list của kit).
            bool customBuildValid = false;
            if (customBuildContext != null
                && recommendation.KitId.HasValue
                && recommendation.SwitchId.HasValue
                && recommendation.KeycapId.HasValue)
            {
                bool kitMatches = recommendation.KitId.Value == customBuildContext.KitId;
                bool switchInList = customBuildContext.ValidSwitchIds.Contains(recommendation.SwitchId.Value);
                bool keycapInList = customBuildContext.ValidKeycapIds.Contains(recommendation.KeycapId.Value);

                if (kitMatches && switchInList && keycapInList)
                {
                    // Double-check qua repository
                    var switchOk = await _unitOfWork.KitDesignOptions.CheckCompatibilityAsync(
                        recommendation.KitId.Value, recommendation.SwitchId.Value);
                    var keycapOk = await _unitOfWork.KitDesignOptions.CheckCompatibilityAsync(
                        recommendation.KitId.Value, recommendation.KeycapId.Value);
                    customBuildValid = switchOk && keycapOk;

                    if (!customBuildValid)
                        _logger.LogWarning("Custom build compatibility check failed in DB for kit {KitId}", recommendation.KitId);
                }
                else
                {
                    _logger.LogWarning(
                        "LLM returned IDs outside provided custom build options. KitMatch={K}, SwitchInList={S}, KeycapInList={Kc}",
                        kitMatches, switchInList, keycapInList);
                }

                if (!customBuildValid)
                {
                    recommendation.KitId = null;
                    recommendation.SwitchId = null;
                    recommendation.KeycapId = null;
                }
                else
                {
                    // Override LLM's TotalEstimatedPrice với giá tính chính xác từ DB.
                    // Đảm bảo accuracy 100% — không phụ thuộc LLM cộng số có đúng không.
                    decimal switchPrice = customBuildContext.ComponentPrices.TryGetValue(recommendation.SwitchId!.Value, out var sp) ? sp : 0m;
                    decimal keycapPrice = customBuildContext.ComponentPrices.TryGetValue(recommendation.KeycapId!.Value, out var kp) ? kp : 0m;
                    decimal computedTotal = customBuildContext.KitPrice + switchPrice + keycapPrice;
                    if (computedTotal != recommendation.TotalEstimatedPrice)
                    {
                        _logger.LogInformation(
                            "Custom build price corrected: LLM said {LLM} VND, actual = {Actual} VND (kit {Kit} + sw {Sw} + kc {Kc})",
                            recommendation.TotalEstimatedPrice, computedTotal,
                            customBuildContext.KitPrice, switchPrice, keycapPrice);
                    }
                    recommendation.TotalEstimatedPrice = computedTotal;
                }
            }

            if (messageIntent == MessageIntent.Greeting)
            {
                // Greeting: no product suggestions
                recommendation.Items = new List<AIRecommendationItemDTO>();
            }
            else if (usedWebSearch)
            {
                // Web search: external references for inspiration. SourceLinks là đủ.
                recommendation.Items = new List<AIRecommendationItemDTO>();
            }
            else if (customBuildValid)
            {
                // Custom build suggestion: build 3 part cards (kit + switch + keycap)
                recommendation.Items = await BuildRecommendationItemsAsync(recommendation, partCandidates);
            }
            else if (qdrantFoundResults)
            {
                // RECOMMEND FROM CONTEXT: present 2-5 candidates đã pre-filtered.
                // Bypass việc LLM pick 1 ID — code trực tiếp present candidates đã lọc sạch.
                // Lý do: (1) user thường muốn so sánh nhiều option, (2) LLM hay bỏ sót hoặc chỉ chọn 1.
                if (preferAssembled)
                {
                    recommendation.Items = await BuildFallbackItemsAsync(null, assembledCandidates.Take(5).ToList());
                    // Compute estimated price từ trung bình range candidates → giúp user nắm tầm giá.
                    if (recommendation.Items.Count > 0)
                    {
                        var prices = recommendation.Items.Select(i => i.Price).Where(p => p > 0).ToList();
                        if (prices.Any())
                            recommendation.TotalEstimatedPrice = (decimal)prices.Average();
                    }
                }
                else
                {
                    // Parts: nếu user hỏi 1 part type cụ thể → list 2-5 parts; nếu không → giữ logic AI pick (combo)
                    var preferredType = DetectPreferredPartType(request.Message);
                    if (preferredType != null && partCandidates.Any())
                    {
                        recommendation.Items = await BuildFallbackItemsAsync(partCandidates.Take(5).ToList(), null);
                        if (recommendation.Items.Count > 0)
                        {
                            var prices = recommendation.Items.Select(i => i.Price).Where(p => p > 0).ToList();
                            if (prices.Any())
                                recommendation.TotalEstimatedPrice = (decimal)prices.Average();
                        }
                    }
                    else
                    {
                        recommendation.Items = await BuildRecommendationItemsAsync(recommendation, partCandidates);
                    }
                }
            }
            else
            {
                // No match (off-topic decline or commission guidance): no random products
                recommendation.Items = new List<AIRecommendationItemDTO>();
            }

            // Anti-hallucination guard: nếu AI đã pick sản phẩm nhưng validation reject hết
            // và items cuối cùng rỗng → Reasoning đang đề cập sản phẩm KHÔNG tồn tại trong response.
            // Override bằng fallback message để FE không hiển thị text mâu thuẫn với items=[].
            bool aiPickedSomething = aiPickedAssembled || aiPickedCustomBuild;
            bool allCleared = !recommendation.AssembledProductId.HasValue && !customBuildValid;
            bool reasoningOverridden = false;
            if (aiPickedSomething
                && allCleared
                && recommendation.Items.Count == 0
                && messageIntent != MessageIntent.Greeting
                && !usedWebSearch)
            {
                _logger.LogWarning(
                    "AI Reasoning override: validation cleared all IDs (assembled={A}, customBuild={C}). Replacing Reasoning to avoid product hallucination.",
                    aiPickedAssembled, aiPickedCustomBuild);

                recommendation.Reasoning = BuildNoMatchFallbackMessage();
                recommendation.TotalEstimatedPrice = 0;
                reasoningOverridden = true;
            }

            // Safety net: scan Reasoning for external brand hallucinations when no web search was used.
            // Last line of defense — nếu LLM bypass prompt instructions, ta vẫn catch được.
            if (!reasoningOverridden
                && !usedWebSearch
                && messageIntent != MessageIntent.Greeting
                && ContainsExternalBrandMention(recommendation.Reasoning))
            {
                _logger.LogWarning(
                    "AI Reasoning override: detected external brand mention without web search context. Original: {Original}",
                    recommendation.Reasoning);
                recommendation.Reasoning = BuildNoMatchFallbackMessage();
                recommendation.TotalEstimatedPrice = 0;
                recommendation.AssembledProductId = null;
                recommendation.KitId = null;
                recommendation.SwitchId = null;
                recommendation.KeycapId = null;
                recommendation.Items = new List<AIRecommendationItemDTO>();
                reasoningOverridden = true;
            }

            // Safety net 2: items rỗng + Reasoning chứa product claim (giá VND, shop name) + không web search
            // → LLM đang mention sản phẩm cụ thể nhưng items không có → mismatch chắc chắn.
            // Đây là case như "Bàn phím test create1 từ shop SwitchLab VN giá 333,333 VND" với items=[].
            if (!reasoningOverridden
                && !usedWebSearch
                && messageIntent != MessageIntent.Greeting
                && recommendation.Items.Count == 0
                && ContainsProductClaim(recommendation.Reasoning))
            {
                _logger.LogWarning(
                    "AI Reasoning override: items empty but Reasoning mentions product/price/shop — hallucination mismatch. Original: {Original}",
                    recommendation.Reasoning);
                recommendation.Reasoning = BuildNoMatchFallbackMessage();
                recommendation.TotalEstimatedPrice = 0;
                recommendation.AssembledProductId = null;
                recommendation.KitId = null;
                recommendation.SwitchId = null;
                recommendation.KeycapId = null;
                recommendation.Items = new List<AIRecommendationItemDTO>();
            }

            // Post-process Reasoning: nếu items >= 2 (list mode) và không phải web search,
            // strip newlines + bullet/number markers để text thành 1 đoạn gọn.
            // Lý do: items đã hiển thị làm cards → list trong text bị duplicate và ký tự "\n1." xấu.
            if (recommendation.Items.Count >= 2 && !usedWebSearch)
            {
                recommendation.Reasoning = CleanListFormatting(recommendation.Reasoning);
            }

            // 9. Lưu assistant message — payload dạng wrapper để giữ cả Items lẫn SourceLinks
            var assistantPayload = JsonSerializer.Serialize(new ChatMessagePayload
            {
                Items = recommendation.Items,
                SourceLinks = sourceLinks,
                UsedWebSearch = usedWebSearch,
                EstimatedPrice = recommendation.TotalEstimatedPrice
            });

            var assistantMsg = new AIChatMessage
            {
                ConversationId = conversation.Id,
                Role = "assistant",
                Content = recommendation.Reasoning,
                RecommendationPayload = assistantPayload
            };
            await _unitOfWork.AIChat.AddMessageAsync(assistantMsg);

            // 10. Auto-title từ câu hỏi đầu tiên (truncate 60 ký tự)
            conversation.Title = isNew
                ? (request.Message.Length > 60 ? request.Message[..57] + "..." : request.Message)
                : conversation.Title;
            conversation.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.AIChat.UpdateConversation(conversation);

            await _unitOfWork.CommitAsync();

            return new AIChatResponseDTO
            {
                ConversationId = conversation.Id,
                Reply = recommendation.Reasoning,
                Items = recommendation.Items,
                SourceLinks = sourceLinks,
                UsedWebSearch = usedWebSearch,
                EstimatedPrice = recommendation.TotalEstimatedPrice
            };
        }

        // ════════════════════════════════════════════════════════════════
        // AI CHATBOT — STREAMING variant (Server-Sent Events)
        // ════════════════════════════════════════════════════════════════
        //
        // Phase 1 implementation: heartbeat-based streaming.
        // Wrap ChatAsync với heartbeats định kỳ → giữ TCP connection alive → FE không timeout.
        // Logic AI giữ NGUYÊN 100% — không động vào ChatAsync → zero risk to existing functionality.
        //
        // Tradeoff: chưa có "text typing out" như ChatGPT, nhưng:
        //   ✓ Fix triệt để timeout issue (FE thấy heartbeat mỗi 2s)
        //   ✓ Connection không bao giờ idle → không bị middleware/proxy cắt
        //   ✓ Cancel khi FE close tab (CancellationToken propagate)
        //   ✓ Có thể upgrade lên token-by-token streaming sau (Phase 2)
        //
        public async IAsyncEnumerable<ChatChunk> ChatStreamAsync(
            Guid userId,
            AIChatRequestDTO request,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            // 1. Heartbeat đầu tiên — báo FE đã connect ok
            yield return new ChatChunk { Type = "start" };

            // 2. Chạy ChatAsync trong background; heartbeat mỗi 2s nếu chưa xong
            var chatTask = Task.Run(() => ChatAsync(userId, request), cancellationToken);

            const int heartbeatMs = 2000;
            while (!chatTask.IsCompleted)
            {
                var delayTask = Task.Delay(heartbeatMs, cancellationToken);
                var completed = await Task.WhenAny(chatTask, delayTask);
                if (completed != chatTask)
                {
                    yield return new ChatChunk { Type = "heartbeat" };
                }
                if (cancellationToken.IsCancellationRequested) break;
            }

            // 3. Lấy kết quả (hoặc bắt error)
            AIChatResponseDTO? result = null;
            string? errorMessage = null;
            try
            {
                result = await chatTask;
            }
            catch (KeyNotFoundException ex)
            {
                errorMessage = ex.Message;
                _logger.LogWarning(ex, "ChatStreamAsync: conversation not found");
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("ChatStreamAsync: cancelled by client");
                yield break;
            }
            catch (Exception ex)
            {
                errorMessage = "AI service unavailable. Please try again.";
                _logger.LogError(ex, "ChatStreamAsync: unexpected error");
            }

            // 4. Trả result hoặc error
            if (result != null)
            {
                yield return new ChatChunk
                {
                    Type = "result",
                    ConversationId = result.ConversationId,
                    Reply = result.Reply,
                    Items = result.Items,
                    SourceLinks = result.SourceLinks,
                    UsedWebSearch = result.UsedWebSearch,
                    EstimatedPrice = result.EstimatedPrice
                };
            }
            else
            {
                yield return new ChatChunk
                {
                    Type = "error",
                    ErrorMessage = errorMessage ?? "Unknown error"
                };
            }

            // 5. Done signal — FE close connection
            yield return new ChatChunk { Type = "done" };
        }

        public async Task<List<AIChatConversationSummaryDTO>> GetChatConversationsAsync(Guid userId, int limit = 20)
        {
            var conversations = await _unitOfWork.AIChat.GetConversationsByUserAsync(userId, limit);

            return conversations.Select(c => new AIChatConversationSummaryDTO
            {
                Id = c.Id,
                Title = c.Title,
                LastMessage = c.Messages.FirstOrDefault()?.Content,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt
            }).ToList();
        }

        public async Task<List<AIChatMessageDTO>> GetChatMessagesAsync(Guid userId, int conversationId)
        {
            // Kiểm tra ownership
            var conversation = await _unitOfWork.AIChat.GetConversationByIdAsync(conversationId, userId)
                ?? throw new KeyNotFoundException($"Conversation {conversationId} not found.");

            var messages = await _unitOfWork.AIChat.GetAllMessagesAsync(conversation.Id);

            return messages.Select(m =>
            {
                List<AIRecommendationItemDTO>? items = null;
                List<WebSearchSourceLink>? sourceLinks = null;
                bool usedWebSearch = false;
                decimal estimatedPrice = 0m;

                if (m.Role == "assistant" && !string.IsNullOrEmpty(m.RecommendationPayload))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(m.RecommendationPayload);
                        var root = doc.RootElement;

                        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("Items", out _))
                        {
                            // Format mới: { Items, SourceLinks, UsedWebSearch, EstimatedPrice }
                            var payload = JsonSerializer.Deserialize<ChatMessagePayload>(m.RecommendationPayload);
                            if (payload != null)
                            {
                                items = payload.Items;
                                sourceLinks = payload.SourceLinks.Count > 0 ? payload.SourceLinks : null;
                                usedWebSearch = payload.UsedWebSearch;
                                estimatedPrice = payload.EstimatedPrice;
                            }
                        }
                        else if (root.ValueKind == JsonValueKind.Array)
                        {
                            // Format cũ: [ AIRecommendationItemDTO, ... ]
                            items = JsonSerializer.Deserialize<List<AIRecommendationItemDTO>>(m.RecommendationPayload);
                        }
                    }
                    catch
                    {
                        // payload lỗi format → bỏ qua, trả null
                    }
                }

                return new AIChatMessageDTO
                {
                    Id = m.Id,
                    Role = m.Role,
                    Content = m.Content,
                    Items = items,
                    SourceLinks = sourceLinks,
                    UsedWebSearch = usedWebSearch,
                    EstimatedPrice = estimatedPrice,
                    CreatedAt = m.CreatedAt
                };
            }).ToList();
        }

        // Returns true if the Tavily result is a keyboard build showcase (photo post, finished build thread)
        // rather than a how-to article, tutorial video, or subreddit homepage.
        // Two signals are checked independently:
        //   URL structure — a specific Reddit post or GeekHack topic is almost always a build share;
        //                   a subreddit root or YouTube watch URL with a tutorial title is not.
        //   Title keywords — "how to", "guide", "best X" etc. mark editorial/informational content.
        private static bool IsKeyboardShowcaseResult(WebSearchSource source)
        {
            var url = source.Url.ToLowerInvariant();
            var title = source.Title.ToLowerInvariant();

            // Drop subreddit homepages — they have no specific build content
            // Pattern: reddit.com/r/<name>/ with nothing after (no /comments/)
            if (url.Contains("reddit.com/r/") && !url.Contains("/comments/"))
                return false;

            // Drop YouTube tutorials / guides
            if (url.Contains("youtube.com") || url.Contains("youtu.be"))
            {
                string[] tutorialTitleSignals = { "how to", "hướng dẫn", "guide", "tutorial", "tips", "advice", "first custom", "build your" };
                if (tutorialTitleSignals.Any(t => title.Contains(t)))
                    return false;
            }

            // Drop generic keyboard info / review articles
            string[] articleTitleSignals = { "how to", "hướng dẫn", "cách chọn", "nên chọn", "phím cơ có những", "buying guide", "best ", "vs ", " vs", "review", "comparison", "looking for advice", "xin tư vấn" };
            if (articleTitleSignals.Any(t => title.Contains(t)))
                return false;

            // Drop mainstream e-commerce / tech news sites — not build showcases
            string[] genericDomains = { "thegioididong.com", "anphatpc.com", "tinhte.vn", "pcworld.com", "tomsguide.com", "rtings.com", "techradar.com", "9to5mac.com", "baomoi.com", "vnexpress.net", "danviet.vn", "vietnamnet.vn" };
            if (genericDomains.Any(d => url.Contains(d)))
                return false;

            return true;
        }

        // Builds a richer embedding query by concatenating the last 2 user messages with the current one.
        // Problem it solves: follow-up messages like "có gì khác không" or "layout như thế nào" have
        // no keyboard context on their own — embedding them alone produces a near-random vector that
        // Qdrant cannot match against keyboard products. Prepending recent user turns restores that
        // context so the semantic search stays accurate across a long conversation.
        // Cap at ~300 chars so the embedding doesn't get diluted by unrelated earlier turns.
        private static string BuildEffectiveQdrantQuery(string currentMessage, IEnumerable<AIChatMessage> recentHistory)
        {
            var recentUserMessages = recentHistory
                .Where(m => m.Role == "user")
                .TakeLast(2)
                .Select(m => m.Content.Trim())
                .ToList();

            if (recentUserMessages.Count == 0)
                return currentMessage;

            var combined = string.Join(" | ", recentUserMessages.Append(currentMessage.Trim()));

            return combined.Length > 300 ? combined[^300..] : combined;
        }

        // Tính composite rank score = 60% Qdrant position + 40% shop quality.
        // Position 0 = best Qdrant match → score 1.0. Càng xa càng thấp.
        // Shop score: 50% quality (CurrentQualityScore) + 30% badge + 20% sales (log scale).
        // Shop = null → penalty 60% để không bị loại hoàn toàn nhưng deprioritize.
        private static double ComputeRankScore(int position, int totalCandidates, ShopProfile? shop)
        {
            double positionScore = totalCandidates <= 1
                ? 1.0
                : 1.0 - ((double)position / totalCandidates);

            if (shop == null) return positionScore * 0.6;

            double qualityScore = Math.Clamp(shop.CurrentQualityScore / 100.0, 0, 1);
            double badgeBonus = shop.Badge switch
            {
                Domain.Enums.ShopBadge.Premium => 1.0,
                Domain.Enums.ShopBadge.Verified => 0.5,
                _ => 0.0,
            };
            double salesBoost = shop.TotalSales > 0
                ? Math.Min(Math.Log10(1 + shop.TotalSales) / 3.0, 1.0)
                : 0.0;
            double shopScore = 0.5 * qualityScore + 0.3 * badgeBonus + 0.2 * salesBoost;

            return 0.6 * positionScore + 0.4 * shopScore;
        }

        // Shop quality không còn dùng làm hard filter — đã chuyển thành soft signal trong ComputeRankScore.
        // Lý do: hard filter làm new shops không bao giờ xuất hiện → death spiral, user thấy lặp lại 1 sản phẩm.

        // Format shop info ngắn cho LLM context.
        private static string FormatShopInfo(ShopProfile? shop)
        {
            if (shop == null) return "Shop: (unknown)";
            var badge = shop.Badge.ToString();
            return $"Shop: {shop.ShopName} | Badge: {badge} | Sales: {shop.TotalSales}";
        }

        // Try to build a Custom Build Suggestion context — returns null nếu không có combo phù hợp.
        // Pre-condition: user có yêu cầu component cụ thể (silent switch, xuyên LED keycap, etc.)
        // Lý do: web không bán linh kiện rời. User chỉ có thể mua qua Builder Tool với 1 kit + components compatible.
        private async Task<CustomBuildContext?> TryBuildCustomBuildContextAsync(
            string userMessage,
            float[] promptVector)
        {
            // Search Qdrant `parts` collection — sẽ filter PartType="Kit" sau khi load DB
            var partIds = await SearchQdrantSafelyAsync("parts", promptVector, limit: 10);
            if (!partIds.Any()) return null;

            var allParts = (await _unitOfWork.Models.GetByIdsAsync(partIds)).ToList();
            // Preserve Qdrant order
            var partOrderMap = partIds.Select((id, idx) => (id, idx)).ToDictionary(x => x.id, x => x.idx);
            var allKits = allParts
                .Where(p => string.Equals(p.PartType, "Kit", StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => partOrderMap.TryGetValue(p.Id, out var i) ? i : int.MaxValue)
                .ToList();

            if (!allKits.Any()) return null;

            // Re-rank kits theo shop reputation (cùng logic với assembled)
            var kitShopIds = allKits.Select(k => k.ShopId).Distinct().ToList();
            var kitShops = (await _unitOfWork.Shops.GetByIdsAsync(kitShopIds)).ToDictionary(s => s.Id);

            var candidateKits = allKits
                .Select((k, idx) => new
                {
                    Kit = k,
                    Shop = kitShops.TryGetValue(k.ShopId, out var s) ? s : null,
                    Position = idx
                })
                .OrderByDescending(x => ComputeRankScore(x.Position, allKits.Count, x.Shop))
                .Take(2)
                .Select(x => new { x.Kit, x.Shop })
                .ToList();

            if (!candidateKits.Any()) return null;

            // Với mỗi kit, fetch compatible components và filter theo requirement
            foreach (var kitWithShop in candidateKits)
            {
                var kit = kitWithShop.Kit;
                var kitShop = kitWithShop.Shop;
                IEnumerable<KitDesignOption> options;
                try
                {
                    options = await _unitOfWork.KitDesignOptions.GetOptionsByBaseKitAsync(kit.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch design options for kit {KitId}", kit.Id);
                    continue;
                }

                var optionsList = options.ToList();
                if (!optionsList.Any()) continue;

                // Group by step name (switch/keycap/etc)
                var stepGroups = optionsList
                    .Where(o => !string.IsNullOrWhiteSpace(o.StepName) && o.Component != null)
                    .GroupBy(o => o.StepName!.ToLowerInvariant())
                    .ToDictionary(g => g.Key, g => g.ToList());

                // Filter switches & keycaps — đây là 2 step cốt lõi user thường yêu cầu
                List<KitDesignOption> validSwitches = new();
                List<KitDesignOption> validKeycaps = new();
                bool hasSwitchReq = HasSwitchTypeRequirement(userMessage);
                bool hasKeycapReq = HasKeycapRequirement(userMessage);

                foreach (var (stepName, stepOptions) in stepGroups)
                {
                    if (stepName.Contains("switch") || stepName == "sw")
                        validSwitches = FilterStepOptionsByRequirement(stepOptions, userMessage, stepName);
                    else if (stepName.Contains("keycap"))
                        validKeycaps = FilterStepOptionsByRequirement(stepOptions, userMessage, stepName);
                }

                // Combo phải có ít nhất 1 switch và 1 keycap match (nếu user có yêu cầu)
                bool switchOk = !hasSwitchReq || validSwitches.Any();
                bool keycapOk = !hasKeycapReq || validKeycaps.Any();
                if (!switchOk || !keycapOk)
                {
                    _logger.LogInformation(
                        "Kit {KitName} doesn't have valid switch+keycap combo for user requirements. Skipping.",
                        kit.Name);
                    continue;
                }

                // Build context string cho LLM
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"--- CUSTOM BUILD SUGGESTION (compatible combo from same shop's kit ecosystem) ---");
                sb.AppendLine($"KIT: ID: {kit.Id}, Name: {kit.Name}, Price: {kit.Price} VND, {FormatShopInfo(kitShop)}, Specs: {kit.Specifications}");

                if (validSwitches.Any())
                {
                    sb.AppendLine($"SWITCH OPTIONS (compatible with this kit, matching user requirement):");
                    foreach (var sw in validSwitches.Take(5))
                    {
                        sb.AppendLine($"  - ID: {sw.ComponentId}, Name: {sw.Component?.Name}, Price: {sw.Component?.Price} VND, Description: {sw.Component?.Description}");
                    }
                }

                if (validKeycaps.Any())
                {
                    sb.AppendLine($"KEYCAP OPTIONS (compatible with this kit, matching user requirement):");
                    foreach (var kc in validKeycaps.Take(5))
                    {
                        sb.AppendLine($"  - ID: {kc.ComponentId}, Name: {kc.Component?.Name}, Price: {kc.Component?.Price} VND, Description: {kc.Component?.Description}");
                    }
                }

                return new CustomBuildContext
                {
                    Context = sb.ToString(),
                    KitId = kit.Id,
                    KitPrice = kit.Price,
                    KitName = kit.Name,
                    ShopName = kitShop?.ShopName,
                    ValidSwitchIds = validSwitches.Select(s => s.ComponentId).ToList(),
                    ValidKeycapIds = validKeycaps.Select(k => k.ComponentId).ToList(),
                    ComponentPrices = validSwitches.Concat(validKeycaps)
                        .Where(o => o.Component != null)
                        .GroupBy(o => o.ComponentId)
                        .ToDictionary(g => g.Key, g => g.First().Component!.Price)
                };
            }

            return null;
        }

        private class CustomBuildContext
        {
            public string Context { get; set; } = string.Empty;
            public Guid KitId { get; set; }
            public decimal KitPrice { get; set; }
            public string KitName { get; set; } = string.Empty;
            public string? ShopName { get; set; }
            public List<Guid> ValidSwitchIds { get; set; } = new();
            public List<Guid> ValidKeycapIds { get; set; } = new();
            public Dictionary<Guid, decimal> ComponentPrices { get; set; } = new();
        }

        // Returns true nếu user yêu cầu loại keycap đặc biệt (xuyên LED, shine-through, etc.)
        private static bool HasKeycapRequirement(string message)
        {
            var m = message.ToLowerInvariant();
            string[] keycapRequirements =
            {
                "xuyên led", "xuyen led", "xuyên đèn", "xuyen den",
                "shine through", "shine-through", "rgb keycap",
                "pbt", "abs", "double shot", "doubleshot",
                "pudding", "translucent", "trong suốt", "trong suot",
                "cherry profile", "oem profile", "sa profile", "xda profile",
                "thematic keycap", "themed keycap"
            };
            return keycapRequirements.Any(k => m.Contains(k));
        }

        // Filter components in a step bằng user requirement (silent switch, xuyên LED keycap, etc.)
        // Trả về subset hợp lệ — nếu không có gì match thì trả empty list.
        private static List<KitDesignOption> FilterStepOptionsByRequirement(
            IEnumerable<KitDesignOption> stepOptions,
            string userMessage,
            string stepName)
        {
            var lower = stepName.ToLowerInvariant();
            var msg = userMessage.ToLowerInvariant();

            // Switch step: nếu user yêu cầu silent/clicky/tactile/linear → filter component description
            if ((lower.Contains("switch") || lower.Contains("sw")) && HasSwitchTypeRequirement(userMessage))
            {
                return stepOptions
                    .Where(o => o.Component != null && ProductHasSwitchInfo(o.Component.Description) || ProductHasSwitchInfo(o.Component?.Specifications))
                    .Where(o =>
                    {
                        var desc = ((o.Component?.Description ?? "") + " " + (o.Component?.Specifications ?? "") + " " + (o.Component?.Name ?? "")).ToLowerInvariant();
                        if (msg.Contains("silent") || msg.Contains("yên tĩnh") || msg.Contains("yen tinh") || msg.Contains("im lặng") || msg.Contains("im lang"))
                            return desc.Contains("silent") || desc.Contains("yên") || desc.Contains("yen") || desc.Contains("im");
                        if (msg.Contains("clicky")) return desc.Contains("clicky") || desc.Contains("click");
                        if (msg.Contains("tactile")) return desc.Contains("tactile");
                        if (msg.Contains("linear")) return desc.Contains("linear");
                        return true;
                    })
                    .ToList();
            }

            // Keycap step: nếu user yêu cầu xuyên LED/shine-through → filter
            if (lower.Contains("keycap") && HasKeycapRequirement(userMessage))
            {
                return stepOptions
                    .Where(o =>
                    {
                        var desc = ((o.Component?.Description ?? "") + " " + (o.Component?.Specifications ?? "") + " " + (o.Component?.Name ?? "")).ToLowerInvariant();
                        if (msg.Contains("xuyên led") || msg.Contains("xuyen led") || msg.Contains("shine"))
                            return desc.Contains("xuyên") || desc.Contains("xuyen") || desc.Contains("shine") || desc.Contains("translucent") || desc.Contains("pudding");
                        if (msg.Contains("pbt")) return desc.Contains("pbt");
                        if (msg.Contains("abs")) return desc.Contains("abs");
                        return true;
                    })
                    .ToList();
            }

            // No filter applicable → return all
            return stepOptions.ToList();
        }

        // Classify message into one of three intents to decide RAG + web search strategy.
        // CHIẾN LƯỢC: Default về KeyboardQuery (chạy RAG). Chỉ route Greeting khi:
        //   (a) câu chào thuần túy không có keyword keyboard, HOẶC
        //   (b) câu CÓ off-topic signal rõ ràng (date, weather, math, food, jokes, politics).
        // RULE 0 trong system prompt là safety net cuối: nếu LLM thấy off-topic → trả JSON decline.
        private static MessageIntent ClassifyMessageIntent(string? message)
        {
            if (string.IsNullOrWhiteSpace(message)) return MessageIntent.Greeting;

            var m = message.Trim().ToLowerInvariant();

            // Pure greeting: chào hỏi không kèm câu hỏi
            string[] greetingPatterns = { "chào", "xin chào", "hello", "hi ", "hey ", "good morning", "good evening", "good afternoon", "buổi sáng", "buổi tối", "buổi chiều", "alo", "yo " };
            bool hasGreeting = greetingPatterns.Any(g => m.Contains(g));

            // Web search request: user explicit muốn tham khảo từ ngoài
            string[] webSearchSignals = {
                "tìm mẫu", "tìm các mẫu", "mẫu tham khảo", "tham khảo", "reference", "inspiration", "showcase",
                "đặt làm riêng", "làm riêng", "commission", "mẫu để đặt", "mẫu ngoài",
                "tìm trên mạng", "tìm trên web", "tìm ngoài web", "trên mạng", "trên web",
                "ngoài web", "ngoài trang", "bên ngoài", "ngoài hệ thống"
            };
            if (webSearchSignals.Any(k => m.Contains(k)))
                return MessageIntent.WebSearchRequest;

            // Off-topic signals — câu RÕ RÀNG về chủ đề khác (date/weather/math/food/personal/politics).
            // Nếu match → Greeting path (decline lịch sự). Nếu không match → default KeyboardQuery.
            string[] offTopicSignals = {
                // Date/time
                "ngày mấy", "ngay may", "thứ mấy", "thu may", "mấy giờ", "may gio",
                "what day", "what time", "today's date", "tomorrow",
                // Weather
                "thời tiết", "thoi tiet", "weather", "trời nắng", "troi nang", "trời mưa", "troi mua",
                // Food / cooking
                "ăn gì", "an gi", "nấu", "nau ", "cooking", "recipe", "công thức nấu", "phở", "pho ",
                "bún", "bun ", "cơm", "com ", "đồ ăn", "do an",
                // Math / calculation off-topic
                "bằng mấy", "bang may", "tính giùm", "tinh gium", "đáp án", "dap an",
                // Politics / public figures
                "tổng thống", "tong thong", "president", "thủ tướng", "thu tuong", "chính trị", "chinh tri",
                "bầu cử", "bau cu", "election",
                // Jokes / stories
                "kể chuyện cười", "ke chuyen cuoi", "tell a joke", "joke", "chuyện hài", "chuyen hai",
                // Personal / philosophical
                "tôi là ai", "toi la ai", "you are who", "ý nghĩa cuộc sống", "y nghia cuoc song",
                // Random topic markers
                "covid", "vaccine", "vắc xin", "vac xin", "bóng đá", "bong da", "football", "soccer",
                "phim hay", "phim moi", "âm nhạc", "am nhac", "music recommend",
            };
            bool hasOffTopic = offTopicSignals.Any(k => m.Contains(k));

            // Greeting thuần (chỉ chào, không kèm câu hỏi keyboard) → decline ngắn gọn
            if (hasGreeting && m.Length < 40 && !hasOffTopic)
            {
                // Check thêm: nếu message dài hoặc có dấu ? thì có thể vừa chào vừa hỏi → để KeyboardQuery
                if (!m.Contains('?') && m.Split(' ').Length <= 5)
                    return MessageIntent.Greeting;
            }

            // Off-topic rõ ràng → Greeting path (decline)
            if (hasOffTopic)
                return MessageIntent.Greeting;

            // Default: KeyboardQuery — chạy RAG. RULE 0 trong system prompt sẽ catch nếu vẫn off-topic.
            return MessageIntent.KeyboardQuery;
        }

        // Build Tavily query từ user message.
        // Showcase intent (explicit) → English showcase query targeting Reddit/GeekHack.
        // Auto-triggered (Qdrant miss + style query) → aesthetic-focused showcase query.
        // Generic → anchor với "mechanical keyboard" + extracted keywords.
        private static string BuildTavilyQuery(string userMessage, bool isAutoSearch = false)
        {
            var normalized = userMessage.Trim().ToLowerInvariant();

            // Extract layout + style hints (dùng cho cả explicit và auto)
            var styleHints = new List<string>();
            if (normalized.Contains("gaming") || normalized.Contains("game"))
                styleHints.Add("gaming");
            if (normalized.Contains("office") || normalized.Contains("văn phòng") || normalized.Contains("van phong"))
                styleHints.Add("office");
            if (normalized.Contains("fullsize") || normalized.Contains("full size") || normalized.Contains("full-size"))
                styleHints.Add("fullsize");
            if (normalized.Contains("tkl") || normalized.Contains("tenkeyless"))
                styleHints.Add("tenkeyless");
            if (normalized.Contains("75%") || normalized.Contains("75 percent"))
                styleHints.Add("75%");
            if (normalized.Contains("65%") || normalized.Contains("65 percent"))
                styleHints.Add("65%");
            if (normalized.Contains("60%") || normalized.Contains("60 percent"))
                styleHints.Add("60%");
            if (normalized.Contains("alice") || normalized.Contains("ergo"))
                styleHints.Add("alice ergonomic");
            if (normalized.Contains("pastel") || normalized.Contains("màu pastel"))
                styleHints.Add("pastel");
            if (normalized.Contains("dark") || normalized.Contains("đen") || normalized.Contains("tối"))
                styleHints.Add("dark themed");
            if (normalized.Contains("white") || normalized.Contains("trắng"))
                styleHints.Add("white");
            if (normalized.Contains("anime"))
                styleHints.Add("anime themed");
            if (normalized.Contains("cyberpunk"))
                styleHints.Add("cyberpunk");
            if (normalized.Contains("vintage") || normalized.Contains("retro"))
                styleHints.Add("vintage retro");
            if (normalized.Contains("minimalist") || normalized.Contains("tối giản"))
                styleHints.Add("minimalist");
            if (normalized.Contains("military") || normalized.Contains("quân sự"))
                styleHints.Add("military");
            if (normalized.Contains("rgb") || normalized.Contains("led"))
                styleHints.Add("RGB");
            if (normalized.Contains("thematic") || normalized.Contains("chủ đề") || normalized.Contains("chu de") || normalized.Contains("kiểu"))
                styleHints.Add("themed");

            var styleStr = styleHints.Count > 0 ? string.Join(" ", styleHints) + " " : "";

            // Explicit showcase intent (user nói "tìm mẫu", "tham khảo", "commission", etc.)
            bool isShowcaseIntent =
                normalized.Contains("mẫu") || normalized.Contains("mau") ||
                normalized.Contains("tham khảo") || normalized.Contains("tham khao") ||
                normalized.Contains("reference") || normalized.Contains("inspiration") ||
                normalized.Contains("showcase") || normalized.Contains("đặt làm") ||
                normalized.Contains("dat lam") || normalized.Contains("làm riêng") ||
                normalized.Contains("commission");

            if (isShowcaseIntent || isAutoSearch)
            {
                return $"custom {styleStr}mechanical keyboard build showcase site:reddit.com OR site:geekhack.org 2024";
            }

            // Generic keyboard query: strip Vietnamese intent phrases, keep content words
            string[] intentPhrases =
            {
                "tìm giúp tôi", "giúp tôi tìm", "tìm cho tôi",
                "bên ngoài web", "bên ngoài hệ thống", "ngoài web", "ngoài hệ thống",
                "tìm trên mạng", "tìm trên web", "tìm ngoài",
                "để tôi đưa cho các shop", "để đặt làm", "để commission",
                "ảnh tham khảo", "hình tham khảo"
            };

            var cleaned = userMessage;
            foreach (var phrase in intentPhrases)
                cleaned = cleaned.Replace(phrase, " ", StringComparison.OrdinalIgnoreCase);

            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned.Trim(), @"\s+", " ").Trim();

            return string.IsNullOrWhiteSpace(cleaned)
                ? "custom mechanical keyboard build showcase"
                : $"mechanical keyboard {cleaned}";
        }

        private static string BuildWebSearchRagContext(WebSearchResult webResult, string query)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"=== WEB SEARCH RESULTS for: \"{query}\" ===");
            sb.AppendLine("(These are reference builds from the internet — use for inspiration and pricing guidance only)");
            sb.AppendLine();

            for (int i = 0; i < webResult.Sources.Count; i++)
            {
                var src = webResult.Sources[i];
                sb.AppendLine($"[{i + 1}] {src.Title}");
                sb.AppendLine($"    URL: {src.Url}");
                var excerpt = src.Content.Length > 400 ? src.Content[..400] + "..." : src.Content;
                sb.AppendLine($"    Content: {excerpt}");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        // Wrapper DTO để serialize cả Items lẫn SourceLinks vào RecommendationPayload
        private class ChatMessagePayload
        {
            public List<AIRecommendationItemDTO> Items { get; set; } = new();
            public List<WebSearchSourceLink> SourceLinks { get; set; } = new();
            public bool UsedWebSearch { get; set; }
            public decimal EstimatedPrice { get; set; }
        }

        private static ResponseLanguage DetermineResponseLanguage(string? prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt))
            {
                return ResponseLanguage.English;
            }

            var normalized = prompt.Trim().ToLowerInvariant();

            // Vietnamese unicode ranges (ASCII-safe escapes) cover accented letters.
            const string vietnameseUnicodePattern = "[\\u0102\\u0103\\u00C2\\u00E2\\u0110\\u0111\\u00CA\\u00EA\\u00D4\\u00F4\\u01A0\\u01A1\\u01AF\\u01B0\\u1EA0-\\u1EF9]";
            if (Regex.IsMatch(normalized, vietnameseUnicodePattern))
            {
                return ResponseLanguage.Vietnamese;
            }

            // Vietnamese prompts without accents.
            string[] vietnameseHints =
            {
                "ban phim",
                "van phong",
                "go van phong",
                "toi can",
                "cho toi",
                "tu van",
                "de xuat",
                "linh kien",
                "duoc khong",
                "giup toi"
            };

            return vietnameseHints.Any(k => normalized.Contains(k, StringComparison.Ordinal))
                ? ResponseLanguage.Vietnamese
                : ResponseLanguage.English;
        }

        // Extract IDs sản phẩm đã được hiển thị trong assistant messages trước.
        // Dùng cho alternatives: nếu user nói "phím khác đi", "chán quá" → exclude các ID này
        // khỏi candidates để AI không lặp lại đề xuất cũ.
        private static HashSet<Guid> ExtractShownProductIds(IEnumerable<AIChatMessage> history)
        {
            var ids = new HashSet<Guid>();
            foreach (var msg in history)
            {
                if (msg.Role != "assistant" || string.IsNullOrEmpty(msg.RecommendationPayload))
                    continue;
                try
                {
                    var payload = JsonSerializer.Deserialize<ChatMessagePayload>(msg.RecommendationPayload);
                    if (payload?.Items != null)
                    {
                        foreach (var item in payload.Items)
                        {
                            if (item.Id != Guid.Empty) ids.Add(item.Id);
                        }
                    }
                }
                catch
                {
                    // Try legacy format: payload was raw items list
                    try
                    {
                        var legacyItems = JsonSerializer.Deserialize<List<AIRecommendationItemDTO>>(msg.RecommendationPayload);
                        if (legacyItems != null)
                        {
                            foreach (var item in legacyItems)
                            {
                                if (item.Id != Guid.Empty) ids.Add(item.Id);
                            }
                        }
                    }
                    catch { /* ignore */ }
                }
            }
            return ids;
        }

        // Web search runtime note: framing là DESIGN INSPIRATION cho Commission Request,
        // KHÔNG phải product alternative ở shop khác. Web search results dùng để extract
        // design elements (layout, theme, switch sound, color scheme) → mô tả cho shop khi commission.
        private static string BuildWebSearchInspirationRuntimeNote(bool isAuto)
        {
            var prefix = isAuto
                ? "\n\n[RUNTIME — DESIGN INSPIRATION (auto)]: Platform không có sản phẩm khớp yêu cầu này. AI đã tự tìm các build keyboard từ cộng đồng (Reddit, GeekHack...) làm cảm hứng thiết kế. "
                : "\n\n[RUNTIME — DESIGN INSPIRATION]: User muốn tham khảo các mẫu bên ngoài để lấy ý tưởng thiết kế. Context bên dưới là các custom build từ cộng đồng keyboard. ";

            return prefix +
                "MỤC ĐÍCH: web search results = NGUỒN CẢM HỨNG THIẾT KẾ để user mô tả khi tạo Commission Request trên AMK Collective. " +
                "KHÔNG PHẢI product alternative để user đi mua ở shop khác. " +
                "All ID fields MUST be null. TotalEstimatedPrice là estimate cho mức giá build tương tự khi commission, không phải giá ở shop ngoài. " +
                "In Reasoning: " +
                "(1) Mô tả 2-3 build từ kết quả web về CÁC YẾU TỐ THIẾT KẾ: layout (60%/65%/75%/alice...), case material/color, mounting style, switch sound profile, keycap theme. " +
                "(2) Highlight design elements user có thể NÓI VỚI SHOP để recreate: 'kiểu Alice gasket', 'theme dark cyberpunk', 'switch tactile silent', 'keycap PBT pastel'... " +
                "(3) HƯỚNG DẪN: vào Commission Request trên AMK Collective, mô tả các yếu tố thiết kế trên + ngân sách → shop sẽ build cho. " +
                "TUYỆT ĐỐI KHÔNG: " +
                "  • Không gợi ý user đi mua ở shop khác (nShop, GEARVN, Phong Cách Xanh, Tinh Tế, Shopee, Lazada, etc.) " +
                "  • Không paste link ngoài hoặc tên website mua hàng " +
                "  • Không nói 'có thể mua tại...', chỉ nói 'tham khảo thiết kế' " +
                "  • Không mention Canva, Freepik, Google Images, hay design tool nào.";
        }

        // Strip newlines + list markers (1., 2., -, •, *) khỏi Reasoning để text gọn 1 đoạn.
        // Áp dụng khi items đã hiển thị làm cards — list trong text bị duplicate và xấu.
        private static readonly Regex ListMarkerRegex = new(
            @"(?:^|\s)(?:\d+[\.\)]|[-•*])\s+",
            RegexOptions.Multiline | RegexOptions.Compiled);

        private static string CleanListFormatting(string? reasoning)
        {
            if (string.IsNullOrWhiteSpace(reasoning)) return reasoning ?? string.Empty;
            // Replace newlines + list markers với space, collapse multiple spaces.
            var noMarkers = ListMarkerRegex.Replace(reasoning, " ");
            var noNewlines = noMarkers.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ");
            // Collapse multiple spaces
            var collapsed = Regex.Replace(noNewlines, @"\s{2,}", " ");
            return collapsed.Trim();
        }

        // Generic fallback message khi không có sản phẩm fit hoặc Reasoning bị reject.
        // Đặt ở 1 chỗ để consistent giữa các code path khác nhau.
        private static string BuildNoMatchFallbackMessage() =>
            "Hiện tại AMK Collective chưa có sản phẩm khớp chính xác với yêu cầu của bạn. " +
            "Bạn có thể: (1) nói \"tìm mẫu trên web\" để tôi tìm các build tham khảo từ internet, " +
            "hoặc (2) tạo Commission Request mô tả layout, loại switch, ngân sách mong muốn — các shop sẽ báo giá riêng theo yêu cầu.";

        // Detect tên brand bàn phím external phổ biến. Dùng làm safety net khi LLM hallucinate
        // tên sản phẩm không có trong context (Keychron K6, Akko 3068, etc.).
        // Word-boundary regex để tránh false positive (ví dụ "akko" không match trong "akkord").
        private static readonly Regex ExternalBrandRegex = new(
            @"\b(keychron|akko|gmmk|ducky|leopold|varmilo|womier|leobog|filco|royal\s*kludge|rk\s*royal|drop\s+alt|drop\s+ctrl|nuphy|epomaker|ajazz|monsgeek|mode\s+sonnet|mode\s+envoy|qk65|qk75|tofu|bakeneko|krush|luminkey|wuque|class65|owlab|matrix\s+8xv|cstc40|m0lly|zoom65|zoom75|lily58|sofle|corne|nshop|gearvn|phong\s*cách\s*xanh|tinh\s*tế|shopee|lazada|tiki|sendo)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static bool ContainsExternalBrandMention(string? reasoning)
        {
            if (string.IsNullOrWhiteSpace(reasoning)) return false;
            return ExternalBrandRegex.IsMatch(reasoning);
        }

        // Detect product-claim patterns: VND prices hoặc "shop X" mentions trong Reasoning.
        // Khi items rỗng + có pattern này → AI đang mention sản phẩm cụ thể mà không pick được ID.
        // Đây là hallucination mismatch (text nói có product, payload không có).
        private static readonly Regex VndPriceRegex = new(
            @"\b\d{1,3}([.,]\d{3}){1,3}\s*(vnd|đồng|đ\b|d\b)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex ShopMentionRegex = new(
            @"(?:từ|của|tại|từ\s*shop|của\s*shop)\s+shop\s+\w+|shop\s+[A-Z][\wÀ-ỹ]+",
            RegexOptions.Compiled);

        private static bool ContainsProductClaim(string? reasoning)
        {
            if (string.IsNullOrWhiteSpace(reasoning)) return false;
            return VndPriceRegex.IsMatch(reasoning) || ShopMentionRegex.IsMatch(reasoning);
        }

        // Hard constraints parsed deterministically from user message.
        // Code-side filtering — KHÔNG để LLM tự đánh giá, vì LLM bịa lý do reject khi context không fit.
        private sealed class UserConstraints
        {
            public decimal? BudgetMin { get; set; }
            public decimal? BudgetMax { get; set; }
            public bool HasBudget => BudgetMin.HasValue || BudgetMax.HasValue;
        }

        // Parse budget từ message. Hỗ trợ:
        //   "1 triệu", "1tr", "500k", "1-2 triệu", "1 đến 2 triệu", "1 tới 2 triệu", "từ 1 đến 2 triệu"
        //   "tầm 1 triệu" / "khoảng 1 triệu" → range ±30%
        //   "dưới 2 triệu" → max only; "trên 1 triệu" → min only
        private static UserConstraints ExtractUserConstraints(string? message)
        {
            var c = new UserConstraints();
            if (string.IsNullOrWhiteSpace(message)) return c;
            var m = message.ToLowerInvariant();

            // Range pattern trước: "X-Y triệu", "X đến Y triệu", "X tới Y triệu"
            var rangeRe = new Regex(
                @"(\d+(?:[\.,]\d+)?)\s*(?:-|–|—|đến|den|tới|toi|to)\s*(\d+(?:[\.,]\d+)?)\s*(triệu|trieu|tr|m|k|nghìn|nghin|ngàn|ngan)\b",
                RegexOptions.IgnoreCase);
            var rangeMatch = rangeRe.Match(m);
            if (rangeMatch.Success)
            {
                var a = ParseAmount(rangeMatch.Groups[1].Value, rangeMatch.Groups[3].Value);
                var b = ParseAmount(rangeMatch.Groups[2].Value, rangeMatch.Groups[3].Value);
                if (a.HasValue && b.HasValue)
                {
                    c.BudgetMin = Math.Min(a.Value, b.Value);
                    c.BudgetMax = Math.Max(a.Value, b.Value);
                    return c;
                }
            }

            // Single amount: "X triệu" / "Xtr" / "Xk" / "Xm"
            var singleRe = new Regex(
                @"(\d+(?:[\.,]\d+)?)\s*(triệu|trieu|tr|m|k|nghìn|nghin|ngàn|ngan)\b",
                RegexOptions.IgnoreCase);
            var matches = singleRe.Matches(m);
            if (matches.Count == 0) return c;

            // 2+ amounts không có "đến/tới" → coi như range theo thứ tự
            if (matches.Count >= 2)
            {
                var amounts = matches.Cast<Match>()
                    .Select(x => ParseAmount(x.Groups[1].Value, x.Groups[2].Value))
                    .Where(x => x.HasValue).Select(x => x!.Value).ToList();
                if (amounts.Count >= 2)
                {
                    c.BudgetMin = amounts.Min();
                    c.BudgetMax = amounts.Max();
                    return c;
                }
            }

            var only = ParseAmount(matches[0].Groups[1].Value, matches[0].Groups[2].Value);
            if (!only.HasValue) return c;

            bool hasUnder = m.Contains("dưới") || m.Contains("duoi") || m.Contains("không quá") || m.Contains("khong qua") || m.Contains("under") || m.Contains("max");
            bool hasOver = m.Contains("trên") || m.Contains("tren") || m.Contains("ít nhất") || m.Contains("it nhat") || m.Contains("over") || m.Contains("min") || m.Contains("từ ");
            bool hasApprox = m.Contains("tầm") || m.Contains("tam") || m.Contains("khoảng") || m.Contains("khoang") || m.Contains("around") || m.Contains("about");

            if (hasUnder)
                c.BudgetMax = only.Value;
            else if (hasOver)
                c.BudgetMin = only.Value;
            else if (hasApprox)
            {
                c.BudgetMin = only.Value * 0.7m;
                c.BudgetMax = only.Value * 1.3m;
            }
            else
            {
                // Single number không qualifier → assume "max around this"
                c.BudgetMin = only.Value * 0.5m;
                c.BudgetMax = only.Value * 1.2m;
            }
            return c;
        }

        private static decimal? ParseAmount(string numStr, string unit)
        {
            numStr = numStr.Replace(",", ".");
            if (!decimal.TryParse(numStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var num))
                return null;
            return unit.ToLowerInvariant() switch
            {
                "triệu" or "trieu" or "tr" or "m" => num * 1_000_000m,
                "k" or "nghìn" or "nghin" or "ngàn" or "ngan" => num * 1_000m,
                _ => num
            };
        }

        // Returns true nếu query mang tính style/aesthetic mà platform khó có sẵn → auto web search hợp lý.
        // Chỉ dùng khi Qdrant đã miss để tránh trigger web search không cần thiết.
        private static bool HasAestheticStyleQuery(string message)
        {
            var m = message.ToLowerInvariant();
            string[] styleKeywords =
            {
                "anime", "cyberpunk", "pastel", "vintage", "retro",
                "minimalist", "tối giản", "toi gian",
                "aesthetic", "phong cách", "phong cach",
                "chủ đề", "chu de", "themed", "thematic",
                "military", "quân sự", "quan su",
                "sakura", "ocean", "forest", "space", "galaxy",
                "dark mode", "all black", "all white",
                "custom design", "mẫu thiết kế", "mau thiet ke",
                "concept", "unique", "độc đáo", "doc dao",
                "pink", "beige", "cream", "đỏ", "xanh", "vàng",
                "rgb", "led", "lighting",
                "kiểu", "kieu"
            };
            return styleKeywords.Any(k => m.Contains(k));
        }

        // Returns true nếu user đang hỏi alternatives hoặc bày tỏ không hài lòng về đề xuất trước.
        // Dùng để (1) force LLM đa dạng hóa recommendation, (2) exclude products đã shown trước đó.
        private static bool IsAskingForAlternatives(string message)
        {
            var m = message.ToLowerInvariant();

            string[] explicitAlternativePhrases =
            {
                // Yêu cầu rõ ràng "cái khác" / "loại khác" / "mẫu khác" / "phím khác" / "sản phẩm khác"
                "cái khác", "cai khac", "loại khác", "loai khac",
                "mẫu khác", "mau khac", "phím khác", "phim khac",
                "sản phẩm khác", "san pham khac", "đề xuất khác", "de xuat khac",
                "gợi ý khác", "goi y khac", "lựa chọn khác", "lua chon khac",
                "khác đi", "khac di", "khác không", "khac khong",
                "khác đê", "khac de", "khác đêi",
                // Còn lại nào / có gì nữa
                "còn loại", "con loai", "còn mẫu", "con mau",
                "còn cái", "con cai", "còn phím", "con phim",
                "còn gì", "con gi", "còn lại", "con lai",
                "không còn", "khong con", "hết rồi", "het roi",
                "không có nào nữa", "khong co nao nua",
                "không có loại khác", "khong co loai khac",
                "thay thế", "thay the",
                // Tiếng Anh
                "alternative", "different", "another",
                "something else", "what else", "more options", "any other",
            };
            if (explicitAlternativePhrases.Any(k => m.Contains(k)))
                return true;

            // Emotional dissatisfaction về đề xuất trước (chán/xấu/không thích) thường đi kèm yêu cầu đổi.
            // Chỉ trigger khi message ngắn (<60 char) để tránh false positive trong câu dài có nhiều ngữ cảnh.
            string[] dissatisfactionSignals = {
                "chán", "chan", "ngán", "ngan",
                "xấu", "xau", "không đẹp", "khong dep", "tệ", "te",
                "không thích", "khong thich", "ghét", "ghet",
                "không ổn", "khong on", "không được", "khong duoc",
                "không hay", "khong hay",
                "boring", "ugly", "bad", "not good"
            };
            if (m.Length < 60 && dissatisfactionSignals.Any(k => m.Contains(k)))
                return true;

            // Pattern hỏi tu từ kiểu "đâu phím nào ổn đâu?", "có phím nào ổn không?"
            // — tín hiệu user không hài lòng và muốn xem cái khác
            if (m.Contains("đâu") && (m.Contains("ổn") || m.Contains("on") || m.Contains("hay") || m.Contains("đẹp")))
                return true;

            return false;
        }

        // Returns true nếu user yêu cầu loại switch cụ thể.
        private static bool HasSwitchTypeRequirement(string message)
        {
            var m = message.ToLowerInvariant();
            string[] switchRequirements =
            {
                "silent", "yên tĩnh", "yen tinh", "im lặng", "im lang", "không ồn", "khong on",
                "clicky", "tactile", "linear",
                "red switch", "blue switch", "brown switch", "black switch",
                "gateron", "cherry", "kailh", "boba", "akko switch", "huano",
                "lube", "lubed"
            };
            return switchRequirements.Any(k => m.Contains(k));
        }

        // Returns true nếu description của assembled product đề cập đến switch — tức là có thể verify.
        private static bool ProductHasSwitchInfo(string? description)
        {
            if (string.IsNullOrWhiteSpace(description)) return false;
            var d = description.ToLowerInvariant();
            string[] switchKeywords =
            {
                "switch", "silent", "clicky", "tactile", "linear",
                "gateron", "cherry", "kailh", "boba", "akko", "huano",
                "red", "blue", "brown" // common switch color names in descriptions
            };
            return switchKeywords.Any(k => d.Contains(k));
        }

        // Returns true nếu user yêu cầu layout cụ thể nhưng product layout không khớp.
        // Chỉ check khi user mention rõ layout — không reject nếu user không nói gì về layout.
        private static bool IsLayoutMismatch(string userMessage, string? productLayout)
        {
            if (string.IsNullOrWhiteSpace(productLayout)) return false;

            var msg = userMessage.ToLowerInvariant();
            var layout = productLayout.Trim().ToLowerInvariant();

            // Map từ keyword user nói → layout thực tế
            var layoutRequirements = new Dictionary<string, Func<string, bool>>
            {
                ["full size"] = l => l != "100%",
                ["fullsize"] = l => l != "100%",
                ["full-size"] = l => l != "100%",
                ["100%"] = l => l != "100%",
                ["tkl"] = l => l != "tkl" && l != "tenkeyless" && l != "80%",
                ["tenkeyless"] = l => l != "tkl" && l != "tenkeyless" && l != "80%",
                ["80%"] = l => l != "80%" && l != "tkl",
                ["75%"] = l => l != "75%",
                ["65%"] = l => l != "65%",
                ["60%"] = l => l != "60%",
                ["40%"] = l => l != "40%",
            };

            foreach (var (keyword, isMismatch) in layoutRequirements)
            {
                if (msg.Contains(keyword) && isMismatch(layout))
                    return true;
            }

            return false;
        }
    }
}
