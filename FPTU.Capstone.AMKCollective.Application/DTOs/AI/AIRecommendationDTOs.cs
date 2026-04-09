using System;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.AI
{
    public class AIRecommendationRequestDTO
    {
        public string UserPrompt { get; set; } = string.Empty;

        /// <summary>
        /// Optional: Limit the recommendation to parts from a specific shop.
        /// </summary>
        public Guid? ShopId { get; set; }

        /// <summary>
        /// Optional: Context about the current base kit if the user is already looking at one.
        /// </summary>
        public Guid? BaseKitId { get; set; }
    }

    public class AIRecommendationResponseDTO
    {
        public Guid? KitId { get; set; }
        public Guid? SwitchId { get; set; }
        public Guid? KeycapId { get; set; }
        public string Reasoning { get; set; } = string.Empty;
        public decimal TotalEstimatedPrice { get; set; }
    }
}
