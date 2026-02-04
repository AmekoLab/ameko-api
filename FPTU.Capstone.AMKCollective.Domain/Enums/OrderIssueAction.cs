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
        Create, //Tạo yêu cầu

        ShopApprove, //Shop đồng ý

        ShopReject, //Shop từ chối

        UserUpdate, //Khách cập nhật thông tin

        UserEscalate, //Khách khiếu nại lên Admin

        AdminDecision, //Admin phán quyết

        SystemCancel, //Hủy tự động (System)

        UserCancel, //Khách tự hủy

        UserShippedReturn, // Khách confirm đã gửi

        ShopReceivedReturn // Shop confirm đã nhận
    }
}
