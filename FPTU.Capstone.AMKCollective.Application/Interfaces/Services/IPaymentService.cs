using FPTU.Capstone.AMKCollective.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Services
{
    public interface IPaymentService
    {
        //URL: redirect user to checkout
        Task<CheckoutSessionResponse> CreateCheckoutSessionAsync(Guid orderGroupId, CancellationToken token = default);
        
        Task HandleWebhookAsync(string jsonBody, string signature);

        Task<List<PaymentDto>> GetPaymentHistoryByOrderGroupAsync(Guid orderGroup, CancellationToken token = default);
    }
}
