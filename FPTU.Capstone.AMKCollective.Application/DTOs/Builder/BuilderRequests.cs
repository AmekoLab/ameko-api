using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Builder
{
    /// <summary>
    /// Request để validate cấu hình build
    /// </summary>
    public class ValidateConfigRequest
    {
        [Required]
        public Guid BaseKitId { get; set; }
        public List<Guid> SelectedComponentIds { get; set; } = new List<Guid>();
    }

    /// <summary>
    /// Request để bắt đầu một session mới (Start Build)
    /// </summary>
    public class BuilderStartRequest
    {
        [Required]
        public Guid BaseKitId { get; set; }
    }

    /// <summary>
    /// Request khi người dùng chọn 1 linh kiện (mỗi cú click)
    /// </summary>
    public class BuilderSelectRequest
    {
        [Required]
        public Guid SessionId { get; set; }

        [Required]
        public Guid SelectedPartId { get; set; }

        [Required]
        public string StepName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Query tìm linh kiện tương thích
    /// </summary>
    public class CompatiblePartsQuery
    {
        public Guid BaseKitId { get; set; }
        public string? PartType { get; set; }
        public string? SearchTerm { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    /// <summary>
    /// Request tạo kit option (form-data)
    /// </summary>
    public class CreateKitOptionRequest
    {
        public Guid BaseKitId { get; set; }
        public Guid ComponentId { get; set; }
        public string StepName { get; set; } = string.Empty;
        public int StepOrder { get; set; }
        public bool IsDefault { get; set; }
        public IFormFile? LayerImageFile { get; set; }
    }

    /// <summary>
    /// Request validate builder configuration
    /// </summary>
    public class ValidateBuilderRequest
    {
        public Guid BaseKitId { get; set; }
        public List<Guid> ComponentIds { get; set; } = new List<Guid>();
    }
}
