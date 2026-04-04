using System.ComponentModel.DataAnnotations;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Order
{
    public class ShopCancelOrderRequest
    {
        [Required]
        public string Reason { get; set; } = string.Empty;
    }
}
