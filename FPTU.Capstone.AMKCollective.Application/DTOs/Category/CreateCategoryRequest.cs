using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Category
{
    public class CreateCategoryRequest
    {
        [Required]
        public string Name { get; set; } = string.Empty;
        public Guid? ParentId { get; set; }
        public bool IsActive { get; set; } = true;

        // ShopId removed - inferred from token
        // public Guid? ShopId { get; set; }

        public IFormFile? ThumbnailImage { get; set; }
    }
}
        
