using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Voucher
{
    public class ShopVoucherGroupResponse
    {
        public Guid ShopId { get; set; }
        public List<VoucherResponse> Vouchers { get; set; } = new List<VoucherResponse>();
    }
}
