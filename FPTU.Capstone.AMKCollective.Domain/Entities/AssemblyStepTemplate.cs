using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Domain.Entities
{
    public class AssemblyStepTemplate : BaseEntity
    {
        public Guid ShopId { get; set; }

        public string StepName { get; set; } = string.Empty;

        public int StepOrder { get; set; }

        public bool IsRequired { get; set; } = true; // Bắt buộc hay tùy chọn

        // Navigation Properties
        public virtual ShopProfile Shop { get; set; } = null!;
    }
}
