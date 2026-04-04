using System;
using FPTU.Capstone.AMKCollective.Domain.Enums;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Order
{
    public class UpdateOrderStatusRequest
    {
        public OrderStatus Status { get; set; }
        public DateTime? ExpectedDeliveryDate { get; set; }
    }
}
