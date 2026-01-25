namespace FPTU.Capstone.AMKCollective.Application.DTOs.Category
{
    /// <summary>
    /// DTO chi tiết category
    /// </summary>
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

    /// <summary>
    /// DTO danh sách category (rút gọn)
    /// </summary>
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
}
