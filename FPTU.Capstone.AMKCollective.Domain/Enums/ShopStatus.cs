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
        PendingApproval =0, // Chờ Admin xét duyệt hồ sơ

        Active = 1, // Hoạt động bình thường

        Inactive = 2, // Tạm nghỉ/Ẩn

        Rejected = 3, // Hồ sơ bị từ chối

        Banned = 4 // Bị cấm hoạt động
    }
}
