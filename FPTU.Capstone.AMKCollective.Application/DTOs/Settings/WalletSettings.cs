using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Settings
{
    public class WalletSettings
    {
        public decimal WithdrawalFeePercent { get; set; }
        public decimal MinimumBalanceAfterWithdrawal { get; set; }
        public decimal MinimumWithdrawalAmount { get; set; }
        public int PinResetOtpExpiryMinutes { get; set; }
        public Guid SystemWalletId { get; set; }
    }
}
