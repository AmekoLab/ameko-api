using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.OrderIssues
{
    public class CancelOrderRequest
    {
        public Guid OrderId { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string? Description { get; set; }
        /// <summary>
        /// Danh sách item muốn hủy. Null hoặc rỗng = hủy toàn bộ order.
        /// Chỉ được dùng khi order.OrderStatus == Pending (chưa assembly).
        /// </summary>
        public List<Guid>? ItemIds { get; set; }
    }
}
