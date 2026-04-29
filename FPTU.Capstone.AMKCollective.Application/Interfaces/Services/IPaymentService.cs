using FPTU.Capstone.AMKCollective.Application.DTOs;
using FPTU.Capstone.AMKCollective.Application.DTOs.Payment;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Services
{
    public interface IPaymentService
    {
        Task<CheckoutSessionResponse> CreateCheckoutSessionAsync(CreateCheckoutSessionRequest request, CancellationToken token = default);
        Task<CheckoutSessionResponse> CreateCheckoutSessionAsync(CreateCheckoutSessionRequest request, Guid requestingUserId, CancellationToken token = default);

        Task ProcessWebhookAsync(string json, string stripeSignature);

        Task RefundPaymentAsync(Guid orderGroupId);
        Task<CheckoutSessionResponse> CreateDepositSessionAsync(decimal amount, string userEmail, string userIdString, string successUrl, string cancelUrl);

        Task<VerifySessionResponse> VerifySessionAsync(string sessionId);
    }
}
