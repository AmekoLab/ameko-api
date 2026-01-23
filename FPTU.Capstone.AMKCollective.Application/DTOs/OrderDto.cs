using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs
{
    public class CheckoutRequest
    {
        [Required]
        public string ReceiverName { get; set; } = string.Empty; // Thống nhất dùng tên này
        [Required]
        public string ReceiverPhone { get; set; } = string.Empty;
        [Required]
        public string ShippingAddress { get; set; } = string.Empty;

        public string? Note { get; set; }

        // List item từ giỏ hàng
        public List<CheckoutItemDto> Items { get; set; } = new();

        public string SuccessUrl { get; set; } = string.Empty;
        public string CancelUrl { get; set; } = string.Empty;
    }

    public class CheckoutItemDto
    {
        public Guid ProductId { get; set; }
        public Guid ShopId { get; set; } // Cần để chia đơn
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }

        // Custom
        public bool IsCustom { get; set; }
        public List<Guid>? CustomComponentIds { get; set; } // List ID component
    }

    // ================== OUTPUT (Trả về sau khi checkout) ==================
    public class CheckoutResponse
    {
        public Guid OrderGroupId { get; set; }
        public decimal TotalAmount { get; set; }
        public string PaymentUrl { get; set; } = string.Empty;
    }

    // ================== VIEW (Xem lịch sử đơn hàng) ==================
    public class OrderGroupDto
    {
        public Guid Id { get; set; }
        public decimal TotalGroupAmount { get; set; }
        public string PaymentStatus { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<OrderDto> Orders { get; set; } = new();
    }

    public class OrderDto
    {
        public Guid Id { get; set; }
        public string ShopName { get; set; } 
        public string OrderStatus { get; set; }
        public decimal SubTotal { get; set; } 
        public decimal ShippingFee { get; set; } 
        public decimal DiscountAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public List<OrderItemDto> OrderItems { get; set; } = new();
    }

    public class OrderItemDto
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } // Tên SP
        public string ProductImage { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }

        public bool IsCustom { get; set; }
        public List<Guid>? CustomComponentIds { get; set; } // Đã giải nén JSON
        public List<OrderItemComponentDto> OrderItemComponents { get; set; }
    }

    public class AddToCartRequest
    {
        public Guid ProductId { get; set; }
        public int Quantity { get; set; }
        public bool IsCustom { get; set; } = false;
        public List<Guid>? CustomComponentIds { get; set; }
        public Guid? BuilderSessionId { get; set; }
    }
    public class OrderItemComponentDto
    {
        public Guid PartId { get; set; }
        public string PartName { get; set; }
        public decimal PartPriceSnapshot { get; set; }
        public string PartImageUrl { get; set; }
        public int Quantity { get; set; }
    }
}