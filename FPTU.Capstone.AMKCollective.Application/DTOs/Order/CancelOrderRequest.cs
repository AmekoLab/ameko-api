using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Order
{
    public class CancelOrderRequest
    {
        public string Reason { get; set; } = string.Empty;
    }
}
