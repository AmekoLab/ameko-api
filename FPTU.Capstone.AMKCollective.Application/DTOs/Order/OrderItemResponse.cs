using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Order
{
    public class OrderItemResponse
    {
        public Guid OrderItemId { get; set; }
        public Guid? ProductId { get; set; }
        public Guid? AssembledProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string ProductImage { get; set; } = string.Empty;
        public Guid ShopId { get; set; }
        public string ShopName { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
        public decimal ShopAllocatedDiscount { get; set; }
        public decimal SystemAllocatedDiscount { get; set; }
        public decimal AllocatedDiscount { get; set; }
        public decimal FinalPrice { get; set; }
        public string ItemStatus { get; set; } = "Active";
        public bool IsCustom { get; set; }
        public string? Note { get; set; }
        public List<Guid>? CustomComponentIds { get; set; }
        public List<OrderItemComponentDto> OrderItemComponents { get; set; } = new();

        // Chỉ có giá trị khi IsCustom = true.
        // = UnitPrice - tổng giá các add-on components → bằng đúng baseKit.Price tại thời điểm checkout.
        public decimal? BaseKitPriceSnapshot { get; set; }

        /// <summary>True khi shop của item này bị ban/inactive — FE hiển thị warning và block checkout.</summary>
        public bool IsShopUnavailable { get; set; }
        public string? ShopUnavailableReason { get; set; }
    }
}
