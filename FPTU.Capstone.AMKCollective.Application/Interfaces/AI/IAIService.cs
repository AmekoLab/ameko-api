using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FPTU.Capstone.AMKCollective.Application.Contracts.AI;
using FPTU.Capstone.AMKCollective.Domain.Entities;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.AI
{
    public interface IAIService
    {
        /// <summary>
        /// Generates a personalized keyboard build recommendation based on user input and context.
        /// </summary>
        Task<AIRecommendationResponseDTO> GetRecommendationAsync(AIRecommendationRequestDTO request);

        /// <summary>
        /// Performs a semantic search for shops.
        /// </summary>
        Task<IEnumerable<FPTU.Capstone.AMKCollective.Application.DTOs.Shop.ShopResponse>> SearchShopsAsync(string query, int limit = 10);

        /// <summary>
        /// Performs a semantic search for assembled products (builds).
        /// </summary>
        Task<IEnumerable<FPTU.Capstone.AMKCollective.Application.DTOs.AssembledProduct.AssembledProductResponse>> SearchBuildsAsync(string query, int limit = 10);

        /// <summary>
        /// Synchronizes all eligible entities to Qdrant (Admin tool).
        /// </summary>
        Task SyncAllEntitiesToQdrantAsync();

        Task SyncShopAsync(ShopProfile shop);
        Task SyncBuildAsync(AssembledProduct build);
        Task SyncPartAsync(Model part);

        /// <summary>
        /// Analyzes an order issue (cancellation/refund) and provides an AI recommendation.
        /// </summary>
        Task<string?> AnalyzeOrderIssueAsync(OrderIssue issue, Order order);
    }
}
