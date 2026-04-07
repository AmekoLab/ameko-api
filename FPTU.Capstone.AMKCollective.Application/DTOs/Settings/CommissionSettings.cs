using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Settings
{
    public class CommissionSettings
    {
        public int QuoteValidityDays { get; set; } = 7;
        public int DefaultShopResponseHours { get; set; } = 24;
        public int DefaultCustomerResponseHours { get; set; } = 24;
        public int ReminderIntervalHours { get; set; } = 24;
        public int MaxReminderCount { get; set; } = 3;
        public int MaxActiveRequests { get; set; } = 5;
        /// <summary>
        /// Số giờ tối đa sau khi user accept quote nhưng chưa thanh toán.
        /// Hết thời gian này: xóa cart item, hủy commission, trừ reputation.
        /// </summary>
        public int PaymentWindowHours { get; set; } = 48;
    }
}
