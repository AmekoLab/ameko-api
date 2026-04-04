using System;
using System.ComponentModel.DataAnnotations;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Commission
{
    public class UpdateCommissionRequest
    {
        [Required(ErrorMessage = "Title must not be empty.")]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Detailed description (formula/requirement) must not be empty")]
        public string Description { get; set; } = string.Empty;

        public string? ReferenceImages { get; set; }

        public decimal? MinBudget { get; set; }

        public decimal? MaxBudget { get; set; }

        [Range(1, 1000, ErrorMessage = "Quantity must be greater than 0.")]
        public int Quantity { get; set; } = 1;

        public Guid? TargetedShopId { get; set; }

        [Range(1, 168, ErrorMessage = "Shop response window must be between 1 and 168 hours.")]
        public int? ShopResponseWindowHours { get; set; }

        [Range(1, 168, ErrorMessage = "Customer response window must be between 1 and 168 hours.")]
        public int? CustomerResponseWindowHours { get; set; }
    }
}
