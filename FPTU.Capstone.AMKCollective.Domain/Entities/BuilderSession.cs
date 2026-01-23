using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Domain.Entities
{
    public class BuilderSession : BaseEntity
    {
        public Guid BaseKitId { get; set; }

        public Guid? UserId { get; set; }
        [Column(TypeName = "longtext")]
        public string SelectedItemsJson { get; set; } = "{}";

        public string CurrentStep { get; set; } = "start";

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalPrice { get; set; } = 0;
        public DateTime ExpiresAt { get; set; }

        // --- Navigation Properties ---
        [ForeignKey("BaseKitId")]
        public virtual Model BaseKit { get; set; } = null!;

        [ForeignKey("UserId")]
        public virtual User? User { get; set; }
    }
}

