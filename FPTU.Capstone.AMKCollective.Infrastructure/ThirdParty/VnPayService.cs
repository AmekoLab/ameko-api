using FPTU.Capstone.AMKCollective.Application.DTOs.Payment;
using FPTU.Capstone.AMKCollective.Application.DTOs.Settings;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Application.Helpers;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using FPTU.Capstone.AMKCollective.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Infrastructure.ThirdParty
{
    public class VnPayService : IVnPayService
    {
        private readonly VnPaySettings _vnpaySettings;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IServiceProvider _serviceProvider;
        private readonly OrderSettings _orderSettings;

        public VnPayService(IOptions<VnPaySettings> vnpaySettings, IUnitOfWork unitOfWork, IServiceProvider serviceProvider, IOptions<OrderSettings> orderOptions)
        {
            _vnpaySettings = vnpaySettings.Value;
            _unitOfWork = unitOfWork;
            _serviceProvider = serviceProvider;
            _orderSettings = orderOptions.Value;
        }

        public async Task<string> CreatePaymentUrlAsync(CreateCheckoutSessionRequest request, Guid requestingUserId, HttpContext context)
        {
            var orderGroup = await _unitOfWork.OrderGroups.GetByIdAsync(request.OrderGroupId);
            if (orderGroup == null) throw new KeyNotFoundException("Order Group not found");

            // Kiểm tra bảo mật: Đúng owner mới được thanh toán
            if (orderGroup.CustomerId != requestingUserId)
                throw new UnauthorizedAccessException("You are not authorized to pay for this order.");

            // VNPay yêu cầu vnp_TxnRef phải là duy nhất cho mỗi lần bấm thanh toán.
            // Nếu dùng OrderGroupId thì khi thanh toán hỏng, user bấm lại sẽ bị lỗi "Mã giao dịch trùng lặp".
            // => Giải pháp: Nối OrderGroupId với số Tick thời gian (VD: "58ac33..._638123456789")
            var tick = DateTime.Now.Ticks.ToString();
            var txnRef = $"{orderGroup.Id}_{tick}";

            var pay = new VnPayLibrary();
            pay.AddRequestData("vnp_Version", _vnpaySettings.Version);
            pay.AddRequestData("vnp_Command", _vnpaySettings.Command);
            pay.AddRequestData("vnp_TmnCode", _vnpaySettings.TmnCode);
            pay.AddRequestData("vnp_Amount", ((long)(orderGroup.TotalGroupAmount * 100)).ToString());
            pay.AddRequestData("vnp_CreateDate", DateTime.Now.ToString("yyyyMMddHHmmss"));
            pay.AddRequestData("vnp_CurrCode", _vnpaySettings.CurrCode);
            pay.AddRequestData("vnp_IpAddr", pay.GetIpAddress(context));
            pay.AddRequestData("vnp_Locale", _vnpaySettings.Locale);

            pay.AddRequestData("vnp_OrderInfo", $"AMK Collective - Payment for OrderGroup {orderGroup.Id}");
            pay.AddRequestData("vnp_OrderType", "other");
            pay.AddRequestData("vnp_ReturnUrl", _vnpaySettings.ReturnUrl);
            pay.AddRequestData("vnp_TxnRef", txnRef);

            var paymentUrl = pay.CreateRequestUrl(_vnpaySettings.BaseUrl, _vnpaySettings.HashSecret);

            return paymentUrl;
        }

        public async Task<string> CreatePaymentUrlMobileAsync(CreateCheckoutSessionRequest request, Guid requestingUserId, HttpContext context, string returnUrl)
        {
            var orderGroup = await _unitOfWork.OrderGroups.GetByIdAsync(request.OrderGroupId);
            if (orderGroup == null) throw new KeyNotFoundException("Order Group not found");

            if (orderGroup.CustomerId != requestingUserId)
                throw new UnauthorizedAccessException("You are not authorized to pay for this order.");

            var tick = DateTime.Now.Ticks.ToString();
            var txnRef = $"{orderGroup.Id}_{tick}";

            var pay = new VnPayLibrary();
            pay.AddRequestData("vnp_Version", _vnpaySettings.Version);
            pay.AddRequestData("vnp_Command", _vnpaySettings.Command);
            pay.AddRequestData("vnp_TmnCode", _vnpaySettings.TmnCode);
            pay.AddRequestData("vnp_Amount", ((long)(orderGroup.TotalGroupAmount * 100)).ToString());
            pay.AddRequestData("vnp_CreateDate", DateTime.Now.ToString("yyyyMMddHHmmss"));
            pay.AddRequestData("vnp_CurrCode", _vnpaySettings.CurrCode);
            pay.AddRequestData("vnp_IpAddr", pay.GetIpAddress(context));
            pay.AddRequestData("vnp_Locale", _vnpaySettings.Locale);

            pay.AddRequestData("vnp_OrderInfo", $"AMK Collective - Payment for OrderGroup {orderGroup.Id}");
            pay.AddRequestData("vnp_OrderType", "other");
            pay.AddRequestData("vnp_ReturnUrl", returnUrl); // Sử dụng returnUrl truyền vào
            pay.AddRequestData("vnp_TxnRef", txnRef);

            var paymentUrl = pay.CreateRequestUrl(_vnpaySettings.BaseUrl, _vnpaySettings.HashSecret);

            return paymentUrl;
        }

        public async Task<string> CreateDepositUrlAsync(decimal amount, Guid paymentId, HttpContext context)
        {
            // DEP_{paymentId}_{tick} — prefix "DEP" phân biệt deposit vs order trong IPN và vnpay-return
            var tick = DateTime.Now.Ticks.ToString();
            var txnRef = $"DEP_{paymentId}_{tick}";

            var pay = new VnPayLibrary();
            pay.AddRequestData("vnp_Version", _vnpaySettings.Version);
            pay.AddRequestData("vnp_Command", _vnpaySettings.Command);
            pay.AddRequestData("vnp_TmnCode", _vnpaySettings.TmnCode);
            pay.AddRequestData("vnp_Amount", ((long)(amount * 100)).ToString());
            pay.AddRequestData("vnp_CreateDate", DateTime.Now.ToString("yyyyMMddHHmmss"));
            pay.AddRequestData("vnp_CurrCode", _vnpaySettings.CurrCode);
            pay.AddRequestData("vnp_IpAddr", pay.GetIpAddress(context));
            pay.AddRequestData("vnp_Locale", _vnpaySettings.Locale);
            pay.AddRequestData("vnp_OrderInfo", $"AMK Wallet Deposit {paymentId}");
            pay.AddRequestData("vnp_OrderType", "other");
            // Dùng cùng ReturnUrl với order — backend vnpay-return nhận biết deposit qua prefix "DEP_"
            pay.AddRequestData("vnp_ReturnUrl", _vnpaySettings.ReturnUrl);
            pay.AddRequestData("vnp_TxnRef", txnRef);

            return pay.CreateRequestUrl(_vnpaySettings.BaseUrl, _vnpaySettings.HashSecret);
        }

        public async Task<PaymentResponseModel> ProcessIpnAsync(IQueryCollection collections)
        {
            var pay = new VnPayLibrary();
            var response = pay.GetFullResponseData(collections, _vnpaySettings.HashSecret);
            var transactionStatus = collections["vnp_TransactionStatus"].ToString();
            response.IsPaid = response.Success
                              && response.VnPayResponseCode == "00"
                              && (string.IsNullOrEmpty(transactionStatus) || transactionStatus == "00");
            if (!response.Success) return response; // Sai chữ ký bảo mật

            var parts = response.OrderId.Split('_');

            // Deposit: DEP_{paymentId}_{tick}
            if (parts.Length >= 2 && parts[0] == "DEP" && Guid.TryParse(parts[1], out Guid paymentId))
            {
                if (response.VnPayResponseCode == "00")
                    await FulfillDepositAsync(paymentId);
            }
            // Order: {orderGroupId}_{tick}
            else if (parts.Length > 0 && Guid.TryParse(parts[0], out Guid orderGroupId))
            {
                if (response.VnPayResponseCode == "00")
                    await FulfillOrderAsync(orderGroupId, response.TransactionId, response.OrderId, response.Amount);
            }

            return response;
        }
        private async Task FulfillDepositAsync(Guid paymentId)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var walletService = scope.ServiceProvider.GetRequiredService<IWalletService>();

                var payment = await _unitOfWork.Payments.GetByIdAsync(paymentId);
                if (payment == null || payment.Status == PaymentStatus.Paid) return; // idempotency guard

                payment.Status = PaymentStatus.Paid;
                _unitOfWork.Payments.Update(payment);
                await _unitOfWork.CommitAsync();

                await walletService.CreditVnPayDepositAsync(payment.UserId, payment.Amount, paymentId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[VNPAY DEPOSIT IPN ERROR] {ex.Message}");
                throw;
            }
        }

        private async Task FulfillOrderAsync(Guid orderGroupId, string transactionId, string vnPaySessionId, decimal amount)
        {
            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var walletService = scope.ServiceProvider.GetRequiredService<IWalletService>();
                    var orderGroup = await _unitOfWork.OrderGroups.GetByIdAsync(orderGroupId);

                    if (orderGroup == null) return;

                    // Idempotency guard - Tránh 1 IPN gọi 2 lần cộng tiền 2 lần
                    if (orderGroup.PaymentStatus == PaymentStatus.Paid) return;

                    orderGroup.PaymentStatus = PaymentStatus.Paid;

                    var shopPendingSales = new List<(Guid ShopUserId, Guid OrderId, decimal Amount, decimal FeeAmount)>();
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
                                    shopPendingSales.Add((shopProfile.UserId, order.Id, shopRevenue, feeAmount));
                                }
                            }
                        }
                    }

                    // Lưu lịch sử Payment
                    var payment = new FPTU.Capstone.AMKCollective.Domain.Entities.Payment
                    {
                        Id = Guid.NewGuid(),
                        OrderGroupId = orderGroupId,
                        UserId = orderGroup.CustomerId,
                        Amount = amount,
                        Currency = "vnd",
                        // Mượn tạm 2 field của Stripe để lưu mã giao dịch VNPay
                        StripePaymentIntentId = transactionId,
                        StripeSessionId = vnPaySessionId,
                        Method = PaymentMethod.VnPay,
                        Status = PaymentStatus.Paid,
                        Type = PaymentType.OrderPayment,
                        CreatedAt = DateTime.UtcNow
                    };

                    await _unitOfWork.Payments.AddAsync(payment);

                    // Commit db 
                    await _unitOfWork.CommitAsync();

                    // Update ví Shop
                    foreach (var (shopUserId, orderId, amt, feeAmt) in shopPendingSales)
                    {
                        await walletService.AddPendingSalesToWalletAsync(shopUserId, orderId, amt, feeAmt);
                    }

                    // Notify shop: đơn hàng mới
                    var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
                    foreach (var (shopUserId, orderId, _, _) in shopPendingSales)
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
                Console.WriteLine($"[VNPAY IPN ERROR] {ex.Message}");
                throw;
            }
        }
    }
}
