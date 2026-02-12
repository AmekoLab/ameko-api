using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Wallet
{
    public class ForgotWalletPinRequest
    {
        // DTO để trống vì hệ thống tự lấy email từ Access Token.
        // Không cho user nhập email để tránh bị hacker reset PIN của người khác
        // hoặc spam OTP vào email nạn nhân.
        // Chỉ cần gọi API -> gửi OTP reset PIN cho đúng chủ tài khoản đang đăng nhập.
    }
}
