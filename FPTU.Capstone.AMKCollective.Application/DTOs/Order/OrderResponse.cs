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
        public decimal SystemDiscountAmount { get; set; }
        public decimal TotalAmount { get; set; }

        /// <summary>
        /// Phí platform thu trên đơn hàng — itemize để biết mỗi đơn shop đóng bao nhiêu cho sàn.
        /// Set lúc checkout, không đổi sau đó. Đơn vị: VND.
        /// </summary>
        public decimal PlatformFeeAmount { get; set; }

        // 3. Nhóm thông tin người nhận (Bắt buộc cho trang Chi tiết đơn hàng)
        public string ReceiverName { get; set; } = string.Empty; 
        public string ReceiverPhone { get; set; } = string.Empty; 
        public string ShippingAddress { get; set; } = string.Empty; 
        public DateTime? ExpectedDeliveryDate { get; set; }
        public string? Note { get; set; }
        public bool HasCancelRequest { get; set; }
        public bool HasWarrantyRequest { get; set; }

        // 5. Thông tin hủy đơn (chỉ có giá trị khi OrderStatus = Cancelled)
        public string? CancelledBy { get; set; }   // "Shop" | "Customer" | "System"
        public string? CancelReason { get; set; }

        // 6. Thời gian
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Ngày ghi nhận doanh thu — set khi đơn qua bảo hành và tiền được release từ HeldBalance sang Balance.
        /// Dùng cho báo cáo kế toán theo "ngày ghi nhận doanh thu". Null nếu đơn chưa được release.
        /// </summary>
        public DateTime? RevenueRecognizedAt { get; set; }

        public List<OrderItemResponse> OrderItems { get; set; } = new();
    }
}
