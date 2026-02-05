using FPTU.Capstone.AMKCollective.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Builder
{
    public class CompatiblePartResponse
    {
        public Guid PartId { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string ThumbnailUrl { get; set; } = string.Empty;
        public string LayerImageUrl { get; set; } = string.Empty;
        public bool IsDefault { get; set; }
        public StockStatus Status { get; set; } // Added field
    }
}
