using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Shop
{
    public class BadgeHistoryDto
    {
        public string Date { get; set; } = string.Empty;
        public string NewBadge { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }
}
