using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs
{
    public class AddToCartDto
    {
        [Required]
        public Guid ProductId { get; set; } 

        [Range(1, 100, ErrorMessage = "Số lượng phải ít nhất là 1")]
        public int Quantity { get; set; } = 1;

        public bool IsCustom { get; set; } = false;

        // Dùng cho Custom Builder: Danh sách ID các linh kiện đã chọn
        // Frontend gửi: ["id_switch", "id_keycap", "id_plate"]
        public List<Guid>? ComponentIds { get; set; }
    }

    public class CartItemDto
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? ProductImage { get; set; } 
        public string ProductType { get; set; } 

        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; } 
        public decimal TotalPrice => UnitPrice * Quantity; 

        public bool IsCustom { get; set; }
        public List<Guid>? CustomComponentIds { get; set; } // Parse từ JSON ra để FE dùng

        public string Status { get; set; } 
        public decimal? NegotiatedPrice { get; set; }
        public string? AdminNote { get; set; }
    }

    public class CartDto
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public decimal TotalAmount { get; set; } 
        public List<CartItemDto> Items { get; set; } = new List<CartItemDto>();
    }
}
