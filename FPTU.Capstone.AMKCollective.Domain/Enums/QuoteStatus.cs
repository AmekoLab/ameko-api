using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Domain.Enums
{
    public enum QuoteStatus
    {
        PendingUserDecision = 0, // Chờ khách hàng chốt (Accept/Reject)

        Accepted = 1,            // Khách hàng đã đồng ý báo giá này

        Rejected = 2             // Khách hàng đã từ chối báo giá này
    }
}
