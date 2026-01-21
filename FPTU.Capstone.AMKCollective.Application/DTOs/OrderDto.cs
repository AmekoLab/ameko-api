using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs
{
    public class CreateOrderDto
    {
        [Required]
        [MaxLength(100)]
        public string RecipientName { get; set; } = string.Empty;

        [Required]
        [Phone]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string ShippingAddress { get; set; } = string.Empty;

        public string? Note { get; set; }

        public Guid? VoucherId { get; set; }
        public string PaymentMethod { get; set; } = "Stripe";
    }

    public class OrderDto
    {
        public Guid Id { get; set; }
        public Guid OrderGroupId { get; set; } 
        public string OrderStatus { get; set; }
        public string PaymentStatus { get; set; }
        public DateTime CreatedAt { get; set; }

        public decimal TotalAmount { get; set; }
        public decimal ShippingFee { get; set; }

        public Guid ShopId { get; set; }
        public string ShopName { get; set; } = string.Empty;

        public List<OrderItemDto> OrderItems { get; set; } = new List<OrderItemDto>();
    }

    public class OrderItemDto
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string ProductImage { get; set; } = string.Empty;

        public bool IsCustom { get; set; }
        public List<Guid>? CustomComponentIds { get; set; }

        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
    }

    public class CheckoutResponseDto
    {
        public Guid OrderGroupId { get; set; }
        public decimal TotalAmount { get; set; }
        public string StripeSessionUrl { get; set; } = string.Empty; 
    }
}

