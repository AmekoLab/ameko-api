using FPTU.Capstone.AMKCollective.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Domain.Entities
{
    public class CommissionRequest :BaseEntity
    {
        public Guid UserId { get; set; }

        // Nếu user chỉ định đích danh 1 shop ngay từ đầu
        public Guid? TargetedShopId { get; set; }

        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        //// Lưu "Công thức" linh kiện (tương tự như cách BuilderSession đang làm)
        //[Column(TypeName = "longtext")]
        //public string DesiredComponentsJson { get; set; } = "{}";

        // Có thể lưu chuỗi JSON chứa danh sách URL hoặc URL cách nhau bởi dấu phẩy
        public string? ReferenceImages { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? MinBudget { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal? MaxBudget { get; set; }
        public int Quantity { get; set; } = 1;

        public CommissionStatus Status { get; set; } = CommissionStatus.OpenPool;

        // Navigation Properties
        public virtual User User { get; set; } = null!;
        public virtual ShopProfile? TargetedShop { get; set; }

        // 1 Yêu cầu có thể có nhiều Báo giá từ các Shop khác nhau
        public virtual ICollection<CommissionQuote> Quotes { get; set; } = new List<CommissionQuote>();
    }
}
