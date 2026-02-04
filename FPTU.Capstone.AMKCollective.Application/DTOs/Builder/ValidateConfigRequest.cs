using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Builder
{
    /// <summary>
    /// Request để validate cấu hình build
    /// </summary>

    public class ValidateConfigRequest
    {
        [Required]
        public Guid BaseKitId { get; set; }
        public List<Guid> SelectedComponentIds { get; set; } = new List<Guid>();
    }
}
