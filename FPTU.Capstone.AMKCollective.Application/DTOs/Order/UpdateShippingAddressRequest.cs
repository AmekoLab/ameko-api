using System.ComponentModel.DataAnnotations;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Order
{
    public class UpdateShippingAddressRequest
    {
        [Required]
        public string ReceiverName { get; set; } = string.Empty;

        [Required]
        public string ReceiverPhone { get; set; } = string.Empty;

        [Required]
        public string ShippingAddress { get; set; } = string.Empty;
    }
}
