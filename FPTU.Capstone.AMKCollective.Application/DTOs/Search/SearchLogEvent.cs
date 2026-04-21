using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Search
{
    // Dùng record cho các gói tin Event là chuẩn best practice (nhẹ, immutable)
    public record SearchLogEvent(Guid UserId, string Keyword, string SearchType);
}
