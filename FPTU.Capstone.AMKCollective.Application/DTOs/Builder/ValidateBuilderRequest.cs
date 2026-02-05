using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Builder
{
    public class ValidateBuilderRequest
    {
        public Guid BaseKitId { get; set; }
        public List<Guid> ComponentIds { get; set; } = new List<Guid>();
    }
}
