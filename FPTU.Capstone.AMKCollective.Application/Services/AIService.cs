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

        private readonly IUnitOfWork _unitOfWork;
        private readonly IEmbeddingService _embeddingService;
        private readonly IQdrantService _qdrantService;
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
            AutoMapper.IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _embeddingService = embeddingService;
            _qdrantService = qdrantService;
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
                var candidateShops = await _qdrantService.SearchAsync("shops", promptVector, limit: 1);
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
                var buildIds = await _qdrantService.SearchAsync("builds", promptVector, limit: 5, shopId: targetShopId);
                assembledCandidates = (await _unitOfWork.AssembledProducts.GetByIdsAsync(buildIds)).ToList();

                contextData = string.Join(
                    "\n",
                    assembledCandidates.Select(b =>
                        $"ID: {b.Id}, Name: {b.Name}, Price: {b.Price}, Layout: {b.Layout}, Mounting: {b.Mounting}, Connection: {b.Connection}, Description: {b.Description}"));
            }
            else
            {
                // Filter by ShopId if identified or provided
                var partIds = await _qdrantService.SearchAsync("parts", promptVector, limit: 5, shopId: targetShopId);
                partCandidates = (await _unitOfWork.Models.GetByIdsAsync(partIds)).ToList();

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
            var ids = await _qdrantService.SearchAsync("shops", vector, limit);
            if (!ids.Any()) return Enumerable.Empty<FPTU.Capstone.AMKCollective.Application.DTOs.Shop.ShopResponse>();
            var shops = await _unitOfWork.Shops.GetByIdsAsync(ids);
            return _mapper.Map<IEnumerable<FPTU.Capstone.AMKCollective.Application.DTOs.Shop.ShopResponse>>(shops);
        }

        public async Task<IEnumerable<FPTU.Capstone.AMKCollective.Application.DTOs.AssembledProduct.AssembledProductResponse>> SearchBuildsAsync(string query, int limit = 10)
        {
            var vector = await _embeddingService.GenerateEmbeddingAsync(query);
            var ids = await _qdrantService.SearchAsync("builds", vector, limit);
            if (!ids.Any()) return Enumerable.Empty<FPTU.Capstone.AMKCollective.Application.DTOs.AssembledProduct.AssembledProductResponse>();
            var builds = await _unitOfWork.AssembledProducts.GetByIdsAsync(ids);
            return _mapper.Map<IEnumerable<FPTU.Capstone.AMKCollective.Application.DTOs.AssembledProduct.AssembledProductResponse>>(builds);
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
            if (!systemPrompt.Contains("json", StringComparison.OrdinalIgnoreCase))
            {
                systemPrompt += " IMPORTANT OUTPUT RULES: Return ONLY valid JSON.";
            }

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

            var partById = candidateParts.ToDictionary(x => x.Id, x => x);
            var missingPartIds = selectedPartIds.Where(id => !partById.ContainsKey(id)).ToList();

            if (missingPartIds.Count > 0)
            {
                var fetched = await _unitOfWork.Models.GetByIdsAsync(missingPartIds);
                foreach (var model in fetched)
                {
                    partById[model.Id] = model;
                }
            }

            foreach (var id in selectedPartIds)
            {
                if (!partById.TryGetValue(id, out var model))
                {
                    continue;
                }

                items.Add(MapPartItem(model));
            }

            // Build assembled-product card if returned by AI.
            if (recommendation.AssembledProductId.HasValue)
            {
                var assembled = await _unitOfWork.AssembledProducts.GetByIdWithDetailsAsync(recommendation.AssembledProductId.Value);
                if (assembled != null && !assembled.IsDeleted)
                {
                    items.Insert(0, MapAssembledItem(assembled));
                }
            }

            return items;
        }

        private static AIRecommendationItemDTO MapPartItem(Model model)
        {
            return new AIRecommendationItemDTO
            {
                Id = model.Id,
                RecommendationKind = "part",
                Name = model.Name,
                Price = model.Price,
                ImageUrl = FirstNonEmpty(model.ThumbnailURL, model.DefaultLayerImageUrl),
                DetailPath = $"/shop/product/{model.Id}",
                ShopId = model.ShopId,
                ShopName = model.Shop?.ShopName,
                ShopAvatarUrl = model.Shop?.LogoUrl
            };
        }

        private static AIRecommendationItemDTO MapAssembledItem(AssembledProduct assembled)
        {
            var shop = assembled.ProductAssembledDetails?
                .Select(d => d.BaseKit?.Shop)
                .FirstOrDefault(s => s != null);

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
    }
}
