using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Category
{
    /// <summary>
    /// Request tạo category mới (form-data)
    /// </summary>
    public class CreateCategoryRequest
    {
        [Required]
        public string Name { get; set; } = string.Empty;
        public Guid? ParentId { get; set; }
        public bool IsActive { get; set; } = true;
        public IFormFile? ThumbnailImage { get; set; }
    }

    /// <summary>
    /// Request cập nhật category (form-data)
    /// </summary>
    public class UpdateCategoryRequest
    {
        public string? Name { get; set; }
        public Guid? ParentId { get; set; }
        public bool? IsActive { get; set; }
        public IFormFile? ThumbnailImage { get; set; }
    }

    /// <summary>
    /// Query params cho danh sách category
    /// </summary>
    public class CategoryQueryParams
    {
        public bool? IsActive { get; set; }
        public Guid? ParentId { get; set; }
        public bool IncludeSubCategories { get; set; } = false;
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
}
