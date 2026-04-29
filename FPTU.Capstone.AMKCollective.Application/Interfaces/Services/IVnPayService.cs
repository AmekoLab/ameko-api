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

        // Hàm tạo link thanh toán dành riêng cho Mobile
        Task<string> CreatePaymentUrlMobileAsync(CreateCheckoutSessionRequest request, Guid requestingUserId, HttpContext context, string returnUrl);

        // Hàm xử lý khi VNPay gọi IPN trả kết quả về
        Task<PaymentResponseModel> ProcessIpnAsync(IQueryCollection collections);
    }
}
