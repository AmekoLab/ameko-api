using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Domain.Enums
{
    public enum AssemblyStepStatus
    {
        Pending = 0,     // Chưa bắt đầu
        InProgress = 1,  // Đang thực hiện
        Completed = 2,   // Đã hoàn thành
        Skipped = 3      // Bỏ qua (nếu khách không yêu cầu/không cần thiết)
    }
}
