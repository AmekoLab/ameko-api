using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Part
{
    public class GetPartsFilterRequest
    {
        public Guid? ShopId { get; set; }
        public Guid CategoryId { get; set; }
        public string? PartType { get; set; }
        public bool? IsActive { get; set; }
        public string? SearchTerm { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
}
