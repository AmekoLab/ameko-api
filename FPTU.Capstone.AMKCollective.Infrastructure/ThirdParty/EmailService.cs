using FPTU.Capstone.AMKCollective.Application.DTOs.Settings;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Infrastructure.ThirdParty
{
    public class EmailService : IEmailService
    {
        private readonly EmailSettings _emailSettings;

        public EmailService(IOptions<EmailSettings> emailSettings)
        {
            _emailSettings = emailSettings.Value;
        }

        public async Task SendEmailAsync(string to, string subject, string body)
        {
            using (var client = new SmtpClient(_emailSettings.SmtpServer, _emailSettings.Port))
            {
                client.UseDefaultCredentials = false;
                client.Credentials = new NetworkCredential(_emailSettings.Username, _emailSettings.Password);
                client.EnableSsl = true;
                client.DeliveryMethod = SmtpDeliveryMethod.Network;

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_emailSettings.From, "AmekoLab System"),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                };

                mailMessage.To.Add(to);

                await client.SendMailAsync(mailMessage);
            }
        }

        public async Task SendVerificationEmailAsync(string to, string code)
        {
            var subject = "AmekoLab - Xác nhận email / Verify your email";
            var body = $@"
                <div style='font-family:Segoe UI,Arial,sans-serif;max-width:500px;margin:auto;border:1px solid #eee;border-radius:10px;padding:32px 24px;background:#fafbfc;'>
                    <div style='text-align:center;margin-bottom:24px;'>
                        <img src='https://res.cloudinary.com/dolzghn2d/image/upload/Gemini_Generated_Image_glu6opglu6opglu6_mzy3ev.png' alt='AmekoLab Logo' width='64' style='margin-bottom:8px;'/>
                        <h2 style='color:#1a1a1a;margin:0;'>Chào mừng đến <span style='color:#4f8cff;'>AmekoLab</span></h2>
                        <p style='color:#666;font-size:12px;margin:4px 0 0 0;'>Welcome to <span style='color:#4f8cff;'>AmekoLab</span></p>
                    </div>
                    
                    <p style='font-size:15px;color:#333;margin-bottom:18px;text-align:center;'>
                        Vui lòng nhập mã sau để xác nhận tài khoản của bạn:<br/>
                        <span style='font-size:12px;color:#888;'>Please enter the following code to verify your account:</span>
                    </p>
                    
                    <div style='background:#f4f8ff;border-radius:8px;padding:18px 0;margin:0 0 18px 0;text-align:center;'>
                        <span style='font-size:2rem;letter-spacing:6px;color:#4f8cff;font-weight:bold;'>{code}</span>
                    </div>
                    
                    <div style='background:#fff3cd;border:1px solid #ffc107;border-radius:6px;padding:12px 16px;margin:16px 0;font-size:12px;color:#856404;'>
                        <strong>Bảo mật / Security:</strong> Không chia sẻ mã này với bất kỳ ai. Never share this code with anyone.
                    </div>
                    
                    <p style='font-size:13px;color:#888;text-align:center;margin-bottom:12px;'>
                        Mã này sẽ hết hạn trong <b>15 phút</b>.<br/>
                        <span style='font-size:12px;'>This code will expire in <b>15 minutes</b>.</span>
                    </p>
                    
                    <p style='font-size:13px;color:#888;text-align:center;margin-bottom:16px;'>
                        Nếu bạn không yêu cầu điều này, vui lòng bỏ qua email này.<br/>
                        <span style='font-size:12px;'>If you did not request this, please ignore this email.</span>
                    </p>
                    
                    <hr style='margin:24px 0 16px 0;border:none;border-top:1px solid #eee;'/>
                    
                    <div style='font-size:11px;color:#999;text-align:center;margin-bottom:12px;'>
                        <p style='margin:0 0 8px 0;'>Có câu hỏi? / Have questions?<br/>
                        <a href='mailto:support@amekolab.online' style='color:#4f8cff;text-decoration:none;'>support@amekolab.online</a></p>
                    </div>
                    
                    <div style='font-size:11px;color:#aaa;text-align:center;border-top:1px solid #eee;padding-top:12px;'>
                        &copy; {DateTime.UtcNow.Year} AmekoLab. All rights reserved.<br/>
                        <a href='#' style='color:#aaa;text-decoration:none;font-size:10px;'>Unsubscribe</a> | 
                        <a href='#' style='color:#aaa;text-decoration:none;font-size:10px;'>Privacy Policy</a>
                    </div>
                </div>
            ";

            await SendEmailAsync(to, subject, body);
        }

        public async Task SendPasswordResetEmailAsync(string to, string code)
        {
            var subject = "AmekoLab - Đặt lại mật khẩu / Reset your password";
            var body = $@"
                <div style='font-family:Segoe UI,Arial,sans-serif;max-width:500px;margin:auto;border:1px solid #eee;border-radius:10px;padding:32px 24px;background:#fafbfc;'>
                    <div style='text-align:center;margin-bottom:24px;'>
                        <img src='https://res.cloudinary.com/dolzghn2d/image/upload/Gemini_Generated_Image_glu6opglu6opglu6_mzy3ev.png' alt='AmekoLab Logo' width='64' style='margin-bottom:8px;'/>
                        <h2 style='color:#1a1a1a;margin:0;'>Đặt lại <span style='color:#4f8cff;'>Mật khẩu</span></h2>
                        <p style='color:#666;font-size:12px;margin:4px 0 0 0;'>Reset Your <span style='color:#4f8cff;'>Password</span></p>
                    </div>
                    
                    <p style='font-size:15px;color:#333;margin-bottom:18px;text-align:center;'>
                        Bạn đã yêu cầu đặt lại mật khẩu. Vui lòng sử dụng mã sau:<br/>
                        <span style='font-size:12px;color:#888;'>You requested to reset your password. Please use the following code:</span>
                    </p>
                    
                    <div style='background:#f4f8ff;border-radius:8px;padding:18px 0;margin:0 0 18px 0;text-align:center;'>
                        <span style='font-size:2rem;letter-spacing:6px;color:#4f8cff;font-weight:bold;'>{code}</span>
                    </div>
                    
                    <div style='background:#f8d7da;border:1px solid #f5c6cb;border-radius:6px;padding:12px 16px;margin:16px 0;font-size:12px;color:#721c24;'>
                        <strong>Bảo mật / Security:</strong> Không chia sẻ mã này với bất kỳ ai. Nếu bạn không yêu cầu đặt lại mật khẩu, liên hệ support ngay lập tức. Never share this code. If you didn't request a reset, contact support immediately.
                    </div>
                    
                    <p style='font-size:13px;color:#888;text-align:center;margin-bottom:12px;'>
                        Mã này sẽ hết hạn trong <b>15 phút</b>.<br/>
                        <span style='font-size:12px;'>This code will expire in <b>15 minutes</b>.</span>
                    </p>
                    
                    <p style='font-size:13px;color:#888;text-align:center;margin-bottom:16px;'>
                        Nếu bạn không yêu cầu điều này, vui lòng bỏ qua email này.<br/>
                        <span style='font-size:12px;'>If you did not request this, please ignore this email.</span>
                    </p>
                    
                    <hr style='margin:24px 0 16px 0;border:none;border-top:1px solid #eee;'/>
                    
                    <div style='font-size:11px;color:#999;text-align:center;margin-bottom:12px;'>
                        <p style='margin:0 0 8px 0;'>Có vấn đề? / Issues?<br/>
                        <a href='mailto:support@amekolab.online' style='color:#4f8cff;text-decoration:none;'>support@amekolab.online</a></p>
                    </div>
                    
                    <div style='font-size:11px;color:#aaa;text-align:center;border-top:1px solid #eee;padding-top:12px;'>
                        &copy; {DateTime.UtcNow.Year} AmekoLab. All rights reserved.<br/>
                        <a href='#' style='color:#aaa;text-decoration:none;font-size:10px;'>Unsubscribe</a> | 
                        <a href='#' style='color:#aaa;text-decoration:none;font-size:10px;'>Privacy Policy</a>
                    </div>
                </div>
            ";

            await SendEmailAsync(to, subject, body);
        }
    }
}
