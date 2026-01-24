namespace FPTU.Capstone.AMKCollective.Application.DTOs.Builder
{
    /// <summary>
    /// Kết quả validate cấu hình
    /// </summary>
    public class ValidationResultDto
    {
        public bool IsValid { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<Guid> InvalidComponentIds { get; set; } = new List<Guid>();
    }

    /// <summary>
    /// Cấu hình builder với các bước
    /// </summary>
    public class BuilderConfigDto
    {
        public Guid BaseKitId { get; set; }
        public string BaseKitName { get; set; } = string.Empty;
        public string BaseThumbnail { get; set; } = string.Empty;
        public List<BuilderStepDto> Steps { get; set; } = new List<BuilderStepDto>();
    }

    /// <summary>
    /// Thông tin một bước trong builder
    /// </summary>
    public class BuilderStepDto
    {
        public string StepName { get; set; } = string.Empty;
        public int StepOrder { get; set; }
        public string PartType { get; set; } = string.Empty;
        public bool IsRequired { get; set; } = true;
        public List<CompatiblePartDto> Options { get; set; } = new List<CompatiblePartDto>();
    }

    /// <summary>
    /// Linh kiện tương thích có thể chọn
    /// </summary>
    public class CompatiblePartDto
    {
        public Guid PartId { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string ThumbnailUrl { get; set; } = string.Empty;
        public string LayerImageUrl { get; set; } = string.Empty;
        public bool IsDefault { get; set; }
    }

    /// <summary>
    /// Response tổng trả về cho Frontend
    /// </summary>
    public class BuilderStepResponse
    {
        public string Message { get; set; } = "Product selected";
        public BuilderSessionData Data { get; set; } = new();
    }

    /// <summary>
    /// Data của session builder
    /// </summary>
    public class BuilderSessionData
    {
        public SessionInfo Session { get; set; } = new();
        public NextStepInfo NextStep { get; set; } = new();
    }

    /// <summary>
    /// Thông tin session hiện tại
    /// </summary>
    public class SessionInfo
    {
        public Guid Id { get; set; }
        public Dictionary<string, SelectedPartDetail> Selection { get; set; } = new();
        public decimal TotalPrice { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsComplete { get; set; }
    }

    /// <summary>
    /// Chi tiết món hàng đã chọn
    /// </summary>
    public class SelectedPartDetail
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string ThumbnailUrl { get; set; } = string.Empty;
    }

    /// <summary>
    /// Thông tin bước tiếp theo
    /// </summary>
    public class NextStepInfo
    {
        public StepDetail Step { get; set; } = new();
        public List<CompatiblePartDto> Products { get; set; } = new();
    }

    /// <summary>
    /// Chi tiết bước
    /// </summary>
    public class StepDetail
    {
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public int StepOrder { get; set; }
    }

    /// <summary>
    /// Item trong session selection
    /// </summary>
    public class BuilderSessionSelectionItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string? ThumbnailUrl { get; set; }
    }
}
