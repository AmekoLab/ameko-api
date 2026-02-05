using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Domain.Enums
{
    public enum ShopStatus
    {
        PendingApproval, // Chờ Admin xét duyệt hồ sơ

        Active, // Hoạt động bình thường

        Inactive, // Tạm nghỉ/Ẩn

        Rejected, // Hồ sơ bị từ chối

        Banned // Bị cấm hoạt động
    }
}
