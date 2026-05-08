using FPTU.Capstone.AMKCollective.Application.DTOs;
using FPTU.Capstone.AMKCollective.Application.DTOs.Payment;
using FPTU.Capstone.AMKCollective.Application.DTOs.Settings;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Application.Helpers;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Domain.Enums;
using FPTU.Capstone.AMKCollective.Infrastructure.Configurations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PaymentMethod = FPTU.Capstone.AMKCollective.Domain.Enums.PaymentMethod;
namespace FPTU.Capstone.AMKCollective.Infrastructure.ThirdParty
{
    public class StripePaymentService : IPaymentService
    {
        private readonly StripeSettings _stripeSettings;
        private readonly IUnitOfWork _unitOfWork;
                private readonly OrderSettings _orderSettings;
        //private readonly IWalletService _walletService;
        private readonly IServiceProvider _serviceProvider;

        public StripePaymentService(IOptions<StripeSettings> stripeSettings, IUnitOfWork unitOfWork, //IWalletService walletService
                    IServiceProvider serviceProvider, IOptions<OrderSettings> orderOptions)
        {
            _stripeSettings = stripeSettings.Value;
            StripeConfiguration.ApiKey = _stripeSettings.SecretKey;
            _unitOfWork = unitOfWork;
            //_walletService = walletService;
            _serviceProvider = serviceProvider;
                        _orderSettings = orderOptions.Value;
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
                ClientReferenceId = orderGroup.Id.ToString(),
                Metadata = new Dictionary<string, string>
                {
                    { "Type", "OrderPayment" },
                    { "OrderGroupId", orderGroup.Id.ToString() }
                },
                LineItems = new List<SessionLineItemOptions>()
            };


            // Gửi 1 dòng thanh toán tổng cho cả OrderGroup (Đã bao gồm mọi loại Voucher và Phí ship)
            options.LineItems.Add(new SessionLineItemOptions
            {
                PriceData = new SessionLineItemPriceDataOptions
                {
                    UnitAmount = (long)orderGroup.TotalGroupAmount, 
                    Currency = "vnd",
                    ProductData = new SessionLineItemPriceDataProductDataOptions
                    {
                        Name = "AMK Collective - Order Payment",
                        Description = $"Payment for {orderGroup.Orders.Count} orders, including shipping fees and all applied discounts/vouchers."
                    },
                },
                Quantity = 1,
            });

            var service = new SessionService();
            Session session = await service.CreateAsync(options, cancellationToken: token);

            return new CheckoutSessionResponse
            {
                SessionId = session.Id,
                PaymentUrl = session.Url
            };
        }

        // [Fix #3] Overload có ownership check — dùng khi gọi từ Controller (có Auth)
        public async Task<CheckoutSessionResponse> CreateCheckoutSessionAsync(CreateCheckoutSessionRequest request, Guid requestingUserId, CancellationToken token = default)
        {
            var orderGroup = await _unitOfWork.OrderGroups.GetByIdAsync(request.OrderGroupId);
            if (orderGroup == null) throw new KeyNotFoundException("Order Group not found");

            // Ownership check: đảm bảo user chỉ thanh toán đơn của chính mình
            if (orderGroup.CustomerId != requestingUserId)
                throw new UnauthorizedAccessException("You are not authorized to pay for this order.");

            // Delegate về hàm gốc sau khi đã validate
            return await CreateCheckoutSessionAsync(request, token);
        }

        public async Task ProcessWebhookAsync(string json, string stripeSignature)
        {
            try
            {
                var stripeEvent = EventUtility.ConstructEvent(json, stripeSignature, _stripeSettings.WebhookSecret);

                if (stripeEvent.Type == EventTypes.CheckoutSessionCompleted)
                {
                    var session = stripeEvent.Data.Object as Session;

                    // Lấy loại giao dịch từ Metadata
                    string transactionType = "";
                    if (session.Metadata != null && session.Metadata.TryGetValue("Type", out var typeVal))
                    {
                        transactionType = typeVal;
                    }

                    // CASE 1: NẠP TIỀN (DEPOSIT)
                    if (transactionType == "Deposit")
                    {
                        // Tìm Payment Pending trong DB
                        var payment = await _unitOfWork.Payments.GetPaymentBySessionIdAsync(session.Id);

                        if (payment != null && payment.Status != PaymentStatus.Paid)
                        {
                            // Update Payment status
                            payment.Status = PaymentStatus.Paid;
                            payment.StripePaymentIntentId = session.PaymentIntentId;
                            payment.Description += " | Success";

                            // Lấy hoặc tạo Wallet
                            var wallet = await _unitOfWork.Wallets.GetByUserIdAsync(payment.UserId);
                            if (wallet == null)
                            {
                                wallet = new Wallet
                                {
                                    UserId = payment.UserId,
                                    Balance = 0,
                                    HeldBalance = 0,
                                    Currency = "VND",
                                    IsActive = true
                                };
                                await _unitOfWork.Wallets.AddAsync(wallet);
                                _unitOfWork.Payments.Update(payment);
                                await _unitOfWork.CommitAsync();

                                // Re-fetch để có state chính xác sau khi persist
                                wallet = await _unitOfWork.Wallets.GetByUserIdAsync(payment.UserId);
                                if (wallet == null) throw new Exception("Failed to create wallet for deposit.");
                            }

                            // [FIX Critical #2] Dùng atomic SQL update thay vì EF tracking
                            // Tránh race condition khi Stripe gửi webhook retry song song
                            var (success, oldBal, oldHeld) = await _unitOfWork.Wallets.UpdateBalancesAsync(wallet.Id, payment.Amount, 0);
                            if (!success)
                                throw new Exception($"Failed to update wallet balance for deposit. WalletId: {wallet.Id}");

                            // [FIX Critical #1] Tạo Transaction record — trước đây Deposit không có log
                            var txCode = $"DEP-{DateTime.UtcNow:yyMMdd}-{Guid.NewGuid().ToString().Substring(0, 6).ToUpper()}";
                            var transaction = new Transaction
                            {
                                TransactionCode = txCode,
                                IdempotencyKey = $"STRIPE_DEPOSIT_{session.Id}",
                                WalletId = wallet.Id,
                                Amount = payment.Amount,
                                BalanceBeforeTransaction = oldBal,
                                BalanceAfterTransaction = oldBal + payment.Amount,
                                Direction = TransactionDirection.In,
                                Type = TransactionType.Deposit,
                                HeldBalanceBeforeTransaction = oldHeld,
                                HeldBalanceAfterTransaction = oldHeld,
                                FeeAmount = 0,
                                Description = $"Deposit via Stripe (Payment: {payment.Id})",
                                Currency = "VND",
                                CreatedAt = DateTime.UtcNow
                            };
                            await _unitOfWork.Transactions.AddAsync(transaction);

                            _unitOfWork.Payments.Update(payment);
                            await _unitOfWork.CommitAsync();
                        }
                    }
                    // CASE 2: THANH TOÁN ĐƠN HÀNG 
                    else
                    {
                        string orderGroupIdStr = session.ClientReferenceId;
                        if (string.IsNullOrEmpty(orderGroupIdStr))
                        {
                            session.Metadata?.TryGetValue("OrderGroupId", out orderGroupIdStr);
                        }

                        if (!string.IsNullOrEmpty(orderGroupIdStr) && Guid.TryParse(orderGroupIdStr, out Guid orderGroupId))
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
            catch (Exception ex)
            {
                Console.WriteLine($"[WEBHOOK CRITICAL ERROR] {ex.Message}");
                throw;
            }
        }

        // Helper
        private async Task FulfillOrderAsync(Guid orderGroupId, string transactionId, string sessionId)
        {
            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var walletService = scope.ServiceProvider.GetRequiredService<IWalletService>();

                    var orderGroup = await _unitOfWork.OrderGroups.GetByIdAsync(orderGroupId);

                    if (orderGroup == null) return;

                    // [Fix #1] Idempotency guard — Stripe có thể gửi cùng 1 event nhiều lần
                    if (orderGroup.PaymentStatus == PaymentStatus.Paid)
                    {
                        Console.WriteLine($"[WEBHOOK] OrderGroup {orderGroupId} already fulfilled. Skipping.");
                        return;
                    }

                    orderGroup.PaymentStatus = PaymentStatus.Paid;

                    // Thu thập thông tin shop TRƯỚC khi commit, để wallet call sau commit
                    var shopPendingSales = new List<(Guid ShopUserId, Guid OrderId, decimal Amount, decimal FeeAmount, decimal SysVoucherDeduction)>();
                    if (orderGroup.Orders != null && orderGroup.Orders.Any())
                    {
                        foreach (var order in orderGroup.Orders)
                        {
                            order.PaymentStatus = PaymentStatus.Paid;
                            order.OrderStatus = OrderStatus.Processing;
                            await _unitOfWork.Orders.UpdateOrderAsync(order);

                            if (order.ShopId.HasValue)
                            {
                                var shopProfile = await _unitOfWork.Shops.GetByIdAsync(order.ShopId.Value);
                                if (shopProfile != null)
                                {
                                    decimal shopRevenue = ShopRevenueCalculator.CalculateShopRevenue(
                                        order,
                                        _orderSettings.ShopPayoutRate,
                                        _orderSettings.SystemVoucherShopShareRate,
                                        _orderSettings.SystemVoucherShopShareCap);
                                    decimal feeAmount = order.TotalAmount - shopRevenue;
                                    order.PlatformFeeAmount = feeAmount;
                                    decimal sysVoucherDeduction = order.SystemDiscountAmount > 0
                                        ? Math.Min(order.SystemDiscountAmount * _orderSettings.SystemVoucherShopShareRate, _orderSettings.SystemVoucherShopShareCap)
                                        : 0;
                                    shopPendingSales.Add((shopProfile.UserId, order.Id, shopRevenue, feeAmount, sysVoucherDeduction));
                                }
                            }
                        }
                    }

                    // Tạo Payment Log
                    if (orderGroup.CustomerId == Guid.Empty)
                        throw new Exception("OrderGroup has invalid CustomerId (Guid.Empty)");

                    var payment = new FPTU.Capstone.AMKCollective.Domain.Entities.Payment
                    {
                        Id = Guid.NewGuid(),
                        OrderGroupId = orderGroupId,
                        UserId = orderGroup.CustomerId,
                        Amount = orderGroup.TotalGroupAmount,
                        Currency = "vnd",
                        StripePaymentIntentId = transactionId,
                        StripeSessionId = sessionId,
                        Method = PaymentMethod.CreditCard,
                        Status = PaymentStatus.Paid,
                        Type = PaymentType.OrderPayment,
                        CreatedAt = DateTime.UtcNow
                    };

                    await _unitOfWork.Payments.AddAsync(payment);

                    // [Fix #2] CommitAsync TRƯỚC — đảm bảo Order/Payment đã lưu xuống DB
                    await _unitOfWork.CommitAsync();

                    // [Fix #2] Sau commit mới cộng tiền vào ví Shop (tránh inconsistency)
                    // Nếu wallet call fail ở đây, order đã paid → có thể reconcile sau
                    foreach (var (shopUserId, orderId, amount, feeAmt, sysVoucherDeduction) in shopPendingSales)
                    {
                        await walletService.AddPendingSalesToWalletAsync(shopUserId, orderId, amount, feeAmt, sysVoucherDeduction);
                    }

                    // Notify shop: đơn hàng mới
                    var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
                    foreach (var (shopUserId, orderId, _, _, _) in shopPendingSales)
                    {
                        await notificationService.SendNotificationAsync(
                            shopUserId,
                            "Đơn hàng mới",
                            $"Bạn có đơn hàng mới #{orderId} cần xử lý.",
                            nameof(FPTU.Capstone.AMKCollective.Domain.Enums.NotificationType.OrderCreated),
                            orderId.ToString(),
                            "Order",
                            actorId: orderGroup.CustomerId);
                    }
                }
            }
            catch (Exception ex)
            {
                var innerMsg = ex.InnerException != null ? ex.InnerException.Message : "No inner exception";
                Console.WriteLine($"[STRIPE WEBHOOK ERROR] {ex.Message} | Inner: {innerMsg}");
                throw;
            }
        }

        public async Task<CheckoutSessionResponse> CreateDepositSessionAsync(decimal amount, string userEmail, string userIdString, string successUrl, string cancelUrl)
        {
            var options = new SessionCreateOptions
            {
                PaymentMethodTypes = new List<string> { "card" },
                Mode = "payment",
                // Đính session_id vào SuccessUrl để FE có thể gọi verify-session sau redirect
                SuccessUrl = successUrl + "?session_id={CHECKOUT_SESSION_ID}",
                CancelUrl = cancelUrl,
                CustomerEmail = userEmail,

                Metadata = new Dictionary<string, string>
                {
                    { "Type", "Deposit" },
                    { "UserId", userIdString }
                },

                LineItems = new List<SessionLineItemOptions>
                {
                    new SessionLineItemOptions
                    {
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            Currency = "vnd",
                            UnitAmount = (long)amount,
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = "Top up to AMK (Deposit)",
                                Description = $"Top up amount: {amount:N0} VND"
                            }
                        },
                        Quantity = 1
                    }
                },
            };

            var service = new SessionService();
            Session session = await service.CreateAsync(options);

            return new CheckoutSessionResponse
            {
                SessionId = session.Id,
                PaymentUrl = session.Url, 
                PaymentIntentId = session.PaymentIntentId
            };
        }

        public async Task<VerifySessionResponse> VerifySessionAsync(string sessionId)
        {
            var service = new SessionService();
            var session = await service.GetAsync(sessionId);

            return new VerifySessionResponse
            {
                SessionId = session.Id,
                PaymentStatus = session.PaymentStatus,
                IsPaid = session.PaymentStatus == "paid"
            };
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
                    _unitOfWork.Payments.Update(payment);

                    // [Fix #4] Propagate Refunded status lên OrderGroup và tất cả Orders
                    if (payment.OrderGroupId.HasValue)
                    {
                        var orderGroup = await _unitOfWork.OrderGroups.GetByIdAsync(payment.OrderGroupId.Value);
                        if (orderGroup != null)
                        {
                            orderGroup.PaymentStatus = PaymentStatus.Refunded;
                            if (orderGroup.Orders != null)
                            {
                                foreach (var order in orderGroup.Orders)
                                {
                                    order.PaymentStatus = PaymentStatus.Refunded;
                                    await _unitOfWork.Orders.UpdateOrderAsync(order);
                                }
                            }
                        }
                    }

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