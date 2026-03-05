using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Voucher
{
    public class ApplicableVoucherResponse
    {
        // Danh sách mã của Sàn / Đền bù (Áp dụng chung)
        public List<VoucherResponse> SystemVouchers { get; set; } = new List<VoucherResponse>();

        // Danh sách mã của từng Shop (Gom nhóm theo ShopId)
        public List<ShopVoucherGroupResponse> ShopVoucherGroups { get; set; } = new List<ShopVoucherGroupResponse>();
    }
}
