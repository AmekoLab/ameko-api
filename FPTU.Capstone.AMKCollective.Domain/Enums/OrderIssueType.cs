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
        CancelRequest, //Yêu cầu hủy đơn hàng

        ReturnRequest, //Yêu cầu trả hàng/Hoàn tiền

        WarrantyClaim //Yêu cầu bảo hành/Sửa chữa
    }
}
