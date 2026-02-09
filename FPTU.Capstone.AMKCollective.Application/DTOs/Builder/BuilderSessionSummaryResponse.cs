using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Builder
{
        public class BuilderSessionSummaryResponse
    {
        public Guid SessionId { get; set; }
        public Guid BaseKitId { get; set; }
        public string BaseKitName { get; set; } = string.Empty;
        public string? BaseKitThumbnail { get; set; }

        public string CurrentStep { get; set; } = string.Empty;
        public decimal TotalPrice { get; set; }
        public string? CurrentPreviewImage { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }

        // Status hiển thị (Computed)
        public string Status => ExpiresAt > DateTime.UtcNow ? "Active" : "Expired";
    }
}
