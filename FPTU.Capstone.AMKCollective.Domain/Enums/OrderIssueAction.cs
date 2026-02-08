using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Domain.Enums
{
    public enum OrderIssueAction //dùng cho OrderIssueLog
    {
        Create = 0, //Tạo yêu cầu

        ShopApprove = 1, //Shop đồng ý

        ShopReject = 2, //Shop từ chối

        UserUpdate = 3, //Khách cập nhật thông tin

        UserEscalate = 4, //Khách khiếu nại lên Admin

        AdminDecision = 5, //Admin phán quyết

        SystemCancel = 6, //Hủy tự động (System)

        UserCancel = 7, //Khách tự hủy

        UserShippedReturn = 8, // Khách confirm đã gửi

        ShopReceivedReturn = 9 // Shop confirm đã nhận
    }
}
