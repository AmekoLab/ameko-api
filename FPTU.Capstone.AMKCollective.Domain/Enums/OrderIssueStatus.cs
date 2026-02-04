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
        Pending, //Khách mới tạo yêu cầu

        InProgress, //Đã Valid/Có bằng chứng, chờ Shop xử lý

        Approved, //Shop đồng ý yêu cầu

        Rejected, //Shop/Admin từ chối yêu cầu

        Cancelled, //Hủy yêu cầu (Khách rút lui, Timeout không gửi hàng, Shop không rep

        AwaitingReturn, //Admin chấp thuận, chờ khách gửi hàng

        Returning, //Khách đã gửi hàng, đang vận chuyển

        Returned, //Shop đã nhận được hàng

        Completed //Hoàn tất (Đã hoàn tiền/Voucher/Đổi hàng)
    }
}
