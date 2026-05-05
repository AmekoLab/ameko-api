using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Part
{
    public class GetPartsFilterRequest
    {
        public Guid? ShopId { get; set; }
        public Guid CategoryId { get; set; }
        public string? PartType { get; set; }
        public bool? IsActive { get; set; }
        /// <summary>Nếu true, chỉ lấy parts được đánh dấu IsAddonEligible. Dùng bởi addon-options query.</summary>
        public bool? IsAddonEligible { get; set; }
        /// <summary>Nếu true, chỉ lấy kit đã có cấu hình builder (có KitDesignOptions).</summary>
        public bool? RequireBuilderConfig { get; set; }
        public string? SearchTerm { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public bool? IsDeleted { get; set; }
    }
}
