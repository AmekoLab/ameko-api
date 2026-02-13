using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Wallet
{
    public class HeldTransactionResponse
    {
        public Guid TransactionId { get; set; }
        public decimal Amount { get; set; }
        public DateTime Date { get; set; }

        // Thông tin đơn hàng liên quan để Shop biết tại sao bị giữ
        public Guid? OrderId { get; set; }
        public string OrderStatus { get; set; } // Trạng thái đơn (Processing, Shipping...)
        public string Reason { get; set; } // Ví dụ: "Chờ khách hàng xác nhận"
    }
}
