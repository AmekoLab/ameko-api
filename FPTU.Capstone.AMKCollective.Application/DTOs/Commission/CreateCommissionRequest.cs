using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Commission
{
    public class CreateCommissionRequest
    {
        public Guid? TargetedShopId { get; set; }

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

        public bool IsDraft { get; set; } = false;

        [Range(24, 72, ErrorMessage = "Shop response window must be between 24 and 72 hours.")]
        public int? ShopResponseWindowHours { get; set; }

        [Range(1, 72, ErrorMessage = "Customer response window must be between 1 and 72 hours.")]
        public int? CustomerResponseWindowHours { get; set; }
    }
}
