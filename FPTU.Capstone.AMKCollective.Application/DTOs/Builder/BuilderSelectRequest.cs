using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Builder
{
    public class BuilderSelectRequest
    {
        [Required]
        public Guid SessionId { get; set; }
        [Required]
        public Guid SelectedPartId { get; set; }
        [Required]
        public string StepName { get; set; } = string.Empty;
    }
}
