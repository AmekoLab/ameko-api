using FPTU.Capstone.AMKCollective.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Wallet
{
    public class WalletTransactionDetailResponse : WalletTransactionResponse
    {
        public decimal BalanceBeforeTransaction { get; set; }
        //public decimal BalanceAfterTransaction { get; set; }
        public decimal HeldBalanceBeforeTransaction { get; set; }
        public decimal HeldBalanceAfterTransaction { get; set; }
        public Guid? RelatedOrderId { get; set; }
        public Guid? OrderGroupId { get; set; }
        TransactionMetadata? Metadata { get; set; }
    }
}
