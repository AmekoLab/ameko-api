using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs
{
    //response
    public class CategoryDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? ThumbnailURL { get; set; }
        public Guid? ParentId { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public List<CategoryDto>? SubCategories { get; set; }
    }

    public class CategoryListDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? ThumbnailURL { get; set; }
        public Guid? ParentId { get; set; }
        public bool IsActive { get; set; }
        public int SubCategoryCount { get; set; }
        public int PartCount { get; set; }
    }

    public class PartInCategoryDto //
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? ThumbnailURL { get; set; }
        public string PartType { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int StockQuantity { get; set; }
        public Guid ShopId { get; set; }
        public string? ShopName { get; set; }
    }

    //request
    public class CreateCategoryRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? ThumbnailURL { get; set; }
        public Guid? ParentId { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class UpdateCategoryRequest
    {
        public string? Name { get; set; }
        public string? Slug { get; set; }
        public string? ThumbnailURL { get; set; }
        public Guid? ParentId { get; set; }
        public bool? IsActive { get; set; }
    }

    //query
    public class CategoryQueryParams
    {
        public bool? IsActive { get; set; }
        public Guid? ParentId { get; set; }
        public bool IncludeSubCategories { get; set; } = false;
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public class PartQueryParams
    {
        public Guid? ShopId { get; set; }
        public Guid CategoryId { get; set; }
        public string? PartType { get; set; }
        public bool? IsActive { get; set; }
        public string? SearchTerm { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
}
