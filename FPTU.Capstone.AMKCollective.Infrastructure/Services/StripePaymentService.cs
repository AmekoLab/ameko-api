using FPTU.Capstone.AMKCollective.Application.DTOs;
using FPTU.Capstone.AMKCollective.Application.Interfaces;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PaymentMethod = FPTU.Capstone.AMKCollective.Domain.Enums.PaymentMethod;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Services
{
    public class StripePaymentService : IPaymentService
    {
        private readonly IPaymentRepository _paymentRepo;
        private readonly IOrderGroupRepository _orderGroupRepo;
        private readonly StripeSettings _stripeSettings;

        public StripePaymentService(
            IPaymentRepository paymentRepo,
            IOrderGroupRepository orderGroupRepo,
            IOptions<StripeSettings> stripeOptions)
        {
            _paymentRepo = paymentRepo;
            _orderGroupRepo = orderGroupRepo;
            _stripeSettings = stripeOptions.Value;

            // Set API Key
            StripeConfiguration.ApiKey = _stripeSettings.SecretKey;
        }

        // 1. SỬA LẠI HÀM NÀY ĐỂ NHẬN GUID (KHỚP VỚI INTERFACE & ORDER SERVICE)
        public async Task<CheckoutSessionResponse> CreateCheckoutSessionAsync(Guid orderGroupId, CancellationToken token = default)
        {
            var orderGroup = await _orderGroupRepo.GetByIdAsync(orderGroupId, token);
            if (orderGroup == null) throw new KeyNotFoundException("Order Group not found");

            // Hardcode URL tạm thời để chạy được tính năng
            // (Thực tế nên lấy từ AppSettings hoặc DTO nếu bạn refactor lại sau)
            string domain = "http://localhost:3000";

            var options = new SessionCreateOptions
            {
                PaymentMethodTypes = new List<string> { "card" },
                Mode = "payment",
                SuccessUrl = $"{domain}/payment/success?session_id={{CHECKOUT_SESSION_ID}}",
                CancelUrl = $"{domain}/payment/failed",

                LineItems = new List<SessionLineItemOptions>
                {
                    new SessionLineItemOptions
                    {
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            Currency = "vnd",
                            UnitAmount = (long)orderGroup.TotalGroupAmount,
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = $"Thanh toán đơn hàng #{orderGroup.Id.ToString().Substring(0, 8)}",
                            },
                        },
                        Quantity = 1,
                    },
                },
                Metadata = new Dictionary<string, string>
                {
                    { "OrderGroupId", orderGroup.Id.ToString() }
                }
            };

            var service = new SessionService();
            Session session = await service.CreateAsync(options, cancellationToken: token);

            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                OrderGroupId = orderGroup.Id,
                Amount = orderGroup.TotalGroupAmount,
                Currency = "vnd",
                StripeSessionId = session.Id,
                Status = PaymentStatus.Pending,
                Method = PaymentMethod.CreditCard,
                Description = "Initiated Stripe Checkout",
                CreatedAt = DateTime.UtcNow
            };

            await _paymentRepo.AddAsync(payment, token);
            await _paymentRepo.SaveChangesAsync(token);

            return new CheckoutSessionResponse
            {
                SessionId = session.Id,
                PaymentUrl = session.Url
            };
        }

        // 2. GIỮ NGUYÊN WEBHOOK (ĐÃ SỬA LỖI EVENT STRING)
        public async Task HandleWebhookAsync(string jsonBody, string signature)
        {
            var secret = _stripeSettings.WebhookSecret;
            try
            {
                Stripe.Event stripeEvent = EventUtility.ConstructEvent(jsonBody, signature, secret);

                // Dùng chuỗi string để tránh lỗi version thư viện
                if (stripeEvent.Type == "checkout.session.completed")
                {
                    var session = stripeEvent.Data.Object as Session;
                    if (session != null)
                    {
                        var payment = await _paymentRepo.GetByStripeSessionIdAsync(session.Id);
                        if (payment != null)
                        {
                            payment.Status = PaymentStatus.Success;
                            payment.StripePaymentIntentId = session.PaymentIntentId;
                            payment.PayerEmail = session.CustomerDetails?.Email;

                            await _orderGroupRepo.UpdatePaymentStatusAsync(payment.OrderGroupId, "Paid");
                            await _paymentRepo.SaveChangesAsync();
                        }
                    }
                }
            }
            catch (StripeException e)
            {
                throw new Exception($"Stripe Webhook Error: {e.Message}");
            }
        }

        // 3. THÊM HÀM NÀY ĐỂ KHÔNG BỊ LỖI THIẾU INTERFACE MEMBER
        public async Task<List<PaymentDto>> GetPaymentHistoryByOrderGroupAsync(Guid orderGroupId, CancellationToken token = default)
        {
            // Tạm thời trả về list rỗng để code build được
            // Bạn có thể implement logic lấy từ DB sau
            return new List<PaymentDto>();
        }
    }
}