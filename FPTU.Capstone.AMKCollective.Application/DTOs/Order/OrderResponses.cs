namespace FPTU.Capstone.AMKCollective.Application.DTOs.Order
{
    /// <summary>
    /// Response sau khi checkout thành công
    /// </summary>
    public class CheckoutResponse
    {
        public Guid OrderGroupId { get; set; }
        public decimal TotalAmount { get; set; }
        public string PaymentUrl { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO nhóm đơn hàng
    /// </summary>
    public class OrderGroupDto
    {
        public Guid Id { get; set; }
        public decimal TotalGroupAmount { get; set; }
        public string PaymentStatus { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public List<OrderDto> Orders { get; set; } = new();
    }

    /// <summary>
    /// DTO đơn hàng
    /// </summary>
    public class OrderDto
    {
        public Guid Id { get; set; }
        public string ShopName { get; set; } = string.Empty;
        public string OrderStatus { get; set; } = string.Empty;
        public decimal SubTotal { get; set; }
        public decimal ShippingFee { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public List<OrderItemDto> OrderItems { get; set; } = new();
    }

    /// <summary>
    /// DTO item trong đơn hàng
    /// </summary>
    public class OrderItemDto
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string ProductImage { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
        public bool IsCustom { get; set; }
        public List<Guid>? CustomComponentIds { get; set; }
        public List<OrderItemComponentDto> OrderItemComponents { get; set; } = new();
    }

    /// <summary>
    /// DTO component trong order item (cho custom build)
    /// </summary>
    public class OrderItemComponentDto
    {
        public Guid PartId { get; set; }
        public string PartName { get; set; } = string.Empty;
        public decimal PartPriceSnapshot { get; set; }
        public string PartImageUrl { get; set; } = string.Empty;
        public int Quantity { get; set; }
    }
}
