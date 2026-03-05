using System;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Services
{
    public interface INotificationService
    {
        Task SendNotificationAsync(Guid userId, string title, string message, string type = "Refund");
    }
}
