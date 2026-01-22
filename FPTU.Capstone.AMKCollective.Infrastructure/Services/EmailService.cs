using FPTU.Capstone.AMKCollective.Application.DTOs.Settings;
using FPTU.Capstone.AMKCollective.Application.Interfaces;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Services
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
                    From = new MailAddress(_emailSettings.From, "Ameko System"),
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
             var subject = "Ameko - Verify your email";
             var body = $@"
                <h2>Welcome to Ameko</h2>
                <p>Please enter the following code to verify your account:</p>
                <h3>{code}</h3>
                <p>This code will expire in 15 minutes.</p>
             ";

             await SendEmailAsync(to, subject, body);
        }

        public async Task SendPasswordResetEmailAsync(string to, string code)
        {
             var subject = "Ameko - Reset your password";
             var body = $@"
                <h2>Reset Password</h2>
                <p>You requested to reset your password. Please use the following code:</p>
                <h3>{code}</h3>
                <p>This code will expire in 15 minutes.</p>
             ";

             await SendEmailAsync(to, subject, body);
        }
    }
}
