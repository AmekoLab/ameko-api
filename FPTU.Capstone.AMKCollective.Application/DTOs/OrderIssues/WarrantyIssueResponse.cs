using FPTU.Capstone.AMKCollective.Domain.Enums;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.OrderIssues
{
    public class WarrantyIssueResponse
    {
        public Guid Id { get; set; }
        public Guid OrderId { get; set; }
        public Guid UserId { get; set; }
        public OrderIssueType Type { get; set; }
        public string? TypeName { get; set; }
        public OrderIssueStatus Status { get; set; }
        public string? StatusName { get; set; }
        public string? Reason { get; set; }
        public string? Description { get; set; }
        public string? EvidenceUrl { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerAvatar { get; set; }
        public bool RequiresReturn { get; set; }
        public string? ExpectedAction { get; set; }
        public decimal RefundAmount { get; set; }
        public List<Guid>? OrderItemIds { get; set; }
        public bool IsSystemValid { get; set; }
        public string? ShopResponse { get; set; }
        public string? AdminNote { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? AIAnalysisResult { get; set; }
    }
}
