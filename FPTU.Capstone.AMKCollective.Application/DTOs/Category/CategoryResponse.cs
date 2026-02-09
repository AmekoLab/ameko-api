using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Category
{
    public class CategoryResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? ThumbnailURL { get; set; }
        public Guid? ParentId { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public List<CategoryResponse>? SubCategories { get; set; }
        
        /// <summary>
        /// Shop ID - indicates ownership
        /// - Null: GLOBAL category (created by admin)
        /// - Guid value: PRIVATE category (created by shop)
        /// </summary>
        public Guid? ShopId { get; set; }
        
        /// <summary>
        /// Category type indicator
        /// - "global": Admin-managed, available to all shops
        /// - "private": Shop-specific, only for that shop
        /// </summary>
        public string CategoryType => ShopId.HasValue ? "private" : "global";
    }
}
