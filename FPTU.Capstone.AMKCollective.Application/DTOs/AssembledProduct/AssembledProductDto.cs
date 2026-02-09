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
        public Guid ShopId { get; set; }
        public string ShopName { get; set; } = string.Empty;
        public bool IsDeleted { get; set; }
        public string? Image1 { get; set; }
        public string? Image2 { get; set; }
        public string? Image3 { get; set; }
        public string? Description { get; set; }
        public int? Quantity { get; set; }
        public string? Layout { get; set; }
        public string? Mounting { get; set; }
        public string? PCB { get; set; }
        public string? Connection { get; set; }
        public string? Battery { get; set; }
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
        public string? SoundUrl { get; set; }
    }

    public class CreateAssembledProductRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? View3DUrl { get; set; }
        public decimal Price { get; set; }
        public List<ProductAssembledDetailRequest> Details { get; set; } = new List<ProductAssembledDetailRequest>();
        public string? Image1 { get; set; }
        public string? Image2 { get; set; }
        public string? Image3 { get; set; }
        public string? Description { get; set; }
        public int? Quantity { get; set; }
        public string? Layout { get; set; }
        public string? Mounting { get; set; }
        public string? PCB { get; set; }
        public string? Connection { get; set; }
        public string? Battery { get; set; }
    }

    public class ProductAssembledDetailRequest
    {
        public Guid BaseKitId { get; set; }
        public Guid ComponentId { get; set; }
        public int Quantity { get; set; }
        public string? SoundUrl { get; set; }
    }

    public class UpdateAssembledProductRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? View3DUrl { get; set; }
        public decimal Price { get; set; }
        public string? Image1 { get; set; }
        public string? Image2 { get; set; }
        public string? Image3 { get; set; }
        public string? Description { get; set; }
        public int? Quantity { get; set; }
        public string? Layout { get; set; }
        public string? Mounting { get; set; }
        public string? PCB { get; set; }
        public string? Connection { get; set; }
        public string? Battery { get; set; }
        public List<ProductAssembledDetailRequest>? Details { get; set; }
    }
}
