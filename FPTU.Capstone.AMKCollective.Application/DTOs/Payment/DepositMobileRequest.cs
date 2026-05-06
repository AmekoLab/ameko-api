namespace FPTU.Capstone.AMKCollective.Application.DTOs.Payment
{
    public class DepositMobileRequest
    {
        /// <summary>
        /// The amount to deposit into the wallet.
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// The URL to redirect to after a successful payment. 
        /// Can be a deep link (e.g., ameko://payment/success).
        /// </summary>
        public string SuccessUrl { get; set; }

        /// <summary>
        /// The URL to redirect to after a cancelled payment.
        /// Can be a deep link (e.g., ameko://payment/cancel).
        /// </summary>
        public string CancelUrl { get; set; }
    }
}
