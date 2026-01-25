using System.ComponentModel.DataAnnotations;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Order
{
    /// <summary>
    /// Request checkout giỏ hàng
    /// </summary>
    public class CheckoutRequest
    {
        [Required]
        public string ReceiverName { get; set; } = string.Empty;

        [Required]
        public string ReceiverPhone { get; set; } = string.Empty;

        [Required]
        public string ShippingAddress { get; set; } = string.Empty;

        public string? Note { get; set; }

        public List<CheckoutItemDto> Items { get; set; } = new();

        public string SuccessUrl { get; set; } = string.Empty;
        public string CancelUrl { get; set; } = string.Empty;
    }

    /// <summary>
    /// Item trong checkout request
    /// </summary>
    public class CheckoutItemDto
    {
        public Guid ProductId { get; set; }
        public Guid ShopId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public bool IsCustom { get; set; }
        public List<Guid>? CustomComponentIds { get; set; }
    }

    /// <summary>
    /// Request thêm vào giỏ hàng
    /// </summary>
    public class AddToCartRequest
    {
        public Guid? ProductId { get; set; }
        public int Quantity { get; set; }
        public bool IsCustom { get; set; } = false;
        public List<Guid>? CustomComponentIds { get; set; }
        public Guid? BuilderSessionId { get; set; }
    }

    /// <summary>
    /// Request cập nhật số lượng trong giỏ
    /// </summary>
    public class UpdateCartItemRequest
    {
        public Guid OrderItemId { get; set; }
        public int Quantity { get; set; }
    }

    public class CancelOrderRequest
    {
        public string Reason { get; set; } = string.Empty;
    }
}
