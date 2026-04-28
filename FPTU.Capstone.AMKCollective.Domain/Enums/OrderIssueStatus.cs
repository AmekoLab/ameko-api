using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Domain.Enums
{
    public enum OrderIssueStatus
    {
        Pending = 0, //Khách mới tạo yêu cầu

        InProgress = 1, //Đã Valid/Có bằng chứng, chờ Shop xử lý

        ShopAccepted = 2, //Shop đồng ý yêu cầu

        Rejected = 3, //Shop/Admin từ chối yêu cầu

        AutoCancelled = 4, //Hủy yêu cầu (Khách rút lui, Timeout không gửi hàng, Shop không rep

        AwaitingReturn = 5, //Admin chấp thuận, chờ khách gửi hàng

        Returning = 6, //Khách đã gửi hàng, đang vận chuyển

        Returned = 7, //Shop đã nhận được hàng

        Completed = 8, //Hoàn tất (Đã hoàn tiền/Voucher/Đổi hàng)

        CancelledByUser = 9, // Khách hàng tự rút lại/hủy yêu cầu

        ShopRejected = 10 // Shop từ chối, chờ Admin quyết định cuối cùng
    }
}
