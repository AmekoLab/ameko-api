using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Domain.Enums
{
    public enum OrderIssueType //phân biệt khi nào bảo hành khi nào hủy đơn.
    {
        CancelRequest = 0, //Yêu cầu hủy đơn hàng

        ReturnRequest = 1, //Yêu cầu trả hàng/Hoàn tiền

        WarrantyClaim = 2 //Yêu cầu bảo hành/Sửa chữa
    }
}
