using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Domain.Entities
{
    public class Cart : BaseEntity
    {
        public Guid UserId { get; set; } 

        // Navigation Property
        public virtual ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
    }
}
