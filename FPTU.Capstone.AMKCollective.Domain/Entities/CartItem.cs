using FPTU.Capstone.AMKCollective.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Domain.Entities
{
    public class CartItem : BaseEntity
    {
        public Guid CartId { get; set; }
        public Guid ProductId { get; set; }

        public int Quantity { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }

        public bool IsCustom { get; set; } = false;

        public string? CustomConfig { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? NegotiatedPrice { get; set; } 

        public string? AdminNote { get; set; } 

        public CartItemStatus Status { get; set; } = CartItemStatus.Normal;

        // Navigation Properties
        public virtual Cart Cart { get; set; } = null!;

        [ForeignKey("ProductId")]
        public virtual Model Product { get; set; } = null!;
    }
}
