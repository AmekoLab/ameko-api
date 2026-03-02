using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Domain.Enums
{
    public enum CommissionStatus
    {
        PendingTarget = 0, // Chờ Shop được chỉ định phản hồi (nếu User có chọn đích danh 1 shop)

        OpenPool = 1,      // Đang trên chợ chung, chờ các Shop vào báo giá

        Quoted = 2,        // Đã có ít nhất 1 báo giá từ Shop

        Completed = 3,     // Đã chốt báo giá và chuyển thành công thành Đơn hàng

        Canceled = 4       // Yêu cầu đã bị hủy (bởi User hoặc hệ thống)
    }
}
