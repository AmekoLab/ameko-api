using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using FPTU.Capstone.AMKCollective.Application.DTOs.AI;
using FPTU.Capstone.AMKCollective.Application.DTOs.Settings;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Application.Services;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Domain.Enums;
using FPTU.Capstone.AMKCollective.Infrastructure.ThirdParty.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace FPTU.Capstone.AMKCollective.Tests
{
    /// <summary>
    /// E2E Integration tests that verify the full AI pipeline:
    /// Text -> Google Embedding -> Qdrant Storage/Search -> Groq LLM Response -> Cleanup.
    /// </summary>
    public class AIEndToEndTests : IAsyncDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly AISettings _settings;
        private readonly GoogleEmbeddingService _embeddingService;
        private readonly QdrantService _qdrantService;
        private readonly Mock<IUnitOfWork> _unitOfWork = new();
        private readonly AIService _aiService;
        private readonly List<(string Collection, Guid Id)> _createdPoints = new();

        public AIEndToEndTests(ITestOutputHelper output)
        {
            _output = output;

            // Load configuration from the Api project's appsettings.Development.json
            var projectDir = Directory.GetCurrentDirectory();
            // Assumes running from bin/Debug/netX.0, so go up to solution root then to Api project
            var apiProjPath = Path.Combine(projectDir, "..", "..", "..", "..", "FPTU.Capstone.AMKCollective.Api");
            
            var config = new ConfigurationBuilder()
                .SetBasePath(apiProjPath)
                .AddJsonFile("appsettings.Development.json", optional: false)
                .Build();

            _settings = new AISettings();
            config.GetSection("AISettings").Bind(_settings);

            // Manual check for critical keys (to avoid opaque errors)
            if (string.IsNullOrEmpty(_settings.GoogleApiKey) || _settings.GoogleApiKey == "your_google_api_key_here")
                throw new Exception("GoogleApiKey is not valid in appsettings.Development.json");
            
            _output.WriteLine($"Using Google Model: {_settings.GoogleModelName}");
            _output.WriteLine($"Using Groq Model: {_settings.GroqModelName}");
            _output.WriteLine($"Using Qdrant URL: {_settings.Qdrant.Url}");

            var options = Options.Create(_settings);
            
            _embeddingService = new GoogleEmbeddingService(options);
            _qdrantService = new QdrantService(options);
            
            var httpClient = new HttpClient();
            var logger = new Mock<ILogger<AIService>>().Object;
            // Mock Models repository to avoid NullReferenceException in Sync methods
            _unitOfWork.Setup(x => x.Models).Returns(new Mock<IModelRepository>().Object);

            _aiService = new AIService(
                _unitOfWork.Object,
                _embeddingService,
                _qdrantService,
                httpClient,
                options,
                logger);
        }

        [Fact]
        public async Task GetRecommendation_FullPipeline_Success()
        {
            _output.WriteLine("Step 0: Ensuring 'parts' collection exists (dim=3072)...");
            await _qdrantService.EnsureCollectionExistsAsync("parts", 3072);

            _output.WriteLine("Step 1: Preparing test data directly in Qdrant...");
            
            var shopId = Guid.NewGuid();
            var kitId = Guid.NewGuid();
            var switchId = Guid.NewGuid();
            
            var kit = new Model 
            { 
                Id = kitId, 
                Name = "E2E Test Kit 65%", 
                Description = "Gasket mount acrylic case", 
                PartType = "Kit", 
                Specifications = "65% Layout, Hot-swap",
                Price = 1200000,
                ShopId = shopId
            };

            var switches = new Model
            {
                Id = switchId,
                Name = "E2E Silent Linear",
                Description = "Smooth silent switches",
                PartType = "Switch",
                Specifications = "Linear, 45g",
                Price = 15000,
                ShopId = shopId
            };

            // Sync these to Qdrant manually for the test
            await _aiService.SyncPartAsync(kit);
            await _aiService.SyncPartAsync(switches);
            
            _createdPoints.Add(("parts", kitId));
            _createdPoints.Add(("parts", switchId));

            _output.WriteLine("Step 2: Mocking DB results for those IDs...");
            _unitOfWork.Setup(x => x.Models.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(new List<Model> { kit, switches });

            _output.WriteLine("Step 3: Calling GetRecommendationAsync (E2E)...");
            var request = new AIRecommendationRequestDTO 
            { 
                UserPrompt = "I want a budget silent 65% keyboard", 
                ShopId = shopId 
            };

            var response = await _aiService.GetRecommendationAsync(request);

            _output.WriteLine("=== AI E2E Recommendation Response ===");
            _output.WriteLine($"Reasoning: {response.Reasoning}");
            _output.WriteLine($"Total Price: {response.TotalEstimatedPrice}");
            _output.WriteLine($"Kit ID: {response.KitId}");

            Assert.NotNull(response);
            Assert.NotNull(response.KitId);
            Assert.Equal(kitId, response.KitId);
            Assert.NotEmpty(response.Reasoning);
        }

        [Fact]
        public async Task AnalyzeOrderIssue_FullPipeline_Success()
        {
            _output.WriteLine("Step 1: Calling AnalyzeOrderIssueAsync (Real LLM)...");
            
            var order = new Order
            {
                Id = Guid.NewGuid(),
                OrderStatus = OrderStatus.Processing,
                TotalAmount = 2500000m,
                OrderItems = new List<OrderItem>
                {
                    new() { ProductName = "Custom Keyboard Build X", Quantity = 1 }
                }
            };

            var issue = new OrderIssue
            {
                Type = OrderIssueType.CancelRequest,
                Reason = "Wrong Color",
                Description = "I ordered Black but I need White instead. Please cancel so I can rebuy."
            };

            var analysisJson = await _aiService.AnalyzeOrderIssueAsync(issue, order);

            _output.WriteLine("=== AI E2E Issue Analysis ===");
            _output.WriteLine(analysisJson ?? "(null)");

            Assert.NotNull(analysisJson);
            
            var parsed = JsonSerializer.Deserialize<JsonElement>(analysisJson!);
            Assert.True(parsed.TryGetProperty("Recommendation", out _));
            
            var rec = parsed.GetProperty("Recommendation").GetString();
            _output.WriteLine($"Recommendation: {rec}");
            
            // For a processing order with a color mistake, AI should likely approve or escalate
            Assert.True(rec == "Approve" || rec == "Escalate");
        }

        public async ValueTask DisposeAsync()
        {
            if (!_createdPoints.Any()) return;

            _output.WriteLine("\n--- CLEANUP: Deleting test points from Qdrant ---");
            foreach (var point in _createdPoints)
            {
                try
                {
                    await _qdrantService.DeletePointAsync(point.Collection, point.Id);
                    _output.WriteLine($"Deleted point {point.Id} from collection {point.Collection}");
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"FAILED to delete point {point.Id}: {ex.Message}");
                }
            }
        }
    }
}
