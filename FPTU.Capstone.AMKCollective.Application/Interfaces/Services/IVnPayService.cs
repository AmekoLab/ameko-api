using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Services
{
    public interface IVnPayService
    {
        // Hàm tạo link thanh toán (dùng chung CreateCheckoutSessionRequest)
        Task<string> CreatePaymentUrlAsync(CreateCheckoutSessionRequest request, Guid requestingUserId, HttpContext context);

        // Hàm xử lý khi VNPay gọi IPN trả kết quả về
        Task<PaymentResponseModel> ProcessIpnAsync(IQueryCollection collections);
    }
}
