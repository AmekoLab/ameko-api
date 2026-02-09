using FPTU.Capstone.AMKCollective.Domain.Enums;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FPTU.Capstone.AMKCollective.Domain.Entities
{
    public class Payment : BaseEntity
    {
        public Guid? OrderGroupId { get; set; }
        public Guid UserId { get; set; }
        // Link to the single Order for tracing product revenue (SalesPending/SalesReleased)
        public Guid? RelatedOrderId { get; set; }
        public Guid? WalletId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        //CURRENCY INFORMATION
        public decimal Amount { get; set; } //total amount

        [Column(TypeName = "decimal(18,2)")]
        public decimal FeeAmount { get; set; } = 0;

        [MaxLength(3)]
        public string Currency { get; set; } = "vnd";


        //STRIPE
        [MaxLength(255)]
        public string? StripeSessionId {  get; set; }
        [MaxLength(255)]
        public string? StripePaymentIntentId { get; set; }

        //Status and Meta
        public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
        public PaymentMethod Method { get; set; } = PaymentMethod.CreditCard;
        public PaymentType Type { get; set; } = PaymentType.OrderPayment; 

        //Billing and log
        [MaxLength(500)]
        public string? BillingAddress {  get; set; }
        [MaxLength(255)]
        public string? PayerEmail { get; set; }

        //Log & Note
        public string? Description {  get; set; } // Example: "Payout for February", "Revenue from order #ABC"
        public string? FailureMessage {  get; set; }

        // Navigation Properties
        public virtual OrderGroup? OrderGroup { get; set; }
        public virtual User? User { get; set; }
        public virtual Order? RelatedOrder { get; set; }
        public virtual Wallet? Wallet { get; set; }
    }
}
