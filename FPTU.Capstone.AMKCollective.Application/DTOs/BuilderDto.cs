using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs
{
    public class ValidateConfigRequest
    {
        [Required]
        public Guid BaseKitId { get; set; }
        public List<Guid> SelectedComponentIds { get; set; } = new List<Guid>();
    }
    public class ValidationResultDto
    {
        public bool IsValid { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<Guid> InvalidComponentIds { get; set; } = new List<Guid>();
    }
    public class CompatiblePartsQuery
    {
        public Guid BaseKitId { get; set; }
        public string? PartType { get; set; } 
        public string? SearchTerm { get; set; } 
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
    public class PartDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string PartType { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int StockQuantity { get; set; }
        public string ThumbnailUrl { get; set; } = string.Empty;
        public string? DefaultLayerImageUrl { get; set; } 
        public string? Description { get; set; }
        public string? Specifications { get; set; } // JSON 

        public Guid ShopId { get; set; }
        public string ShopName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
    }

    public class CreateUpdatePartDto
    {
        public Guid CategoryId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string PartType { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int StockQuantity { get; set; }
        public string? Description { get; set; }
        public string? Specifications { get; set; } // JSON string from FE

        public Stream? ImageStream { get; set; }
        public string? ImageFileName { get; set; }

        public Stream? LayerImageStream { get; set; }
        public string? LayerImageFileName { get; set; }
    }

    public class BuilderConfigDto
    {
        public Guid BaseKitId { get; set; }
        public string BaseKitName { get; set; } = string.Empty;
        public string BaseThumbnail { get; set; } = string.Empty;
        public List<BuilderStepDto> Steps { get; set; } = new List<BuilderStepDto>();
    }

    public class BuilderStepDto
    {
        public string StepName { get; set; } = string.Empty;
        public int StepOrder { get; set; }
        public string PartType { get; set; } = string.Empty; 
        public bool IsRequired { get; set; } = true;

        public List<CompatiblePartDto> Options { get; set; } = new List<CompatiblePartDto>();
    }

    public class CompatiblePartDto
    {
        public Guid PartId { get; set; } 
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string ThumbnailUrl { get; set; } = string.Empty;
        public string LayerImageUrl { get; set; } = string.Empty;
        public bool IsDefault { get; set; }
    }
    public class CreateKitOptionDto
    {
        public Guid BaseKitId { get; set; }
        public Guid ComponentId { get; set; }

        public string StepName { get; set; } = string.Empty;
        public int StepOrder { get; set; }
        public bool IsDefault { get; set; }

        public Stream? FileStream { get; set; }
        public string? FileName { get; set; }
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
        public Guid SessionId { get; set; } //  đang build dở cái nào

        [Required]
        public Guid SelectedPartId { get; set; } // ID linh kiện vừa chọn

        [Required]
        public string StepName { get; set; } = string.Empty; // VD: "case", "plate"
    }

    /// <summary>
    /// Response tổng trả về cho Frontend 
    /// </summary>
    public class BuilderStepResponse
    {
        public string Message { get; set; } = "Product selected";
        public BuilderSessionData Data { get; set; } = new();
    }

    public class BuilderSessionData
    {
        public SessionInfo Session { get; set; } = new();
        public NextStepInfo NextStep { get; set; } = new();
    }

    public class SessionInfo
    {
        public Guid Id { get; set; } // Session ID

        // Dictionary lưu các món đã chọn. Key = Tên bước (case), Value = Chi tiết món
        // Dùng object để linh hoạt hoặc dùng class SelectedPartDetail cụ thể
        public Dictionary<string, SelectedPartDetail> Selection { get; set; } = new();

        public decimal TotalPrice { get; set; }
        public DateTime UpdatedAt { get; set; }

        public bool IsComplete { get; set; } // Cờ báo hiệu đã xong hết chưa để hiện nút AddToCart
    }

    /// <summary>
    /// Chi tiết món hàng đã chọn (để hiển thị bên cột "Đã chọn")
    /// </summary>
    public class SelectedPartDetail
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string ThumbnailUrl { get; set; } = string.Empty;
    }

    public class NextStepInfo
    {
        public StepDetail Step { get; set; } = new();

        // Danh sách sản phẩm khả dụng cho bước tiếp theo (Đã được lọc tương thích)
        public List<CompatiblePartDto> Products { get; set; } = new();
    }

    public class StepDetail
    {
        public string Name { get; set; } = string.Empty; // VD: "Switch Plate"
        public string Slug { get; set; } = string.Empty; // VD: "plate"
        public int StepOrder { get; set; }
    }
    public class BuilderSessionSelectionItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
        public string? ThumbnailUrl { get; set; }
    }

    
}
