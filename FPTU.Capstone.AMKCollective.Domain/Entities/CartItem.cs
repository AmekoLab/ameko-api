using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Domain.Entities
{
    public class CartItem : BaseEntity
    {
        public Guid CartId { get; set; }

        public Guid? ProductId { get; set; }
        public Guid? AssembledProductId { get; set; }

        public int Quantity { get; set; }
        public bool IsCustom { get; set; } = false;

        // Lưu trữ toàn bộ thông tin linh kiện custom (switch, keycap...) dưới dạng JSON
        public string? DesignConfig { get; set; }

        // KHÔNG lưu UnitPrice, TotalPrice ở đây. Giá sẽ được tính Real-time.

        // Navigation Properties
        public virtual Cart Cart { get; set; } = null!;
        public virtual Model? Product { get; set; }
        public virtual AssembledProduct? AssembledProduct { get; set; }
    }
}
