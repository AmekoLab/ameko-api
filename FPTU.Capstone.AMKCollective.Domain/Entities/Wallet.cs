using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Domain.Entities
{
    public class Wallet : BaseEntity
    {
        public Guid UserId { get; set; } // Mỗi Shop đều có 1 User Id

        [Column(TypeName = "decimal(18,2)")]
        public decimal Balance { get; set; } = 0; // Số dư khả dụng (được rút)

        [Column(TypeName = "decimal(18,2)")]
        public decimal HeldBalance { get; set; } = 0; // Số dư bị giữ 

        public string Currency { get; set; } = "VND";

        public bool IsActive { get; set; } = true;
        public string? PinHash { get; set; }
        public string? PinResetCode { get; set; } // Mã OTP 
        public DateTime? PinResetExpiry { get; set; } // Thời gian hết hạn

        // Navigation Properties
        public virtual User? User { get; set; }
        public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
    }
}

