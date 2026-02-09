using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Category
{
    public class GetCategoriesFilterRequest
    {
        public bool? IsActive { get; set; }
        public Guid? ParentId { get; set; }
        public bool IncludeSubCategories { get; set; } = false;
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        
        /// <summary>
        /// Filter categories by Shop ID
        /// - Null = Get only GLOBAL categories (admin created)
        /// - Specific Guid = Get PRIVATE categories for that shop
        /// </summary>
        public Guid? ShopId { get; set; }
    }
}
