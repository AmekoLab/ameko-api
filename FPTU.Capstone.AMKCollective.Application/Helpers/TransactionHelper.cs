using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Helpers
{
    public static class TransactionHelper
    {
        /// <summary>
        /// Tạo mã giao dịch có độ dài vừa phải, dễ tracking: TX-YYMMDD-XXXXXX
        /// </summary>
        public static string GenerateTxCode()
            => $"TX-{DateTime.UtcNow:yyMMdd}-{Guid.NewGuid().ToString().Substring(0, 6).ToUpper()}";
    }
}
