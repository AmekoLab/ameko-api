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

        [Column(TypeName = "decimal(18,2)")]
        //CURRENCY INFORMATION
        public decimal Amount { get; set; } //total amount
        [MaxLength(3)]
        public string Currency { get; set; } = "vnd";


        //STRIPE
        [MaxLength(255)]
        public string? StripeSessionId {  get; set; }
        [MaxLength(255)]
        public string? StripePaymentIntentId { get; set; }

        //Status
        public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
        public PaymentMethod Method { get; set; } = PaymentMethod.CreditCard;

        //Billing
        [MaxLength(500)]
        public string? BillingAddress {  get; set; }
        [MaxLength(255)]
        public string? PayerEmail { get; set; }

        //Log & Note
        public string? Description {  get; set; }
        public string? FailureMessage {  get; set; }

        public PaymentType Type { get; set; } = PaymentType.OrderPayment; 
        // Navigation Properties
        public virtual OrderGroup? OrderGroup { get; set; } = null!;
        public virtual User User { get; set; }
    }
}
