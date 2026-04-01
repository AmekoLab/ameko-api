using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Shop
{
    public class ReputationTrendDto
    {
        public string Date { get; set; } = string.Empty;
        public int Score { get; set; }
        public string Badge { get; set; } = string.Empty;
    }
}
