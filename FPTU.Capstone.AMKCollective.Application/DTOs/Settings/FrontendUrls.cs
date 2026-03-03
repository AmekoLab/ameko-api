using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Settings
{
    public class FrontendUrls
    {
        public string PaymentSuccessPath { get; set; } = string.Empty;
        public string PaymentCancelPath { get; set; } = string.Empty;
        public string OrderPendingPath { get; set; } = string.Empty;
        public string DepositSuccessPath { get; set; } = string.Empty;
        public string DepositCancelPath { get; set; } = string.Empty;
    }
}
