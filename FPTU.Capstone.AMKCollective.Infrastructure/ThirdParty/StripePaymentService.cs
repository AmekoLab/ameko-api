using FPTU.Capstone.AMKCollective.Application.DTOs;
using FPTU.Capstone.AMKCollective.Application.DTOs.Payment;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Domain.Enums;
using FPTU.Capstone.AMKCollective.Infrastructure.Configurations;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PaymentMethod = FPTU.Capstone.AMKCollective.Domain.Enums.PaymentMethod;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
namespace FPTU.Capstone.AMKCollective.Infrastructure.ThirdParty
{
    public class StripePaymentService : IPaymentService
    {
        private readonly StripeSettings _stripeSettings;
        private readonly IUnitOfWork _unitOfWork;

        public StripePaymentService(IOptions<StripeSettings> stripeSettings, IUnitOfWork unitOfWork)
        {
            _stripeSettings = stripeSettings.Value;
            StripeConfiguration.ApiKey = _stripeSettings.SecretKey;
            _unitOfWork = unitOfWork;
        }

        // 1. TẠO CHECKOUT SESSION (Gửi sang Stripe)
        public async Task<CheckoutSessionResponse> CreateCheckoutSessionAsync(CreateCheckoutSessionRequest request, CancellationToken token = default)
        {
            var orderGroup = await _unitOfWork.OrderGroups.GetByIdAsync(request.OrderGroupId);
            if (orderGroup == null) throw new KeyNotFoundException("Order Group not found");

            var options = new SessionCreateOptions
            {
                PaymentMethodTypes = new List<string> { "card" },
                Mode = "payment",
                SuccessUrl = request.SuccessUrl + "?session_id={CHECKOUT_SESSION_ID}",
                CancelUrl = request.CancelUrl,
                Metadata = new Dictionary<string, string>
                {
                    { "OrderGroupId", orderGroup.Id.ToString() }
                },
                LineItems = new List<SessionLineItemOptions>()
            };

            foreach (var order in orderGroup.Orders)
            {
                foreach (var item in order.OrderItems)
                {
                    options.LineItems.Add(new SessionLineItemOptions
                    {
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            UnitAmount = (long)item.UnitPrice,
                            Currency = "vnd",
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = item.ProductName,
                                Description = item.IsCustom ? "Custom Build" : "Part"
                            },
                        },
                        Quantity = item.Quantity,
                    });
                }

                if (order.ShippingFee > 0)
                {
                    options.LineItems.Add(new SessionLineItemOptions
                    {
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            UnitAmount = (long)order.ShippingFee,
                            Currency = "vnd",
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = $"Shipping Fee (Shop ID: {order.ShopId})"
                            },
                        },
                        Quantity = 1,
                    });
                }
            }

            var service = new SessionService();
            Session session = await service.CreateAsync(options, cancellationToken: token);

            return new CheckoutSessionResponse
            {
                SessionId = session.Id,
                PaymentUrl = session.Url
            };
        }

        public async Task ProcessWebhookAsync(string json, string stripeSignature)
        {
            try
            {
                var stripeEvent = EventUtility.ConstructEvent(
                    json,
                    stripeSignature,
                    _stripeSettings.WebhookSecret
                );

                if (stripeEvent.Type == EventTypes.CheckoutSessionCompleted)
                {
                    var session = stripeEvent.Data.Object as Session;

                    if (session.Metadata != null && session.Metadata.TryGetValue("OrderGroupId", out var orderGroupIdStr))
                    {
                        if (Guid.TryParse(orderGroupIdStr, out Guid orderGroupId))
                        {
                            await FulfillOrderAsync(orderGroupId, session.PaymentIntentId, session.Id);
                        }
                    }
                }
            }
            catch (StripeException e)
            {
                throw new Exception($"Stripe Webhook Error: {e.Message}");
            }
        }

        // Helper
        private async Task FulfillOrderAsync(Guid orderGroupId, string transactionId, string sessionId)
        {
            var orderGroup = await _unitOfWork.OrderGroups.GetByIdAsync(orderGroupId);
            if (orderGroup != null)
            {
                orderGroup.PaymentStatus = "Paid";
                foreach (var order in orderGroup.Orders)
                {
                    order.PaymentStatus = "Paid";
                }
                var payment = new FPTU.Capstone.AMKCollective.Domain.Entities.Payment
                {
                    Id = Guid.NewGuid(),
                    OrderGroupId = orderGroupId,
                    Amount = orderGroup.TotalGroupAmount,
                    Currency = "vnd",
                    StripePaymentIntentId = transactionId,
                    StripeSessionId = sessionId,

                    Method = PaymentMethod.CreditCard,
                    Status = PaymentStatus.Paid,
                };

                await _unitOfWork.Payments.AddAsync(payment);
                await _unitOfWork.CommitAsync();
            }
        }

        public async Task RefundPaymentAsync(Guid orderGroupId)
        {
            var payment = await _unitOfWork.Payments.GetPaymentByOrderGroupIdAsync(orderGroupId);

            if (payment == null)
                throw new KeyNotFoundException("Order not found.");

            if (string.IsNullOrEmpty(payment.StripePaymentIntentId))
                throw new InvalidOperationException("Order do not have PaymentIntentId to refund");

            if (payment.Status == PaymentStatus.Refunded)
                throw new InvalidOperationException("Order was refund before.");

            try
            {
                var refundOptions = new RefundCreateOptions
                {
                    PaymentIntent = payment.StripePaymentIntentId, 
                    Reason = RefundReasons.RequestedByCustomer,                                                               
                };

                var service = new RefundService();
                var refund = await service.CreateAsync(refundOptions);

                if (refund.Status == "succeeded" || refund.Status == "pending")
                {
                    payment.Status = PaymentStatus.Refunded;
                    payment.Description = $"Refunded via Stripe. Refund ID: {refund.Id}";
                    await _unitOfWork.CommitAsync();
                }
            }
            catch (StripeException e)
            {
                throw new Exception($"Stripe Refund Failed: {e.Message}");
            }
        }
    }
}