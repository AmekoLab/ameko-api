using System;
using System.Collections.Generic;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.AssembledProduct
{
    public class AssembledProductResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? View3DUrl { get; set; }
        public decimal Price { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class AssembledProductDetailResponse : AssembledProductResponse
    {
        public List<ProductAssembledDetailResponse> Details { get; set; } = new List<ProductAssembledDetailResponse>();
    }

    public class ProductAssembledDetailResponse
    {
        public Guid Id { get; set; }
        public Guid BaseKitId { get; set; }
        public string BaseKitName { get; set; } = string.Empty;
        public Guid ComponentId { get; set; }
        public string ComponentName { get; set; } = string.Empty;
        public int Quantity { get; set; }
    }

    public class CreateAssembledProductRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? View3DUrl { get; set; }
        public decimal Price { get; set; }
        public List<ProductAssembledDetailRequest> Details { get; set; } = new List<ProductAssembledDetailRequest>();
    }

    public class ProductAssembledDetailRequest
    {
        public Guid BaseKitId { get; set; }
        public Guid ComponentId { get; set; }
        public int Quantity { get; set; }
    }

    public class UpdateAssembledProductRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? View3DUrl { get; set; }
        public decimal Price { get; set; }
    }
}
