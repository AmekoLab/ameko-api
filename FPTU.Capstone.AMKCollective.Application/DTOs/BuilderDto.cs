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
}
