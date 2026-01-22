using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces
{
    public interface IEmailService
    {
        Task SendEmailAsync(string to, string subject, string body);
        Task SendVerificationEmailAsync(string to, string code);
        Task SendPasswordResetEmailAsync(string to, string code);
    }
}
