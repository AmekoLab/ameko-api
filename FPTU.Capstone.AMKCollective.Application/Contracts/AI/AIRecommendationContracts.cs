using System;

namespace FPTU.Capstone.AMKCollective.Application.Contracts.AI
{
    public class AIRecommendationRequestDTO
    {
        public string UserPrompt { get; set; } = string.Empty;

        /// <summary>
        /// Optional: Limit the recommendation to parts from a specific shop.
        /// </summary>
        public Guid? ShopId { get; set; }

        /// <summary>
        /// Optional: Context for part-based recommendation (custom builder flow).
        /// </summary>
        public Guid? BaseKitId { get; set; }

        /// <summary>
        /// Optional: Context for assembled-product recommendation flow.
        /// </summary>
        public Guid? AssembledProductId { get; set; }
    }

    public class AIRecommendationResponseDTO
    {
        /// <summary>
        /// Legacy fields retained for backward compatibility.
        /// </summary>
        public Guid? KitId { get; set; }
        public Guid? SwitchId { get; set; }
        public Guid? KeycapId { get; set; }

        /// <summary>
        /// Optional id when AI recommends a full assembled product.
        /// </summary>
        public Guid? AssembledProductId { get; set; }

        public string Reasoning { get; set; } = string.Empty;
        public decimal TotalEstimatedPrice { get; set; }

        /// <summary>
        /// Rich metadata for FE cards/detail routing.
        /// </summary>
        public List<AIRecommendationItemDTO> Items { get; set; } = new();
    }

    public class AIRecommendationItemDTO
    {
        public Guid Id { get; set; }
        public string RecommendationKind { get; set; } = string.Empty; // part | assembled
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string? ImageUrl { get; set; }
        public string? DetailPath { get; set; }

        public Guid? ShopId { get; set; }
        public string? ShopName { get; set; }
        public string? ShopAvatarUrl { get; set; }
    }
}