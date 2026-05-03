namespace FPTU.Capstone.AMKCollective.Application.DTOs.Order
{
    public class MyPaymentHistoryFilterRequest : OrderBaseFilterRequest
    {
        /// <summary>Pending | Paid | Failed | Refunded | Released</summary>
        public string? PaymentStatus { get; set; }
        /// <summary>CreditCard | Wallet | VnPay</summary>
        public string? PaymentMethod { get; set; }
    }
}
