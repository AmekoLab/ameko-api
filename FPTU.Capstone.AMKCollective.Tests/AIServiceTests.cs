using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FPTU.Capstone.AMKCollective.Application.Contracts.AI;
using FPTU.Capstone.AMKCollective.Application.DTOs.Part;
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

namespace FPTU.Capstone.AMKCollective.Tests
{
    // ===================================================================
    // Helper: FakeHttpMessageHandler
    // ===================================================================

    public class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>>? _asyncHandler;

        public FakeHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> asyncHandler)
        {
            _asyncHandler = asyncHandler;
        }

        /// <summary>
        /// Creates a handler that always returns the same response.
        /// </summary>
        public static FakeHttpMessageHandler WithFixedResponse(HttpResponseMessage response)
            => new(_ => Task.FromResult(response));

        /// <summary>
        /// Creates a handler from a synchronous factory (called per-request).
        /// </summary>
        public static FakeHttpMessageHandler WithFactory(Func<HttpResponseMessage> factory)
            => new(_ => Task.FromResult(factory()));

        /// <summary>
        /// Creates a handler from a synchronous factory that can also throw exceptions.
        /// </summary>
        public static FakeHttpMessageHandler WithThrowableFactory(Func<HttpResponseMessage> factory)
            => new(_ =>
            {
                var result = factory(); // May throw
                return Task.FromResult(result);
            });

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _asyncHandler!(request);
        }
    }

    // ===================================================================
    // 1. AISettings Configuration Tests
    // ===================================================================

    public class AISettingsTests
    {
        [Fact]
        public void AISettings_DefaultValues_AreCorrect()
        {
            var settings = new AISettings();

            Assert.Equal(string.Empty, settings.GoogleApiKey);
            Assert.Equal(string.Empty, settings.GroqApiKey);
            Assert.Equal(string.Empty, settings.OpenAiApiKey);
            Assert.Equal("https://api.groq.com/openai/v1/chat/completions", settings.GroqUrl);
            Assert.Equal("https://api.openai.com/v1/chat/completions", settings.OpenAiUrl);
            Assert.Equal("gemini-embedding-exp-03-07", settings.GoogleModelName);
            Assert.NotNull(settings.Qdrant);
        }

        [Fact]
        public void QdrantSettings_DefaultValues_AreCorrect()
        {
            var qdrant = new QdrantSettings();

            Assert.Equal(string.Empty, qdrant.Url);
            Assert.Equal(string.Empty, qdrant.ApiKey);
            Assert.Equal(6334, qdrant.Port);
        }

        [Fact]
        public void AISettings_CanOverrideDefaults()
        {
            var settings = new AISettings
            {
                GoogleApiKey = "test-google-key",
                GroqApiKey = "test-groq-key",
                OpenAiApiKey = "test-openai-key",
                GroqUrl = "https://custom-groq.test/api",
                OpenAiUrl = "https://custom-openai.test/api",
                GoogleModelName = "custom-model-v2",
                Qdrant = new QdrantSettings
                {
                    Url = "https://qdrant.test:6334",
                    ApiKey = "qdrant-key",
                    Port = 9999
                }
            };

            Assert.Equal("test-google-key", settings.GoogleApiKey);
            Assert.Equal("custom-model-v2", settings.GoogleModelName);
            Assert.Equal("https://qdrant.test:6334", settings.Qdrant.Url);
            Assert.Equal(9999, settings.Qdrant.Port);
        }

        [Fact]
        public void AISettings_FallbackDefaults_WhenJsonIsMissing()
        {
            var settings = new AISettings
            {
                GoogleApiKey = "my-key",
                GroqApiKey = "groq-key",
            };

            Assert.Equal("https://api.groq.com/openai/v1/chat/completions", settings.GroqUrl);
            Assert.Equal("https://api.openai.com/v1/chat/completions", settings.OpenAiUrl);
            Assert.Equal("gemini-embedding-exp-03-07", settings.GoogleModelName);
        }
    }

    // ===================================================================
    // 2. AIService Unit Tests
    // ===================================================================

    public class AIServiceTests
    {
        private readonly Mock<IUnitOfWork> _unitOfWork = new();
        private readonly Mock<IEmbeddingService> _embeddingService = new();
        private readonly Mock<IQdrantService> _qdrantService = new();
        private readonly Mock<ILogger<AIService>> _logger = new();
        private readonly AISettings _settings = new()
        {
            GroqApiKey = "test-groq-key",
            OpenAiApiKey = "test-openai-key",
        };

        private AIService CreateService(HttpMessageHandler? handler = null)
        {
            var httpClient = new HttpClient(handler ?? FakeHttpMessageHandler.WithFixedResponse(
                new HttpResponseMessage(HttpStatusCode.InternalServerError)));
            return new AIService(
                _unitOfWork.Object,
                _embeddingService.Object,
                _qdrantService.Object,
                httpClient,
                Options.Create(_settings),
                _logger.Object);
        }

        // Helper to build a fake successful LLM response
        private static HttpResponseMessage MakeLLMResponse(string jsonContent)
            => new(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    choices = new[] { new { message = new { content = jsonContent } } }
                }))
            };

        // ----- SyncShopAsync Tests -----

        [Fact]
        public async Task SyncShopAsync_GeneratesEmbedding_AndUpsertsToQdrant()
        {
            var shopId = Guid.NewGuid();
            var shop = new ShopProfile { Id = shopId, ShopName = "AMK Store", Bio = "Custom keyboards", Rating = 4.5 };
            var expectedVector = new float[] { 0.1f, 0.2f, 0.3f };

            _embeddingService.Setup(x => x.GenerateEmbeddingAsync("AMK Store Custom keyboards"))
                .ReturnsAsync(expectedVector);
            _unitOfWork.Setup(x => x.Shops.UpdateAsync(shop, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _unitOfWork.Setup(x => x.CommitAsync()).Returns(Task.CompletedTask);

            var service = CreateService();
            await service.SyncShopAsync(shop);

            _embeddingService.Verify(x => x.GenerateEmbeddingAsync("AMK Store Custom keyboards"), Times.Once);

            _qdrantService.Verify(x => x.UpsertPointAsync(
                "shops",
                shopId,
                expectedVector,
                It.Is<Dictionary<string, object>>(d =>
                    d["type"].ToString() == "shop" &&
                    d["name"].ToString() == "AMK Store" &&
                    (double)d["rating"] == 4.5
                )), Times.Once);

            Assert.Equal(JsonSerializer.Serialize(expectedVector), shop.Embedding);
            _unitOfWork.Verify(x => x.Shops.UpdateAsync(shop, It.IsAny<CancellationToken>()), Times.Once);
            _unitOfWork.Verify(x => x.CommitAsync(), Times.Once);
        }

        [Fact]
        public async Task SyncBuildAsync_GeneratesEmbedding_AndUpsertsToQdrant()
        {
            var buildId = Guid.NewGuid();
            var build = new AssembledProduct
            {
                Id = buildId,
                Name = "AMK68 Pro",
                Description = "Wireless keyboard",
                Layout = "65%",
                Mounting = "Gasket",
                PCB = "Hot-swap",
                Connection = "BT 5.0",
                Battery = "4000mAh",
                Price = 2500000m
            };
            var expectedVector = new float[] { 0.4f, 0.5f, 0.6f };
            var expectedText = "AMK68 Pro Wireless keyboard 65% Gasket Hot-swap BT 5.0 4000mAh";

            _embeddingService.Setup(x => x.GenerateEmbeddingAsync(expectedText)).ReturnsAsync(expectedVector);
            _unitOfWork.Setup(x => x.AssembledProducts.UpdateAsync(It.IsAny<AssembledProduct>())).Returns(Task.CompletedTask);
            _unitOfWork.Setup(x => x.CommitAsync()).Returns(Task.CompletedTask);

            var service = CreateService();
            await service.SyncBuildAsync(build);

            _embeddingService.Verify(x => x.GenerateEmbeddingAsync(expectedText), Times.Once);
            _qdrantService.Verify(x => x.UpsertPointAsync(
                "builds", buildId, expectedVector,
                It.Is<Dictionary<string, object>>(d =>
                    d["type"].ToString() == "build" &&
                    d["name"].ToString() == "AMK68 Pro" &&
                    (double)d["price"] == 2500000.0
                )), Times.Once);

            Assert.Equal(JsonSerializer.Serialize(expectedVector), build.Embedding);
        }

        [Fact]
        public async Task SyncPartAsync_GeneratesEmbedding_WithShopIdInPayload()
        {
            var partId = Guid.NewGuid();
            var shopId = Guid.NewGuid();
            var part = new Model
            {
                Id = partId, Name = "Gateron Yellow Pro", Description = "Linear switch",
                PartType = "Switch", Specifications = "45g", Price = 15000m, ShopId = shopId
            };
            var expectedVector = new float[] { 0.7f, 0.8f, 0.9f };

            _embeddingService.Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>())).ReturnsAsync(expectedVector);
            _unitOfWork.Setup(x => x.Models.UpdateEmbeddingAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _unitOfWork.Setup(x => x.CommitAsync()).Returns(Task.CompletedTask);

            var service = CreateService();
            await service.SyncPartAsync(part);

            _qdrantService.Verify(x => x.UpsertPointAsync(
                "parts", partId, expectedVector,
                It.Is<Dictionary<string, object>>(d =>
                    d["shop_id"].ToString() == shopId.ToString() &&
                    d["part_type"].ToString() == "Switch"
                )), Times.Once);
        }

        [Fact]
        public async Task SyncPartAsync_NullPartType_UsesEmptyString()
        {
            var part = new Model
            {
                Id = Guid.NewGuid(), Name = "Unknown", PartType = null,
                Price = 5000m, ShopId = Guid.NewGuid()
            };

            _embeddingService.Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>())).ReturnsAsync(new float[] { 0.1f });
            _unitOfWork.Setup(x => x.Models.UpdateEmbeddingAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _unitOfWork.Setup(x => x.CommitAsync()).Returns(Task.CompletedTask);

            var service = CreateService();
            await service.SyncPartAsync(part);

            _qdrantService.Verify(x => x.UpsertPointAsync(
                "parts", part.Id, It.IsAny<float[]>(),
                It.Is<Dictionary<string, object>>(d => d["part_type"].ToString() == "")
            ), Times.Once);
        }

        [Fact]
        public async Task SyncPartAsync_EmbeddingIsSavedToEntity()
        {
            var expectedVector = new float[] { 0.123f, 0.456f, 0.789f };
            var part = new Model { Id = Guid.NewGuid(), Name = "Test", ShopId = Guid.NewGuid(), Price = 100m };

            _embeddingService.Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>())).ReturnsAsync(expectedVector);
            _unitOfWork.Setup(x => x.Models.UpdateEmbeddingAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _unitOfWork.Setup(x => x.CommitAsync()).Returns(Task.CompletedTask);

            var service = CreateService();
            await service.SyncPartAsync(part);

            Assert.NotNull(part.Embedding);
            var deserialized = JsonSerializer.Deserialize<float[]>(part.Embedding!);
            Assert.Equal(expectedVector, deserialized);
        }

        // ----- SearchAsync Tests -----

        [Fact]
        public async Task SearchShopsAsync_ReturnsQdrantResults()
        {
            var query = "premium acrylic keyboard";
            var vector = new float[] { 0.1f, 0.2f };
            var expectedIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };

            _embeddingService.Setup(x => x.GenerateEmbeddingAsync(query)).ReturnsAsync(vector);
            _qdrantService.Setup(x => x.SearchAsync("shops", vector, It.IsAny<int>(), It.IsAny<Guid?>())).ReturnsAsync(expectedIds);

            var service = CreateService();
            var result = await service.SearchShopsAsync(query);

            Assert.Equal(expectedIds, result);
        }

        [Fact]
        public async Task SearchBuildsAsync_UsesBuildsCollection()
        {
            var query = "wireless 65%";
            var vector = new float[] { 0.5f };
            var expectedIds = new List<Guid> { Guid.NewGuid() };

            _embeddingService.Setup(x => x.GenerateEmbeddingAsync(query)).ReturnsAsync(vector);
            _qdrantService.Setup(x => x.SearchAsync("builds", vector, It.IsAny<int>(), It.IsAny<Guid?>())).ReturnsAsync(expectedIds);

            var service = CreateService();
            var result = await service.SearchBuildsAsync(query, 5);

            Assert.Single(result);
        }

        [Fact]
        public async Task SearchShopsAsync_ReturnsEmpty_WhenQdrantHasNoMatches()
        {
            _embeddingService.Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>())).ReturnsAsync(new float[] { 0.1f });
            _qdrantService.Setup(x => x.SearchAsync("shops", It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<Guid?>()))
                .ReturnsAsync(new List<Guid>());

            var service = CreateService();
            var result = await service.SearchShopsAsync("nonexistent");

            Assert.Empty(result);
        }

        // ----- SyncAllEntitiesToQdrantAsync -----

        [Fact]
        public async Task SyncAllEntities_SyncsAllEntityTypes()
        {
            var shop = new ShopProfile { Id = Guid.NewGuid(), ShopName = "S1", Bio = "B1" };
            var build = new AssembledProduct { Id = Guid.NewGuid(), Name = "B1", Price = 100m };
            var part = new Model { Id = Guid.NewGuid(), Name = "P1", Price = 50m, ShopId = Guid.NewGuid() };

            _unitOfWork.Setup(x => x.Shops.GetShopsAsync(It.IsAny<string?>(), It.IsAny<ShopStatus?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync((new List<ShopProfile> { shop }, 1));
            _unitOfWork.Setup(x => x.AssembledProducts.GetAllPagedAsync(It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync((new List<AssembledProduct> { build }, 1));
            _unitOfWork.Setup(x => x.Models.GetPagedAsync(It.IsAny<GetPartsFilterRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync((new List<Model> { part }, 1));
            _embeddingService.Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>())).ReturnsAsync(new float[] { 0.1f });
            _unitOfWork.Setup(x => x.Shops.UpdateAsync(It.IsAny<ShopProfile>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _unitOfWork.Setup(x => x.AssembledProducts.UpdateAsync(It.IsAny<AssembledProduct>())).Returns(Task.CompletedTask);
            _unitOfWork.Setup(x => x.Models.UpdateEmbeddingAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _unitOfWork.Setup(x => x.CommitAsync()).Returns(Task.CompletedTask);

            var service = CreateService();
            await service.SyncAllEntitiesToQdrantAsync();

            _qdrantService.Verify(x => x.UpsertPointAsync("shops", shop.Id, It.IsAny<float[]>(), It.IsAny<Dictionary<string, object>>()), Times.Once);
            _qdrantService.Verify(x => x.UpsertPointAsync("builds", build.Id, It.IsAny<float[]>(), It.IsAny<Dictionary<string, object>>()), Times.Once);
            _qdrantService.Verify(x => x.UpsertPointAsync("parts", part.Id, It.IsAny<float[]>(), It.IsAny<Dictionary<string, object>>()), Times.Once);
        }

        // ----- AnalyzeOrderIssueAsync Tests -----

        [Fact]
        public async Task AnalyzeOrderIssueAsync_ReturnsAIResponse_WithExpectedFields()
        {
            var aiResponse = JsonSerializer.Serialize(new
            {
                Category = "Buyer's Remorse",
                Sentiment = "Neutral",
                Summary = "Customer wants to cancel.",
                Recommendation = "Approve",
                ConfidenceScore = 0.85
            });

            var handler = FakeHttpMessageHandler.WithFixedResponse(MakeLLMResponse(aiResponse));
            var service = CreateService(handler);

            var order = new Order
            {
                Id = Guid.NewGuid(),
                OrderStatus = OrderStatus.Processing,
                TotalAmount = 5000000m,
                OrderItems = new List<OrderItem>
                {
                    new() { ProductName = "AMK68 Wireless", Quantity = 1 },
                    new() { ProductName = "Gateron Yellow", Quantity = 2 }
                }
            };
            var issue = new OrderIssue
            {
                Type = OrderIssueType.CancelRequest,
                Reason = "Changed my mind",
                Description = "Found cheaper option"
            };

            var result = await service.AnalyzeOrderIssueAsync(issue, order);

            Assert.NotNull(result);
            var parsed = JsonSerializer.Deserialize<JsonElement>(result!);
            Assert.Equal("Approve", parsed.GetProperty("Recommendation").GetString());
            Assert.Equal("Buyer's Remorse", parsed.GetProperty("Category").GetString());
            Assert.True(parsed.GetProperty("ConfidenceScore").GetDouble() > 0);
        }

        [Fact]
        public async Task AnalyzeOrderIssueAsync_ReturnsNull_WhenNoKeysConfigured()
        {
            var emptySettings = new AISettings { GroqApiKey = "", OpenAiApiKey = "" };
            var service = new AIService(
                _unitOfWork.Object, _embeddingService.Object, _qdrantService.Object,
                new HttpClient(), Options.Create(emptySettings), _logger.Object);

            var order = new Order
            {
                Id = Guid.NewGuid(), OrderStatus = OrderStatus.Processing, TotalAmount = 100m,
                OrderItems = new List<OrderItem> { new() { ProductName = "Test", Quantity = 1 } }
            };
            var issue = new OrderIssue { Type = OrderIssueType.CancelRequest, Reason = "Test" };

            var result = await service.AnalyzeOrderIssueAsync(issue, order);
            Assert.Null(result);
        }

        [Fact]
        public async Task AnalyzeOrderIssueAsync_FallsBackToOpenAI_WhenGroqFails()
        {
            var aiResponse = JsonSerializer.Serialize(new
            {
                Category = "Quality Issue",
                Sentiment = "Negative",
                Summary = "Defective product.",
                Recommendation = "Escalate",
                ConfidenceScore = 0.6
            });

            var callCount = 0;
            var handler = FakeHttpMessageHandler.WithFactory(() =>
            {
                callCount++;
                if (callCount == 1)
                    return new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("Error") };
                return MakeLLMResponse(aiResponse);
            });

            var service = CreateService(handler);

            var order = new Order
            {
                Id = Guid.NewGuid(), OrderStatus = OrderStatus.Shipped, TotalAmount = 3000000m,
                OrderItems = new List<OrderItem> { new() { ProductName = "Keyboard X", Quantity = 1 } }
            };
            var issue = new OrderIssue { Type = OrderIssueType.CancelRequest, Reason = "Defective" };

            var result = await service.AnalyzeOrderIssueAsync(issue, order);

            Assert.NotNull(result);
            var parsed = JsonSerializer.Deserialize<JsonElement>(result!);
            Assert.Equal("Escalate", parsed.GetProperty("Recommendation").GetString());
            Assert.Equal(2, callCount);
        }

        [Fact]
        public async Task AnalyzeOrderIssueAsync_ContextIncludesAllOrderItems()
        {
            var order = new Order
            {
                Id = Guid.NewGuid(), OrderStatus = OrderStatus.Processing, TotalAmount = 10000000m,
                OrderItems = new List<OrderItem>
                {
                    new() { ProductName = "Base Kit A", Quantity = 1 },
                    new() { ProductName = "Switch B", Quantity = 70 },
                    new() { ProductName = "Keycap C", Quantity = 1 }
                }
            };
            var issue = new OrderIssue { Type = OrderIssueType.CancelRequest, Reason = "Wrong items" };

            string? capturedContent = null;
            var handler = new FakeHttpMessageHandler(async request =>
            {
                capturedContent = await request.Content!.ReadAsStringAsync();
                return MakeLLMResponse("{\"Recommendation\":\"Approve\"}");
            });

            var service = CreateService(handler);
            await service.AnalyzeOrderIssueAsync(issue, order);

            Assert.NotNull(capturedContent);
            Assert.Contains("Base Kit A x1", capturedContent);
            Assert.Contains("Switch B x70", capturedContent);
            Assert.Contains("Keycap C x1", capturedContent);
        }

        // ----- GetRecommendationAsync -----

        [Fact]
        public async Task GetRecommendation_WithShopId_SkipsShopSearch()
        {
            var shopId = Guid.NewGuid();
            var partId = Guid.NewGuid();
            var request = new AIRecommendationRequestDTO { UserPrompt = "budget 65% keyboard", ShopId = shopId };

            _embeddingService.Setup(x => x.GenerateEmbeddingAsync(request.UserPrompt)).ReturnsAsync(new float[] { 0.1f });
            _qdrantService.Setup(x => x.SearchAsync("parts", It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<Guid?>()))
                .ReturnsAsync(new List<Guid> { partId });
            _unitOfWork.Setup(x => x.Models.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Model> { new() { Id = partId, Name = "Kit", Price = 500000, PartType = "Kit", Specifications = "65%" } });

            var aiJson = JsonSerializer.Serialize(new
            {
                KitId = partId, SwitchId = Guid.NewGuid(), KeycapId = Guid.NewGuid(),
                Reasoning = "Budget pick!", TotalEstimatedPrice = 500000
            });

            var handler = FakeHttpMessageHandler.WithFixedResponse(MakeLLMResponse(aiJson));
            var service = CreateService(handler);
            var result = await service.GetRecommendationAsync(request);

            Assert.Equal(partId, result.KitId);
            Assert.Equal("Budget pick!", result.Reasoning);

            // Shop search should NOT be called since ShopId was provided
            _qdrantService.Verify(x => x.SearchAsync("shops", It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<Guid?>()), Times.Never);
        }

        [Fact]
        public async Task GetRecommendation_ThrowsException_WhenAIFails()
        {
            _embeddingService.Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>())).ReturnsAsync(new float[] { 0.1f });
            _qdrantService.Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<Guid?>()))
                .ReturnsAsync(new List<Guid>());
            _unitOfWork.Setup(x => x.Models.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<Model>());

            var assembledRepo = new Mock<IAssembledProductRepository>();
            assembledRepo
                .Setup(x => x.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<AssembledProduct>());
            _unitOfWork.Setup(x => x.AssembledProducts).Returns(assembledRepo.Object);

            var emptySettings = new AISettings();
            var service = new AIService(
                _unitOfWork.Object, _embeddingService.Object, _qdrantService.Object,
                new HttpClient(), Options.Create(emptySettings), _logger.Object);

            await Assert.ThrowsAsync<Exception>(() =>
                service.GetRecommendationAsync(new AIRecommendationRequestDTO { UserPrompt = "test" }));
        }

        [Fact]
        public async Task GetRecommendation_InvalidGuidId_DoesNotThrow_AndMapsToNull()
        {
            var shopId = Guid.NewGuid();
            var partId = Guid.NewGuid();
            var request = new AIRecommendationRequestDTO { UserPrompt = "office keyboard", ShopId = shopId };

            _embeddingService.Setup(x => x.GenerateEmbeddingAsync(request.UserPrompt)).ReturnsAsync(new float[] { 0.2f });
            _qdrantService.Setup(x => x.SearchAsync("parts", It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<Guid?>()))
                .ReturnsAsync(new List<Guid> { partId });
            _unitOfWork.Setup(x => x.Models.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Model> { new() { Id = partId, Name = "Kit", Price = 500000, PartType = "Kit" } });

            var aiJson = JsonSerializer.Serialize(new
            {
                KitId = partId,
                SwitchId = "1",
                KeycapId = Guid.NewGuid(),
                Reasoning = "Valid reasoning",
                TotalEstimatedPrice = 123456.78m
            });

            var handler = FakeHttpMessageHandler.WithFixedResponse(MakeLLMResponse(aiJson));
            var service = CreateService(handler);

            var result = await service.GetRecommendationAsync(request);

            Assert.Equal(partId, result.KitId);
            Assert.Null(result.SwitchId);
            Assert.Equal("Valid reasoning", result.Reasoning);
            Assert.Equal(123456.78m, result.TotalEstimatedPrice);
        }

        [Fact]
        public async Task GetRecommendation_ReturnsPartItemMetadata_ForFeCardRendering()
        {
            var shopId = Guid.NewGuid();
            var partId = Guid.NewGuid();
            var request = new AIRecommendationRequestDTO { UserPrompt = "office build", ShopId = shopId };

            _embeddingService.Setup(x => x.GenerateEmbeddingAsync(request.UserPrompt)).ReturnsAsync(new float[] { 0.25f });
            _qdrantService.Setup(x => x.SearchAsync("parts", It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<Guid?>()))
                .ReturnsAsync(new List<Guid> { partId });
            _unitOfWork.Setup(x => x.Models.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Model>
                {
                    new()
                    {
                        Id = partId,
                        ShopId = shopId,
                        Name = "Office Silent Switch",
                        Price = 12000m,
                        ThumbnailURL = "https://cdn.test/switch.png",
                        PartType = "Switch",
                        Shop = new ShopProfile { Id = shopId, ShopName = "Ameko Shop", LogoUrl = "https://cdn.test/logo.png" }
                    }
                });

            var aiJson = JsonSerializer.Serialize(new
            {
                KitId = partId,
                Reasoning = "Good choice",
                TotalEstimatedPrice = 12000m
            });

            var handler = FakeHttpMessageHandler.WithFixedResponse(MakeLLMResponse(aiJson));
            var service = CreateService(handler);

            var result = await service.GetRecommendationAsync(request);

            Assert.Single(result.Items);
            Assert.Equal("part", result.Items[0].RecommendationKind);
            Assert.Equal("https://cdn.test/switch.png", result.Items[0].ImageUrl);
            Assert.Equal($"/shop/product/{partId}", result.Items[0].DetailPath);
            Assert.Equal("Ameko Shop", result.Items[0].ShopName);
            Assert.Equal("https://cdn.test/logo.png", result.Items[0].ShopAvatarUrl);
        }

        [Fact]
        public async Task GetRecommendation_WithAssembledProductId_ReturnsAssembledItemMetadata()
        {
            var assembledId = Guid.NewGuid();
            var shopId = Guid.NewGuid();
            var request = new AIRecommendationRequestDTO
            {
                UserPrompt = "full assembled keyboard",
                AssembledProductId = assembledId
            };

            _embeddingService.Setup(x => x.GenerateEmbeddingAsync(request.UserPrompt)).ReturnsAsync(new float[] { 0.3f });
            _qdrantService.Setup(x => x.SearchAsync("builds", It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<Guid?>()))
                .ReturnsAsync(new List<Guid> { assembledId });
            _unitOfWork.Setup(x => x.Models.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Model>());

            var assembledRepo = new Mock<IAssembledProductRepository>();
            assembledRepo.Setup(x => x.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync(
                new List<AssembledProduct>
                {
                    new()
                    {
                        Id = assembledId,
                        Name = "Assembled Neo65",
                        Price = 4500000m,
                        Image1 = "https://cdn.test/assembled.png"
                    }
                });
            assembledRepo.Setup(x => x.GetByIdWithDetailsAsync(assembledId)).ReturnsAsync(
                new AssembledProduct
                {
                    Id = assembledId,
                    Name = "Assembled Neo65",
                    Price = 4500000m,
                    Image1 = "https://cdn.test/assembled.png",
                    ProductAssembledDetails = new List<ProductAssembledDetail>
                    {
                        new()
                        {
                            BaseKit = new Model
                            {
                                Shop = new ShopProfile
                                {
                                    Id = shopId,
                                    ShopName = "Assembled House",
                                    LogoUrl = "https://cdn.test/shop-avatar.png"
                                }
                            }
                        }
                    }
                });
            _unitOfWork.Setup(x => x.AssembledProducts).Returns(assembledRepo.Object);

            var aiJson = JsonSerializer.Serialize(new
            {
                AssembledProductId = assembledId,
                Reasoning = "Pick this assembled build",
                TotalEstimatedPrice = 4500000m
            });

            var handler = FakeHttpMessageHandler.WithFixedResponse(MakeLLMResponse(aiJson));
            var service = CreateService(handler);

            var result = await service.GetRecommendationAsync(request);

            Assert.Equal(assembledId, result.AssembledProductId);
            Assert.Single(result.Items);
            Assert.Equal("assembled", result.Items[0].RecommendationKind);
            Assert.Equal("https://cdn.test/assembled.png", result.Items[0].ImageUrl);
            Assert.Equal($"/shop/assembled-product/{assembledId}", result.Items[0].DetailPath);
            Assert.Equal("Assembled House", result.Items[0].ShopName);
            Assert.Equal("https://cdn.test/shop-avatar.png", result.Items[0].ShopAvatarUrl);
        }

        [Fact]
        public async Task GetRecommendation_GenericPrompt_DefaultsToAssembledMode()
        {
            var assembledId = Guid.NewGuid();
            var request = new AIRecommendationRequestDTO
            {
                UserPrompt = "can you recommend a keyboard for office"
            };

            _embeddingService.Setup(x => x.GenerateEmbeddingAsync(request.UserPrompt)).ReturnsAsync(new float[] { 0.12f });
            _qdrantService.Setup(x => x.SearchAsync("builds", It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<Guid?>()))
                .ReturnsAsync(new List<Guid> { assembledId });

            var assembledRepo = new Mock<IAssembledProductRepository>();
            assembledRepo.Setup(x => x.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync(
                new List<AssembledProduct>
                {
                    new()
                    {
                        Id = assembledId,
                        Name = "Office Ready Board",
                        Price = 2500000m,
                        Image1 = "https://cdn.test/office-board.png"
                    }
                });
            assembledRepo.Setup(x => x.GetByIdWithDetailsAsync(assembledId)).ReturnsAsync(
                new AssembledProduct
                {
                    Id = assembledId,
                    Name = "Office Ready Board",
                    Price = 2500000m,
                    Image1 = "https://cdn.test/office-board.png"
                });
            _unitOfWork.Setup(x => x.AssembledProducts).Returns(assembledRepo.Object);

            var aiJson = JsonSerializer.Serialize(new
            {
                AssembledProductId = assembledId,
                Reasoning = "Assembled is better for office user",
                TotalEstimatedPrice = 2500000m
            });

            var handler = FakeHttpMessageHandler.WithFixedResponse(MakeLLMResponse(aiJson));
            var service = CreateService(handler);
            var result = await service.GetRecommendationAsync(request);

            Assert.Equal(assembledId, result.AssembledProductId);
            Assert.Single(result.Items);
            Assert.Equal("assembled", result.Items[0].RecommendationKind);

            _qdrantService.Verify(x => x.SearchAsync("builds", It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<Guid?>()), Times.Once);
            _qdrantService.Verify(x => x.SearchAsync("parts", It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<Guid?>()), Times.Never);
        }

        [Fact]
        public async Task GetRecommendation_EnglishPrompt_SendsEnglishReasoningRule()
        {
            var shopId = Guid.NewGuid();
            var partId = Guid.NewGuid();
            var request = new AIRecommendationRequestDTO
            {
                UserPrompt = "Can you recommend a keyboard for office use?",
                ShopId = shopId
            };

            _embeddingService.Setup(x => x.GenerateEmbeddingAsync(request.UserPrompt)).ReturnsAsync(new float[] { 0.11f });
            _qdrantService.Setup(x => x.SearchAsync("parts", It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<Guid?>()))
                .ReturnsAsync(new List<Guid> { partId });
            _unitOfWork.Setup(x => x.Models.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Model> { new() { Id = partId, Name = "Part", Price = 100m, ShopId = shopId } });

            string? capturedRequestBody = null;
            var handler = new FakeHttpMessageHandler(async httpRequest =>
            {
                capturedRequestBody = await httpRequest.Content!.ReadAsStringAsync();
                var aiJson = JsonSerializer.Serialize(new
                {
                    KitId = partId,
                    Reasoning = "English reasoning",
                    TotalEstimatedPrice = 100m
                });
                return MakeLLMResponse(aiJson);
            });

            var service = CreateService(handler);
            await service.GetRecommendationAsync(request);

            Assert.NotNull(capturedRequestBody);
            Assert.Contains("LANGUAGE RULE: Write the", capturedRequestBody);
            Assert.Contains("in English only", capturedRequestBody);
            Assert.Contains("Response Language: English", capturedRequestBody);
        }

        [Fact]
        public async Task GetRecommendation_VietnamesePrompt_SendsVietnameseReasoningRule()
        {
            var shopId = Guid.NewGuid();
            var partId = Guid.NewGuid();
            var request = new AIRecommendationRequestDTO
            {
                UserPrompt = "Toi can ban phim van phong",
                ShopId = shopId
            };

            _embeddingService.Setup(x => x.GenerateEmbeddingAsync(request.UserPrompt)).ReturnsAsync(new float[] { 0.11f });
            _qdrantService.Setup(x => x.SearchAsync("parts", It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<Guid?>()))
                .ReturnsAsync(new List<Guid> { partId });
            _unitOfWork.Setup(x => x.Models.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Model> { new() { Id = partId, Name = "Part", Price = 100m, ShopId = shopId } });

            string? capturedRequestBody = null;
            var handler = new FakeHttpMessageHandler(async httpRequest =>
            {
                capturedRequestBody = await httpRequest.Content!.ReadAsStringAsync();
                var aiJson = JsonSerializer.Serialize(new
                {
                    KitId = partId,
                    Reasoning = "Ly do bang tieng Viet",
                    TotalEstimatedPrice = 100m
                });
                return MakeLLMResponse(aiJson);
            });

            var service = CreateService(handler);
            await service.GetRecommendationAsync(request);

            Assert.NotNull(capturedRequestBody);
            Assert.Contains("LANGUAGE RULE: Write the", capturedRequestBody);
            Assert.Contains("in Vietnamese only", capturedRequestBody);
            Assert.Contains("Response Language: Vietnamese", capturedRequestBody);
        }
    }

    // ===================================================================
    // 3. OrderIssue Entity + DTO Tests
    // ===================================================================

    public class OrderIssueAIFieldTests
    {
        [Fact]
        public void OrderIssue_AIAnalysisResult_DefaultIsNull()
        {
            Assert.Null(new OrderIssue().AIAnalysisResult);
        }

        [Fact]
        public void OrderIssue_AIAnalysisResult_CanStoreJsonPayload()
        {
            var json = JsonSerializer.Serialize(new
            {
                Category = "Buyer Remorse",
                Recommendation = "Approve",
                ConfidenceScore = 0.9
            });

            var issue = new OrderIssue { AIAnalysisResult = json };

            Assert.NotNull(issue.AIAnalysisResult);
            var parsed = JsonSerializer.Deserialize<JsonElement>(issue.AIAnalysisResult!);
            Assert.Equal("Approve", parsed.GetProperty("Recommendation").GetString());
        }

        [Fact]
        public void OrderIssueResponse_IncludesAIAnalysisResult()
        {
            var response = new Application.DTOs.OrderIssues.OrderIssueResponse
            {
                AIAnalysisResult = "{\"Recommendation\":\"Reject\"}"
            };

            Assert.Contains("Reject", response.AIAnalysisResult!);
        }
    }

    // ===================================================================
    // 4. LLM Fallback Strategy Tests
    // ===================================================================

    public class LLMFallbackTests
    {
        private static Order MakeTestOrder() => new()
        {
            Id = Guid.NewGuid(), OrderStatus = OrderStatus.Pending, TotalAmount = 100m,
            OrderItems = new List<OrderItem> { new() { ProductName = "Item", Quantity = 1 } }
        };
        private static OrderIssue MakeTestIssue() => new() { Type = OrderIssueType.CancelRequest, Reason = "Test" };

        private static AIService CreateServiceWithSettings(AISettings settings, HttpMessageHandler? handler = null)
        {
            return new AIService(
                new Mock<IUnitOfWork>().Object,
                new Mock<IEmbeddingService>().Object,
                new Mock<IQdrantService>().Object,
                new HttpClient(handler ?? FakeHttpMessageHandler.WithFixedResponse(
                    new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(JsonSerializer.Serialize(new
                        {
                            choices = new[] { new { message = new { content = "{\"ok\":true}" } } }
                        }))
                    })),
                Options.Create(settings),
                new Mock<ILogger<AIService>>().Object);
        }

        [Fact]
        public async Task OnlyGroqConfigured_DoesNotCallOpenAI()
        {
            var callCount = 0;
            var handler = FakeHttpMessageHandler.WithFactory(() =>
            {
                callCount++;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(new
                    { choices = new[] { new { message = new { content = "{\"result\":\"groq\"}" } } } }))
                };
            });

            var service = CreateServiceWithSettings(new AISettings { GroqApiKey = "key", OpenAiApiKey = "" }, handler);
            var result = await service.AnalyzeOrderIssueAsync(MakeTestIssue(), MakeTestOrder());

            Assert.NotNull(result);
            Assert.Equal(1, callCount);
        }

        [Fact]
        public async Task OnlyOpenAIConfigured_SkipsGroq()
        {
            var callCount = 0;
            var handler = FakeHttpMessageHandler.WithFactory(() =>
            {
                callCount++;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(new
                    { choices = new[] { new { message = new { content = "{\"from\":\"openai\"}" } } } }))
                };
            });

            var service = CreateServiceWithSettings(new AISettings { GroqApiKey = "", OpenAiApiKey = "key" }, handler);
            var result = await service.AnalyzeOrderIssueAsync(MakeTestIssue(), MakeTestOrder());

            Assert.NotNull(result);
            Assert.Equal(1, callCount);
        }

        [Fact]
        public async Task NoKeysConfigured_ReturnsNull()
        {
            var service = CreateServiceWithSettings(new AISettings { GroqApiKey = "", OpenAiApiKey = "" });
            var result = await service.AnalyzeOrderIssueAsync(MakeTestIssue(), MakeTestOrder());
            Assert.Null(result);
        }

        [Fact]
        public async Task GroqThrowsException_FallsBackToOpenAI()
        {
            var callCount = 0;
            var handler = FakeHttpMessageHandler.WithFactory(() =>
            {
                callCount++;
                if (callCount == 1)
                    throw new HttpRequestException("Groq is down");
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(new
                    { choices = new[] { new { message = new { content = "{\"recovery\":\"openai\"}" } } } }))
                };
            });

            var service = CreateServiceWithSettings(
                new AISettings { GroqApiKey = "groq", OpenAiApiKey = "openai" }, handler);
            var result = await service.AnalyzeOrderIssueAsync(MakeTestIssue(), MakeTestOrder());

            Assert.NotNull(result);
            Assert.Contains("openai", result);
        }
    }
}
