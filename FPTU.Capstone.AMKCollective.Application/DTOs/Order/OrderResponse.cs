using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Order
{
    public class OrderResponse
    {
        public Guid OrderId { get; set; }

        // 1. Nhóm thông tin Shop & Group (Để navigate hoặc hiển thị avatar)
        public Guid OrderGroupId { get; set; } 
        public Guid ShopId { get; set; }       
        public string ShopName { get; set; } = string.Empty;
        public string? ShopAvatar { get; set; }

        // 2. Nhóm trạng thái & Tiền
        public string OrderStatus { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = string.Empty; 
        public decimal SubTotal { get; set; }
        public decimal ShippingFee { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TotalAmount { get; set; }

        // 3. Nhóm thông tin người nhận (Bắt buộc cho trang Chi tiết đơn hàng)
        public string ReceiverName { get; set; } = string.Empty; 
        public string ReceiverPhone { get; set; } = string.Empty; 
        public string ShippingAddress { get; set; } = string.Empty; 
        public string? Note { get; set; }
        public bool HasCancelRequest { get; set; }
        // 4. Thời gian
        public DateTime CreatedAt { get; set; } 

        public List<OrderItemResponse> OrderItems { get; set; } = new();
    }
}
