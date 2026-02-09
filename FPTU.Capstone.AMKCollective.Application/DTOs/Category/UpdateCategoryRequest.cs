using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Category
{
    public class UpdateCategoryRequest
    {
        public string? Name { get; set; }
        
        /// <summary>
        /// Only Shop can update their own private categories
        /// Shops CANNOT change ParentId to move between global/private
        /// </summary>
        public Guid? ParentId { get; set; }
        
        public bool? IsActive { get; set; }
        public IFormFile? ThumbnailImage { get; set; }
    }
}
