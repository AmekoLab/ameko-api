using AutoMapper;
using FPTU.Capstone.AMKCollective.Application.DTOs.Settings;
using FPTU.Capstone.AMKCollective.Application.DTOs.Wallet;
using FPTU.Capstone.AMKCollective.Application.Helpers;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Services
{
    public class WalletService : IWalletService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        //private readonly UserManager<User> _userManager;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly IEmailService _emailService;
        private readonly IPaymentService _paymentService;
        private readonly IVnPayService _vnPayService;
        private readonly WalletSettings _walletSettings;
        private readonly FrontendUrls _frontendUrls;
        private readonly INotificationService _notificationService;

        public WalletService (IUnitOfWork unitOfWork, IMapper mapper, //UserManager<User> userManager,
            IPasswordHasher<User> passwordHasher, IEmailService emailService, IPaymentService paymentService, IVnPayService vnPayService, IOptions<WalletSettings> walletOptions, IOptions<FrontendUrls> urlOptions,
            INotificationService notificationService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
           // _userManager = userManager;
            _passwordHasher = passwordHasher;
            _emailService = emailService;
            _paymentService = paymentService;
            _vnPayService = vnPayService;
            _walletSettings = walletOptions.Value;
            _frontendUrls = urlOptions.Value;
            _notificationService = notificationService;
        }

        public async Task<WalletResponse?> GetWalletByUserIdAsync(Guid userId)
        {
            var wallet = await _unitOfWork.Wallets.GetByUserIdAsync(userId);
            if (wallet == null) return null;

            return _mapper.Map<WalletResponse>(wallet);
        }

        public async Task<List<WalletTransactionResponse>> GetTransactionsAsync(Guid userId)
        {
            var wallet = await _unitOfWork.Wallets.GetByUserIdAsync(userId);
            if (wallet == null) return new List<WalletTransactionResponse>();
            var transactions = await _unitOfWork.Transactions.GetByWalletIdAsync(wallet.Id);
            return _mapper.Map<List<WalletTransactionResponse>>(transactions).ConvertDatesToLocal();
        }

        public async Task<WalletTransactionDetailResponse> GetTransactionDetailAsync(Guid transactionId, Guid userId)
        {
            var wallet = await _unitOfWork.Wallets.GetByUserIdAsync(userId);
            if (wallet == null)
                throw new KeyNotFoundException("Wallet not found."); // Ném lỗi 404

            var transaction = await _unitOfWork.Transactions.GetByIdAsync(transactionId);
            if (transaction == null)
                throw new KeyNotFoundException("Transaction not found."); // Ném lỗi 404

            // Tách riêng lỗi bảo mật (Cố tình xem giao dịch của người khác) thành 403/401
            if (transaction.WalletId != wallet.Id)
                throw new UnauthorizedAccessException("You do not have permission to view this transaction.");

            var dto = _mapper.Map<WalletTransactionDetailResponse>(transaction).ConvertDatesToLocal();
            dto.ShopName = transaction.Wallet?.User?.ShopProfile?.ShopName;

            if (transaction.Type == TransactionType.Withdrawal)
            {
                ApplyWithdrawalDetailsFromDescription(transaction.Description, dto);
            }

            return dto;
        }

        public async Task CreateWalletAsync(Guid userId)
        {
            var existing = await _unitOfWork.Wallets.GetByUserIdAsync(userId);
            if (existing != null) return;

            var wallet = new Wallet
            {
                UserId = userId,
                Balance = 0,
                HeldBalance = 0,
                Currency = "VND",
                IsActive = true
            };

            await _unitOfWork.Wallets.AddAsync(wallet);
            await _unitOfWork.CommitAsync();
        }

        public async Task RequestWithdrawalAsync(Guid userId, WithdrawRequest request)
        {
            // 1. [SECURITY] Xác thực mã PIN
            var isPinValid = await VerifyPinAsync(userId, request.WalletPin);
            if (!isPinValid)
            {
                throw new UnauthorizedAccessException("Incorrect wallet PIN.");
            }

            // 2. Lấy thông tin Wallet và Shop
            var wallet = await _unitOfWork.Wallets.GetByUserIdAsync(userId);
            if (wallet == null) throw new InvalidOperationException("Wallet does not exist.");

            var shop = await _unitOfWork.Shops.GetByUserIdAsync(userId);
            if (shop == null) throw new InvalidOperationException("Shop profile not found.");


            // 3. Kiểm tra thông tin ngân hàng của Shop
            if (string.IsNullOrEmpty(shop.BankAccountNumber) || string.IsNullOrEmpty(shop.BankName))
            {
                throw new InvalidOperationException("You have not updated your bank account information. Please go to Shop Settings to update it.");
            }

            // 4. Tính toán phí và kiểm tra số dư
            decimal feePercent = _walletSettings.WithdrawalFeePercent;
            decimal feeAmount = request.Amount * feePercent;
            decimal totalDeduct = request.Amount + feeAmount;

            if (wallet.Balance < totalDeduct)
            {
                throw new InvalidOperationException($"Insufficient balance. You need {totalDeduct:N0} VND (including fees) to complete this transaction.");
            }

            // Kiểm tra mức rút tối thiểu
            if (request.Amount < _walletSettings.MinimumWithdrawalAmount)
            {
                throw new InvalidOperationException($"Minimum withdrawal amount is {_walletSettings.MinimumWithdrawalAmount:N0} VND.");
            }

            if (wallet.Balance - totalDeduct < _walletSettings.MinimumBalanceAfterWithdrawal)
            {
                throw new InvalidOperationException("The remaining balance after withdrawal must be at least 2,000,000 VND.");
            }

            // 5. Trừ tiền trong ví ngay lập tức (Chuyển sang trạng thái chờ)
            var (success, oldBal, oldHeld) = await _unitOfWork.Wallets.UpdateBalancesAsync(wallet.Id, -totalDeduct, 0);
            if (!success) throw new InvalidOperationException("Transaction failed. Wallet balance changed concurrently.");

            // 6. TẠO RECORD VÀO BẢNG WithdrawalRequest (Thay vì bảng Payment)
            var withdrawalReq = new WithdrawalRequest
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Amount = request.Amount,
                FeeAmount = feeAmount,
                BankName = shop.BankName,
                BankAccountNumber = shop.BankAccountNumber,
                BankAccountName = shop.BankAccountName,
                Status = WithdrawalStatus.Pending,
                RequestedAt = DateTime.UtcNow
            };

            await _unitOfWork.WithdrawalRequests.AddAsync(withdrawalReq);

            // 7. Ghi log Transaction: Trừ tiền ví để rút
            var transaction = new Transaction
            {
                TransactionCode = TransactionHelper.GenerateTxCode(),
                WalletId = wallet.Id,
                Amount = totalDeduct,
                BalanceBeforeTransaction = oldBal,
                BalanceAfterTransaction = oldBal - totalDeduct,
                Direction = TransactionDirection.Out,
                Type = TransactionType.Withdrawal,
                HeldBalanceBeforeTransaction = oldHeld,
                HeldBalanceAfterTransaction = oldHeld,
                FeeAmount = feeAmount,
                Description = $"Withdrawal request to {shop.BankName} - {shop.BankAccountNumber} - {shop.BankAccountName} (Amount: {request.Amount:N0}, Fee: {feeAmount:N0})",
                Currency = "VND",
                CreatedAt = DateTime.UtcNow
            };
            await _unitOfWork.Transactions.AddAsync(transaction);
            await CreditSystemWalletAsync(feeAmount, transaction.TransactionCode, $"Withdrawal fee from {wallet.UserId}", TransactionType.PlatformFee);

            await _unitOfWork.CommitAsync();
            // TODO: gửi thông báo cho admin khi có đơn rút mới.
        }

        public async Task PayOrderWithWalletAsync(Guid userId, Guid orderId, decimal amount)
        {
            var wallet = await _unitOfWork.Wallets.GetByUserIdAsync(userId);
            if (wallet == null) throw new Exception("Wallet does not exist.");

            if (wallet.Balance < amount)
                throw new Exception("Wallet balance is insufficient for payment.");

            var (success, oldBal, oldHeld) = await _unitOfWork.Wallets.UpdateBalancesAsync(wallet.Id, -amount, 0);
            if (!success) throw new Exception("Transaction failed. Wallet balance changed concurrently.");

            var transaction = new Transaction
            {
                TransactionCode = TransactionHelper.GenerateTxCode(),
                IdempotencyKey = $"PAY_ORDER_{orderId}",
                WalletId = wallet.Id,
                RelatedOrderId = orderId,
                Amount = amount,
                BalanceBeforeTransaction = oldBal,
                BalanceAfterTransaction = oldBal - amount,
                Direction = TransactionDirection.Out,
                Type = TransactionType.OrderPayment,
                HeldBalanceBeforeTransaction = oldHeld,
                HeldBalanceAfterTransaction = oldHeld,
                Description = $"Payment for order #{orderId}",
                Currency = "VND",
                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.Transactions.AddAsync(transaction);
            await _unitOfWork.CommitAsync();
        }

        public async Task AddPendingSalesToWalletAsync(Guid shopId, Guid orderId, decimal amount, decimal feeAmount = 0, decimal systemVoucherDeduction = 0)
        {
            var wallet = await _unitOfWork.Wallets.GetByUserIdAsync(shopId);
            if (wallet == null) return;

            // [FIX B1] Query trực tiếp DB thay vì load tất cả transactions vào memory
            var existing = await _unitOfWork.Transactions.ExistsByOrderAndTypeAsync(
                wallet.Id, orderId, TransactionType.SalesPending);
            if (existing) return;

            var (_, oldBal, oldHeld) = await _unitOfWork.Wallets.UpdateBalancesAsync(wallet.Id, 0, amount);

            // amount   = shopRevenue (net shop nhận)
            // feeAmount = platform commission (đã trừ trước)
            // grossAmount = tiền khách thực trả = amount + feeAmount
            decimal grossAmount = amount + feeAmount;

            string description = systemVoucherDeduction > 0
                ? $"Pending sales revenue from order #{orderId} (Khách trả: {grossAmount:N0} | Phí HH: -{feeAmount:N0} | Voucher sàn: -{systemVoucherDeduction:N0} | Net: {amount:N0})"
                : $"Pending sales revenue from order #{orderId} (Khách trả: {grossAmount:N0} | Phí HH: -{feeAmount:N0} | Net: {amount:N0})";

            var transaction = new Transaction
            {
                TransactionCode = TransactionHelper.GenerateTxCode(),
                IdempotencyKey = $"PENDING_SALES_{orderId}",
                WalletId = wallet.Id,
                RelatedOrderId = orderId,
                Amount = grossAmount,                    // tiền khách trả (gross)
                FeeAmount = feeAmount,                   // phí hoa hồng sàn
                // NetAmount = grossAmount - feeAmount = amount (shop nhận) — tính tự động ở DTO
                BalanceBeforeTransaction = oldBal,
                BalanceAfterTransaction = oldBal,        // Held không ảnh hưởng Balance
                Direction = TransactionDirection.Held,
                Type = TransactionType.SalesPending,
                HeldBalanceBeforeTransaction = oldHeld,
                HeldBalanceAfterTransaction = oldHeld + amount, // HeldBalance tăng đúng bằng net shop nhận
                Description = description,
                Currency = "VND",
                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.Transactions.AddAsync(transaction);
            await _unitOfWork.CommitAsync();
        }

        public async Task<bool> ReleaseHeldMoneyAsync(Guid shopId, Guid orderId, decimal amount, decimal feeAmount = 0, DateTime? recognizedAt = null)
        {
            var wallet = await _unitOfWork.Wallets.GetByUserIdAsync(shopId);
            if (wallet == null) return false;

            // Idempotency: skip nếu SalesRevenue transaction đã tồn tại cho order này
            bool alreadyReleased = await _unitOfWork.Transactions
                .ExistsByOrderAndTypeAsync(wallet.Id, orderId, TransactionType.SalesRevenue);
            if (alreadyReleased) return false;

            // Release whatever is available — if HeldBalance < amount, a prior cancellation
            // already cleared it; release only what remains to avoid negative HeldBalance.
            decimal releaseAmount = Math.Min(amount, wallet.HeldBalance);
            if (releaseAmount <= 0) return false;

            var (success, oldBal, oldHeld) = await _unitOfWork.Wallets.UpdateBalancesAsync(wallet.Id, releaseAmount, -releaseAmount);
            if (!success) return false;

            decimal grossAmount = releaseAmount + feeAmount;

            var transaction = new Transaction
            {
                TransactionCode = TransactionHelper.GenerateTxCode(),
                IdempotencyKey = $"RELEASE_FUNDS_{orderId}",
                WalletId = wallet.Id,
                RelatedOrderId = orderId,
                Amount = grossAmount,
                FeeAmount = feeAmount,
                BalanceBeforeTransaction = oldBal,
                BalanceAfterTransaction = oldBal + releaseAmount,
                Direction = TransactionDirection.In,
                Type = TransactionType.SalesRevenue,
                HeldBalanceBeforeTransaction = oldHeld,
                HeldBalanceAfterTransaction = oldHeld - releaseAmount,
                Description = releaseAmount < amount
                    ? $"Partial release for order #{orderId} ({releaseAmount:N0}/{amount:N0} VND — remainder already cleared by prior cancellation)"
                    : $"Released revenue for order #{orderId}",
                Currency = "VND",
                CreatedAt = recognizedAt?.ToUniversalTime() ?? DateTime.UtcNow
            };

            await _unitOfWork.Transactions.AddAsync(transaction);
            await _unitOfWork.CommitAsync();
            return true;
        }

        public async Task RefundToWalletAsync(Guid userId, decimal amount, string reason, decimal penaltyAmount = 0m)
        {
            var wallet = await _unitOfWork.Wallets.GetByUserIdAsync(userId);
            if (wallet == null)
            {
                await CreateWalletAsync(userId);
                wallet = await _unitOfWork.Wallets.GetByUserIdAsync(userId);
                if (wallet == null) return;
            }

            decimal netRefund = amount - penaltyAmount;
            var (_, oldBal, oldHeld) = await _unitOfWork.Wallets.UpdateBalancesAsync(wallet.Id, netRefund, 0);

            var transaction = new Transaction
            {
                TransactionCode = TransactionHelper.GenerateTxCode(),
                WalletId = wallet.Id,
                Amount = amount,
                FeeAmount = penaltyAmount,
                BalanceBeforeTransaction = oldBal,
                BalanceAfterTransaction = oldBal + netRefund,
                Direction = TransactionDirection.In,
                Type = TransactionType.OrderRefund,
                HeldBalanceBeforeTransaction = oldHeld,
                HeldBalanceAfterTransaction = oldHeld,
                Description = reason,
                Currency = "VND",
                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.Transactions.AddAsync(transaction);
            await _unitOfWork.CommitAsync();
        }

        public async Task DeductFundsForRefundAsync(Guid shopId, Guid orderId, decimal amount, bool isOrderCompleted)
        {
            var wallet = await _unitOfWork.Wallets.GetByUserIdAsync(shopId);
            if (wallet == null) return;

            decimal oldBal, oldHeld;

            if (isOrderCompleted)
            {
                // Order was completed → revenue already in Balance
                decimal deductFromBalance = Math.Min(amount, wallet.Balance);
                if (deductFromBalance <= 0) return;
                var (_, ob, oh) = await _unitOfWork.Wallets.UpdateBalancesAsync(wallet.Id, -deductFromBalance, 0);
                oldBal = ob; oldHeld = oh;
                amount = deductFromBalance;
            }
            else
            {
                // Order not completed → revenue still in HeldBalance
                decimal deductFromHeld = Math.Min(amount, wallet.HeldBalance);
                if (deductFromHeld <= 0) return;
                var (_, ob, oh) = await _unitOfWork.Wallets.UpdateBalancesAsync(wallet.Id, 0, -deductFromHeld);
                oldBal = ob; oldHeld = oh;
                amount = deductFromHeld;
            }

            var transaction = new Transaction
            {
                TransactionCode = TransactionHelper.GenerateTxCode(),
                WalletId = wallet.Id,
                RelatedOrderId = orderId,
                Amount = amount,
                BalanceBeforeTransaction = oldBal,
                BalanceAfterTransaction = isOrderCompleted ? oldBal - amount : oldBal,
                Direction = isOrderCompleted ? TransactionDirection.Out : TransactionDirection.Held,
                Type = TransactionType.ManualAdjustment,
                Description = $"[REFUND DEDUCTION] Funds deducted from shop for order #{orderId} refund",
                Currency = "VND",
                CreatedAt = DateTime.UtcNow,
                HeldBalanceBeforeTransaction = oldHeld,
                HeldBalanceAfterTransaction = isOrderCompleted ? oldHeld : oldHeld - amount,
                FeeAmount = 0
            };

            await _unitOfWork.Transactions.AddAsync(transaction);
            await _unitOfWork.CommitAsync();
        }





        public async Task<PaginatedResult<WalletTransactionResponse>> GetTransactionsByFilterAsync(PaymentFilterRequest filter)
        {
            var (items, totalCount) = await _unitOfWork.Transactions.GetTransactionsByFilterAsync(filter);
            var mappedItems = _mapper.Map<List<WalletTransactionResponse>>(items).ConvertDatesToLocal();

            for (int i = 0; i < items.Count; i++)
            {
                var transaction = items[i];
                var dto = mappedItems[i];

                dto.ShopName = transaction.Wallet?.User?.ShopProfile?.ShopName;

                if (transaction.Type == TransactionType.Withdrawal)
                {
                    ApplyWithdrawalDetailsFromDescription(transaction.Description, dto);
                }
            }

            return new PaginatedResult<WalletTransactionResponse>(mappedItems, totalCount, filter.PageNumber, filter.PageSize);
        }

        private static void ApplyWithdrawalDetailsFromDescription(string? description, WalletTransactionResponse dto)
        {
            if (string.IsNullOrWhiteSpace(description)) return;

            const string prefix = "Withdrawal request to ";
            var amountIndex = description.IndexOf("(Amount:", StringComparison.OrdinalIgnoreCase);

            if (description.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                var bankSectionEnd = amountIndex > prefix.Length ? amountIndex : description.Length;
                var bankSection = description.Substring(prefix.Length, bankSectionEnd - prefix.Length).Trim();
                var parts = bankSection.Split(" - ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                if (parts.Length > 0) dto.BankName = parts[0];
                if (parts.Length > 1) dto.BankAccountNumber = parts[1];
                if (parts.Length > 2) dto.BankAccountName = parts[2];
            }

            var feeIndex = description.IndexOf("Fee:", StringComparison.OrdinalIgnoreCase);
            if (feeIndex > -1)
            {
                var feeStart = feeIndex + "Fee:".Length;
                var feeEnd = description.IndexOf(")", feeStart, StringComparison.OrdinalIgnoreCase);
                var feeText = feeEnd > feeStart
                    ? description.Substring(feeStart, feeEnd - feeStart)
                    : description.Substring(feeStart);

                if (decimal.TryParse(FilterDigits(feeText), out var feeAmount))
                {
                    dto.FeeAmount = feeAmount;
                }
            }
        }

        private static string FilterDigits(string input)
        {
            var buffer = new StringBuilder();
            foreach (var ch in input)
            {
                if (char.IsDigit(ch)) buffer.Append(ch);
            }
            return buffer.ToString();
        }



        public async Task ApproveWithdrawalAsync(Guid adminId, Guid paymentId, WithdrawalActionRequest request)
        {
            // Lấy từ bảng WithdrawalRequest (Lưu ý: FE vẫn truyền paymentId nhưng thực chất nó là Id của WithdrawalRequest)
            var withdrawalReq = await _unitOfWork.WithdrawalRequests.GetByIdAsync(paymentId);
            if (withdrawalReq == null) throw new KeyNotFoundException("Transaction not found");

            if (withdrawalReq.Status != WithdrawalStatus.Pending)
                throw new InvalidOperationException($"Cannot approve transaction with status '{withdrawalReq.Status}'. Only 'Pending' requests can be approved.");

            // Validate: Bắt buộc phải có ảnh bằng chứng chuyển khoản theo business rule
            if (string.IsNullOrEmpty(request.EvidenceImageUrl))
                throw new ArgumentException("Evidence Image (Banking Receipt) is required for approval.");

            // Cập nhật trạng thái
            withdrawalReq.Status = WithdrawalStatus.Completed;
            withdrawalReq.AdminId = adminId;
            withdrawalReq.EvidenceUrl = request.EvidenceImageUrl;
            withdrawalReq.AdminMessage = request.Reason;
            withdrawalReq.ProcessedAt = DateTime.UtcNow;

            _unitOfWork.WithdrawalRequests.Update(withdrawalReq);
            await _unitOfWork.CommitAsync();

            await _notificationService.SendNotificationAsync(
                withdrawalReq.UserId,
                "Yêu cầu rút tiền đã được duyệt",
                $"Yêu cầu rút {withdrawalReq.Amount:N0} ₫ đã được duyệt. Tiền sẽ về tài khoản ngân hàng của bạn.",
                nameof(FPTU.Capstone.AMKCollective.Domain.Enums.NotificationType.WalletTransaction),
                paymentId.ToString(),
                "WithdrawalRequest");
        }

        public async Task RejectWithdrawalAsync(Guid adminId, Guid paymentId, WithdrawalActionRequest request)
        {
            // Validate Input
            if (string.IsNullOrEmpty(request.Reason))
            {
                throw new ArgumentException("Reason is required when rejecting a withdrawal request.");
            }

            // Lấy WithdrawalRequest
            var withdrawalReq = await _unitOfWork.WithdrawalRequests.GetByIdAsync(paymentId);
            if (withdrawalReq == null) throw new KeyNotFoundException("Transaction not found");

            if (withdrawalReq.Status != WithdrawalStatus.Pending)
                throw new InvalidOperationException("Cannot reject this transaction. Only 'Pending' requests can be rejected.");

            // HOÀN TIỀN VỀ VÍ
            var wallet = await _unitOfWork.Wallets.GetByUserIdAsync(withdrawalReq.UserId);

            if (wallet != null)
            {
                decimal refundAmount = withdrawalReq.Amount + withdrawalReq.FeeAmount;

                var (_, oldBal, oldHeld) = await _unitOfWork.Wallets.UpdateBalancesAsync(wallet.Id, refundAmount, 0);

                var transaction = new Transaction
                {
                    TransactionCode = TransactionHelper.GenerateTxCode(),
                    WalletId = wallet.Id,
                    Amount = refundAmount,
                    BalanceBeforeTransaction = oldBal,
                    BalanceAfterTransaction = oldBal + refundAmount,
                    Direction = TransactionDirection.In,
                    Type = TransactionType.ManualAdjustment,
                    HeldBalanceBeforeTransaction = oldHeld,
                    HeldBalanceAfterTransaction = oldHeld,
                    FeeAmount = withdrawalReq.FeeAmount,
                    Description = $"[REJECTED] Withdrawal refunded. Reason: {request.Reason}",
                    Currency = "VND",
                    CreatedAt = DateTime.UtcNow
                };
                await _unitOfWork.Transactions.AddAsync(transaction);
            }
            else
            {
                throw new Exception("Wallet not found to refund.");
            }

            // Cập nhật trạng thái giao dịch
            withdrawalReq.Status = WithdrawalStatus.Rejected;
            withdrawalReq.AdminId = adminId;
            withdrawalReq.AdminMessage = request.Reason;
            withdrawalReq.ProcessedAt = DateTime.UtcNow;

            _unitOfWork.WithdrawalRequests.Update(withdrawalReq);
            await _unitOfWork.CommitAsync();

            await _notificationService.SendNotificationAsync(
                withdrawalReq.UserId,
                "Yêu cầu rút tiền bị từ chối",
                $"Yêu cầu rút {withdrawalReq.Amount:N0} ₫ bị từ chối. Tiền đã được hoàn lại vào ví. Lý do: {request.Reason}",
                nameof(FPTU.Capstone.AMKCollective.Domain.Enums.NotificationType.WalletTransaction),
                paymentId.ToString(),
                "WithdrawalRequest");
        }

        public async Task AdjustBalanceAsync(Guid adminId, AdjustBalanceRequest request)
        {
            var wallet = await _unitOfWork.Wallets.GetByUserIdAsync(request.UserId);
            if (wallet == null)
            {
                await CreateWalletAsync(request.UserId);
                wallet = await _unitOfWork.Wallets.GetByUserIdAsync(request.UserId);
            }

            // [FIX B3] Guard null sau CreateWalletAsync tránh NullReferenceException
            if (wallet == null)
                throw new InvalidOperationException("Failed to retrieve or create wallet for the specified user.");

            if (request.Amount < 0 && wallet.Balance + request.Amount < 0)
                throw new InvalidOperationException(
                    $"Cannot deduct {Math.Abs(request.Amount):N0} VND: shop balance ({wallet.Balance:N0} VND) is insufficient.");

            var (_, oldBal, oldHeld) = await _unitOfWork.Wallets.UpdateBalancesAsync(wallet.Id, request.Amount, 0, allowNegative: false);

            var direction = request.Amount > 0 ? TransactionDirection.In : TransactionDirection.Out;

            var transaction = new Transaction
            {
                TransactionCode = TransactionHelper.GenerateTxCode(),
                WalletId = wallet.Id,
                Amount = Math.Abs(request.Amount),
                BalanceBeforeTransaction = oldBal,
                BalanceAfterTransaction = oldBal + request.Amount,
                Direction = direction,
                Type = TransactionType.ManualAdjustment,
                HeldBalanceBeforeTransaction = oldHeld,
                HeldBalanceAfterTransaction = oldHeld,
                Description = $"{request.Reason} (Adjusted by Admin {adminId})",
                Currency = "VND",
                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.Transactions.AddAsync(transaction);
            await _unitOfWork.CommitAsync();
        }

        public async Task<bool> IsPinCreatedAsync(Guid userId)
        {
            var wallet = await _unitOfWork.Wallets.GetByUserIdAsync(userId);
            // Nếu chưa có ví hoặc PinHash null/rỗng -> chưa tạo
            return wallet != null && !string.IsNullOrEmpty(wallet.PinHash);
        }

        public async Task SetupPinAsync(Guid userId, SetupWalletPinRequest request)
        {
            // 1. Lấy User & Wallet
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null) throw new KeyNotFoundException("User not found.");

            var wallet = await _unitOfWork.Wallets.GetByUserIdAsync(userId);
            if (wallet == null)
            {
                // Nếu chưa có ví thì tạo ví trước (auto-create)
                await CreateWalletAsync(userId);
                wallet = await _unitOfWork.Wallets.GetByUserIdAsync(userId);
            }

            // 2. Validate: Nếu đã có PIN rồi thì không cho Setup lại 
            if (!string.IsNullOrEmpty(wallet!.PinHash))
            {
                throw new InvalidOperationException("Wallet PIN is already set. Please use Change PIN function.");
            }

            // 3. Verify Login Password (Lớp bảo mật 1)
            var passwordCheck = VerifyPasswordHash(request.CurrentPassword, user.HashedPassword);
            if (!passwordCheck)
            {
                throw new UnauthorizedAccessException("Incorrect login password.");
            }

            // 4. Hash PIN và lưu (Dùng user object)
            wallet.PinHash = _passwordHasher.HashPassword(user, request.NewPin);

            _unitOfWork.Wallets.Update(wallet);
            await _unitOfWork.CommitAsync();
        }

        public async Task ChangePinAsync(Guid userId, ChangeWalletPinRequest request)
        {
            // 1. Check thủ công (Phòng trường hợp DTO validation bị bypass)
            if (request.NewPin != request.ConfirmNewPin)
            {
                throw new ArgumentException("New PIN and Confirm PIN do not match.");
            }

            // 2. Validate logic cũ: Không được trùng PIN cũ (Optional - Tùy nghiệp vụ)
            if (request.OldPin == request.NewPin)
            {
                throw new ArgumentException("New PIN cannot be the same as the Old PIN.");
            }

            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            var wallet = await _unitOfWork.Wallets.GetByUserIdAsync(userId);

            if (wallet == null || string.IsNullOrEmpty(wallet.PinHash))
                throw new InvalidOperationException("Wallet PIN has not been created yet.");

            // 3. Verify Old PIN (Lớp bảo mật 2)
            if (user == null) throw new KeyNotFoundException("User not found.");

            var verifyResult = _passwordHasher.VerifyHashedPassword(user, wallet.PinHash, request.OldPin);
            if (verifyResult == PasswordVerificationResult.Failed)
            {
                throw new UnauthorizedAccessException("Incorrect old PIN.");
            }

            // 4. Update New PIN
            wallet.PinHash = _passwordHasher.HashPassword(user, request.NewPin);

            _unitOfWork.Wallets.Update(wallet);
            await _unitOfWork.CommitAsync();
        }
        public async Task SendPinResetCodeAsync(Guid userId)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            var wallet = await _unitOfWork.Wallets.GetByUserIdAsync(userId);

            if (wallet == null) throw new KeyNotFoundException("Wallet not found.");
            if (string.IsNullOrEmpty(wallet.PinHash)) throw new InvalidOperationException("You have not set up a PIN yet.");

            // 1. Tạo OTP ngẫu nhiên 6 số — [Fix] dùng RandomNumberGenerator thay new Random() (đảm bảo crypto-safe)
            string otp = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

            // 2. Lưu vào DB (Hết hạn sau 5 phút)
            wallet.PinResetCode = otp;
            wallet.PinResetExpiry = DateTime.UtcNow.AddMinutes(_walletSettings.PinResetOtpExpiryMinutes);

            _unitOfWork.Wallets.Update(wallet);
            await _unitOfWork.CommitAsync();

            // 3. Gửi Email
            // Lưu ý: Cần đảm bảo hàm SendEmailAsync trong EmailService của bạn hoạt động đúng
            string subject = "[AMK Collective] Wallet PIN Reset Verification Code";
            string body = $@"
            <h3>Wallet PIN Reset Request</h3>
            <p>Hello {user.Username},</p>
            <p>You requested to reset your Wallet PIN. Use the code below to proceed:</p>
            <h2 style='color:blue'>{otp}</h2>
            <p>This code expires in 5 minutes.</p>
            <p>If you did not request this, please secure your account immediately.</p>
        ";

            await _emailService.SendEmailAsync(user.Email, subject, body);
        }

        public async Task ResetPinWithOtpAsync(Guid userId, ResetWalletPinRequest request)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            var wallet = await _unitOfWork.Wallets.GetByUserIdAsync(userId);

            if (wallet == null) throw new KeyNotFoundException("Wallet not found.");

            // 1. Kiểm tra OTP
            if (string.IsNullOrEmpty(wallet.PinResetCode) || wallet.PinResetCode != request.Otp)
            {
                throw new ArgumentException("Invalid OTP code.");
            }

            // 2. Kiểm tra hạn OTP
            if (wallet.PinResetExpiry < DateTime.UtcNow)
            {
                throw new ArgumentException("OTP code has expired. Please request a new one.");
            }

            // 3. Đổi PIN mới
            if (user == null) throw new KeyNotFoundException("User not found."); // [Fix] null check trước khi dùng
            wallet.PinHash = _passwordHasher.HashPassword(user, request.NewPin);

            // 4. Xóa OTP cũ để không dùng lại được
            wallet.PinResetCode = null;
            wallet.PinResetExpiry = null;

            _unitOfWork.Wallets.Update(wallet);
            await _unitOfWork.CommitAsync();

            // (Optional) Gửi mail thông báo đã đổi thành công
            await _emailService.SendEmailAsync(user.Email, "Security Alert", "Your Wallet PIN has been successfully reset.");
        }

        public async Task<string> CreateDepositTransactionAsync(Guid userId, DepositRequest request, Microsoft.AspNetCore.Http.HttpContext? httpContext = null)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null) throw new KeyNotFoundException("User not found");

            if (request.Method == PaymentMethod.VnPay)
            {
                if (httpContext == null) throw new ArgumentNullException(nameof(httpContext), "HttpContext is required for VNPay deposit.");

                var vnpayPayment = new Payment
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Amount = request.Amount,
                    Type = PaymentType.Deposit,
                    Status = PaymentStatus.Pending,
                    Method = PaymentMethod.VnPay,
                    Currency = "VND",
                    Description = "Top up wallet via VNPay",
                    CreatedAt = DateTime.UtcNow
                };

                await _unitOfWork.Payments.AddAsync(vnpayPayment);
                await _unitOfWork.CommitAsync();

                return await _vnPayService.CreateDepositUrlAsync(request.Amount, vnpayPayment.Id, httpContext);
            }

            // Default: Stripe (CreditCard)
            // Gọi PaymentService
            var stripeResult = await _paymentService.CreateDepositSessionAsync(
                request.Amount,
                user.Email,
                userId.ToString(),
                _frontendUrls.DepositSuccessPath,
                _frontendUrls.DepositCancelPath
            );

            // Tạo Payment Record
            var payment = new Payment
            {
                Id = Guid.NewGuid(), 
                UserId = userId,
                Amount = request.Amount,
                Type = PaymentType.Deposit,
                Status = PaymentStatus.Pending,
                Method = PaymentMethod.CreditCard,
                StripeSessionId = stripeResult.SessionId,
                Currency = "VND",
                Description = "Top up wallet",
                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.Payments.AddAsync(payment);
            await _unitOfWork.CommitAsync();

            // Return the payment URL
            return stripeResult.PaymentUrl;
        }

        public async Task<string> CreateDepositTransactionMobileAsync(Guid userId, DepositMobileRequest request)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null) throw new KeyNotFoundException("User not found");

            // For Mobile, we use the custom URLs provided (e.g. ameko://payment/success)
            var successUrl = request.SuccessUrl;
            var cancelUrl = request.CancelUrl;

            var stripeResult = await _paymentService.CreateDepositSessionAsync(
                request.Amount,
                user.Email,
                userId.ToString(),
                successUrl,
                cancelUrl
            );

            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Amount = request.Amount,
                Type = PaymentType.Deposit,
                Status = PaymentStatus.Pending,
                Method = PaymentMethod.CreditCard,
                StripeSessionId = stripeResult.SessionId,
                Currency = "VND",
                Description = "Top up wallet (Mobile)",
                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.Payments.AddAsync(payment);
            await _unitOfWork.CommitAsync();

            return stripeResult.PaymentUrl;
        }

        public async Task CreditVnPayDepositAsync(Guid userId, decimal amount, Guid paymentId)
        {
            var wallet = await _unitOfWork.Wallets.GetByUserIdAsync(userId);
            if (wallet == null) throw new InvalidOperationException("Wallet not found.");

            var (success, oldBal, oldHeld) = await _unitOfWork.Wallets.UpdateBalancesAsync(wallet.Id, amount, 0);
            if (!success) throw new InvalidOperationException($"Failed to update wallet balance. WalletId: {wallet.Id}");

            var txCode = $"DEP-{DateTime.UtcNow:yyMMdd}-{Guid.NewGuid().ToString().Substring(0, 6).ToUpper()}";
            var transaction = new Transaction
            {
                TransactionCode = txCode,
                IdempotencyKey = $"VNPAY_DEPOSIT_{paymentId}",
                WalletId = wallet.Id,
                Amount = amount,
                BalanceBeforeTransaction = oldBal,
                BalanceAfterTransaction = oldBal + amount,
                Direction = TransactionDirection.In,
                Type = TransactionType.Deposit,
                HeldBalanceBeforeTransaction = oldHeld,
                HeldBalanceAfterTransaction = oldHeld,
                FeeAmount = 0,
                Description = $"Deposit via VNPay (Payment: {paymentId})",
                Currency = "VND",
                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.Transactions.AddAsync(transaction);
            await _unitOfWork.CommitAsync();

            await _notificationService.SendNotificationAsync(
                userId,
                "Nạp tiền thành công",
                $"Ví của bạn vừa được cộng {amount:N0} ₫ qua VNPay.",
                nameof(FPTU.Capstone.AMKCollective.Domain.Enums.NotificationType.WalletTransaction),
                paymentId.ToString(),
                "Payment");
        }

        public async Task<WalletStatisticsResponse> GetWalletStatisticsAsync(Guid userId)
        {
            var wallet = await _unitOfWork.Wallets.GetByUserIdAsync(userId);
            if (wallet == null) return new WalletStatisticsResponse();

            var now = DateTime.UtcNow;

            // Query thẳng DB, không load vào memory
            var totalRevenue = await _unitOfWork.Transactions
                .SumAmountByTypeAsync(wallet.Id, TransactionType.SalesRevenue);

            var thisMonthRevenue = await _unitOfWork.Transactions
                .SumAmountByTypeAsync(wallet.Id, TransactionType.SalesRevenue, now.Month, now.Year);

            var withdrawals = await _unitOfWork.WithdrawalRequests.GetByUserIdAsync(userId);

            return new WalletStatisticsResponse
            {
                AvailableBalance = wallet.Balance,
                HeldBalance = wallet.HeldBalance,
                TotalRevenue = totalRevenue,
                ThisMonthRevenue = thisMonthRevenue,
                TotalWithdrawn = withdrawals.Where(w => w.Status == WithdrawalStatus.Completed).Sum(w => w.Amount),
                PendingWithdrawal = withdrawals.Where(w => w.Status == WithdrawalStatus.Pending).Sum(w => w.Amount)
            };
        }

        public async Task<ShopStatementResponse> GetShopStatementAsync(Guid userId, int month, int year)
        {
            if (month < 1 || month > 12)
                throw new ArgumentException("Month must be between 1 and 12.", nameof(month));
            if (year < 2000 || year > 2100)
                throw new ArgumentException("Year is out of range.", nameof(year));

            var wallet = await _unitOfWork.Wallets.GetByUserIdAsync(userId);
            if (wallet == null)
                throw new KeyNotFoundException("Wallet not found.");

            var fromUtc = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
            var toUtc = fromUtc.AddMonths(1);

            // ASC để tính closing balance — phần tử cuối là transaction mới nhất
            var transactionsAsc = await _unitOfWork.Transactions.GetByWalletIdInRangeAsync(wallet.Id, fromUtc, toUtc);
            // DESC để hiển thị cho FE — mới nhất trước
            var transactions = await _unitOfWork.Transactions.GetByWalletIdInRangeDescAsync(wallet.Id, fromUtc, toUtc);

            // Opening balance = balance after the last transaction before this period.
            // If no prior transaction exists, opening = current balance - net delta of this period would be wrong;
            // use 0 as opening when wallet had no activity, otherwise pull from the latest pre-period txn snapshot.
            decimal openingBalance = 0m;
            decimal openingHeld = 0m;
            var lastBefore = await _unitOfWork.Transactions.GetLastBeforeAsync(wallet.Id, fromUtc);
            if (lastBefore != null)
            {
                openingBalance = lastBefore.BalanceAfterTransaction;
                openingHeld = lastBefore.HeldBalanceAfterTransaction;
            }

            decimal closingBalance = openingBalance;
            decimal closingHeld = openingHeld;
            if (transactionsAsc.Count > 0)
            {
                var last = transactionsAsc[transactionsAsc.Count - 1];
                closingBalance = last.BalanceAfterTransaction;
                closingHeld = last.HeldBalanceAfterTransaction;
            }

            decimal totalSalesRevenue = transactions
                .Where(t => t.Type == TransactionType.SalesRevenue)
                .Sum(t => t.Amount - t.FeeAmount); // net shop received
            decimal totalSalesPending = transactions
                .Where(t => t.Type == TransactionType.SalesPending)
                .Sum(t => t.Amount - t.FeeAmount);
            decimal totalRefundsDeducted = transactions
                .Where(t => t.Type == TransactionType.OrderRefund && t.Direction == TransactionDirection.Out)
                .Sum(t => t.Amount)
                + transactions
                    .Where(t => t.Type == TransactionType.ManualAdjustment
                                && (t.Direction == TransactionDirection.Out || t.Direction == TransactionDirection.Held)
                                && t.Description != null
                                && t.Description.Contains("REFUND DEDUCTION", StringComparison.OrdinalIgnoreCase))
                    .Sum(t => t.Amount);
            decimal totalWithdrawals = transactions
                .Where(t => t.Type == TransactionType.Withdrawal)
                .Sum(t => t.Amount - t.FeeAmount); // net: chỉ tính số tiền shop thực rút, không tính phí
            decimal totalPlatformFees = transactions
                .Where(t => t.Type == TransactionType.SalesRevenue)
                .Sum(t => t.FeeAmount)
                + transactions
                    .Where(t => t.Type == TransactionType.Withdrawal)
                    .Sum(t => t.FeeAmount); // bao gồm cả withdrawal fees

            // Pending withdrawals at end of period: any WithdrawalRequest still in Pending whose RequestedAt < toUtc.
            var allUserWithdrawals = await _unitOfWork.WithdrawalRequests.GetByUserIdAsync(userId);
            decimal totalPendingWithdrawals = allUserWithdrawals
                .Where(w => w.Status == WithdrawalStatus.Pending && w.RequestedAt < toUtc)
                .Sum(w => w.Amount);

            var mapped = _mapper.Map<List<WalletTransactionResponse>>(transactions).ConvertDatesToLocal();

            return new ShopStatementResponse
            {
                Month = month,
                Year = year,
                OpeningBalance = openingBalance,
                ClosingBalance = closingBalance,
                OpeningHeldBalance = openingHeld,
                ClosingHeldBalance = closingHeld,
                TotalSalesRevenue = totalSalesRevenue,
                TotalSalesPending = totalSalesPending,
                TotalRefundsDeducted = totalRefundsDeducted,
                TotalWithdrawals = totalWithdrawals,
                TotalPendingWithdrawals = totalPendingWithdrawals,
                TotalPlatformFees = totalPlatformFees,
                TransactionCount = transactions.Count,
                Transactions = mapped
            };
        }

        public async Task<List<HeldTransactionResponse>> GetHeldTransactionsAsync(Guid userId)
        {
            var wallet = await _unitOfWork.Wallets.GetByUserIdAsync(userId);
            if (wallet == null) return new List<HeldTransactionResponse>();

            // Query thẳng DB chỉ lấy SalesPending
            var heldTransactions = await _unitOfWork.Transactions
                .GetByWalletIdAndTypeAsync(wallet.Id, TransactionType.SalesPending);

            return _mapper.Map<List<HeldTransactionResponse>>(heldTransactions).ConvertDatesToLocal();
        }
        public async Task PayOrderGroupWithWalletAsync(Guid userId, Guid orderGroupId, decimal amount)
        {
            var wallet = await _unitOfWork.Wallets.GetByUserIdAsync(userId);
            if (wallet == null) throw new InvalidOperationException("Wallet does not exist.");

            if (wallet.Balance < amount)
                throw new InvalidOperationException("Your wallet balance is insufficient to complete this checkout.");

            // Trừ tiền trong ví
            var (success, oldBal, oldHeld) = await _unitOfWork.Wallets.UpdateBalancesAsync(wallet.Id, -amount, 0);
            if (!success) throw new InvalidOperationException("Transaction failed due to concurrent update. Please try again.");

            // Ghi nhận vào Sổ cái (Transaction) bằng OrderGroupId
            var transaction = new Transaction
            {
                TransactionCode = TransactionHelper.GenerateTxCode(),
                IdempotencyKey = $"PAY_ORDER_GROUP_{orderGroupId}",
                WalletId = wallet.Id,
                OrderGroupId = orderGroupId,
                Amount = amount,
                BalanceBeforeTransaction = oldBal,
                BalanceAfterTransaction = oldBal - amount,
                Direction = TransactionDirection.Out,
                Type = TransactionType.OrderPayment,
                HeldBalanceBeforeTransaction = oldHeld,
                HeldBalanceAfterTransaction = oldHeld,
                Description = $"Payment for Order Group #{orderGroupId}",
                Currency = "VND",
                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.Transactions.AddAsync(transaction);
            await _unitOfWork.CommitAsync();
        }

        // Helper
        // dùng cho các API Rút tiền/Update Bank 
        public async Task<bool> VerifyPinAsync(Guid userId, string pin)
        {         
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            var wallet = await _unitOfWork.Wallets.GetByUserIdAsync(userId);

            if (wallet == null || string.IsNullOrEmpty(wallet.PinHash)) return false;
            if (user == null) return false;

            // Kiểm tra Hash thủ công
            var result = _passwordHasher.VerifyHashedPassword(user, wallet.PinHash, pin);
            return result != PasswordVerificationResult.Failed;
        }

        private bool VerifyPasswordHash(string password, string storedFullHash)
        {
            var parts = storedFullHash.Split(':');
            if (parts.Length != 2) return false;

            var salt = Convert.FromBase64String(parts[0]);
            var storedHash = parts[1];

            using var hmac = new HMACSHA512(salt);
            var computedHash = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(password)));
            return computedHash == storedHash;
        }

        // ────────────────────────────────────────────────────────────────
        // Withdrawal History – Shop & Admin
        // ────────────────────────────────────────────────────────────────

        /// <summary>Shop xem lịch sử rút tiền của mình với đầy đủ Status.</summary>
        public async Task<PaginatedResult<WithdrawalSummaryResponse>> GetMyWithdrawalHistoryAsync(Guid userId, int pageIndex, int pageSize)
        {
            var (items, totalCount) = await _unitOfWork.WithdrawalRequests.GetByUserIdPagedAsync(userId, pageIndex, pageSize);
            var mapped = items.Select(w => MapToWithdrawalSummary(w)).ToList();
            return new PaginatedResult<WithdrawalSummaryResponse>(mapped, totalCount, pageIndex, pageSize);
        }

        /// <summary>Admin xem danh sách đơn rút đang Pending từ WithdrawalRequest table (fix endpoint broken).</summary>
        public async Task<PaginatedResult<WithdrawalSummaryResponse>> GetAdminPendingWithdrawalsAsync(int pageIndex, int pageSize, string? shopName = null)
        {
            var (items, totalCount) = await _unitOfWork.WithdrawalRequests.GetPendingPagedAsync(pageIndex, pageSize, shopName);
            var mapped = items.Select(w => MapToWithdrawalSummary(w)).ToList();
            return new PaginatedResult<WithdrawalSummaryResponse>(mapped, totalCount, pageIndex, pageSize);
        }

        /// <summary>Admin xem lịch sử đơn rút đã xử lý (Completed / Rejected).</summary>
        public async Task<PaginatedResult<WithdrawalSummaryResponse>> GetAdminProcessedWithdrawalsAsync(int pageIndex, int pageSize)
        {
            var (items, totalCount) = await _unitOfWork.WithdrawalRequests.GetProcessedPagedAsync(pageIndex, pageSize);
            var mapped = items.Select(w => MapToWithdrawalSummary(w)).ToList();
            return new PaginatedResult<WithdrawalSummaryResponse>(mapped, totalCount, pageIndex, pageSize);
        }

        /// <summary>Admin xem thông tin ví của một user/shop cụ thể.</summary>
        public async Task<WalletResponse?> GetWalletByUserIdForAdminAsync(Guid targetUserId)
        {
            var wallet = await _unitOfWork.Wallets.GetByUserIdAsync(targetUserId);
            if (wallet == null) return null;
            return _mapper.Map<WalletResponse>(wallet);
        }

        public async Task<WalletTransactionDetailResponse> GetTransactionDetailForAdminAsync(Guid transactionId)
        {
            // Lấy transaction từ DB (hàm GetByIdAsync đã bao gồm Include Wallet -> User -> ShopProfile)
            var transaction = await _unitOfWork.Transactions.GetByIdAsync(transactionId);

            if (transaction == null)
                throw new KeyNotFoundException("Transaction not found.");

            // Map sang DTO và convert giờ Local
            var dto = _mapper.Map<WalletTransactionDetailResponse>(transaction).ConvertDatesToLocal();

            // Gán thêm thông tin tên Shop
            dto.ShopName = transaction.Wallet?.User?.ShopProfile?.ShopName;

            // Xử lý bóc tách thông tin ngân hàng nếu là giao dịch rút tiền
            if (transaction.Type == TransactionType.Withdrawal)
            {
                ApplyWithdrawalDetailsFromDescription(transaction.Description, dto);
            }

            return dto;
        }
        // ────────────────────────────────────────────────────────────────
        // Private helpers
        // ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Map WithdrawalRequest entity → WithdrawalSummaryResponse.
        /// FeeAmount được tính lại theo config hiện tại.
        /// </summary>
        private WithdrawalSummaryResponse MapToWithdrawalSummary(WithdrawalRequest w)
        {
            return new WithdrawalSummaryResponse
            {
                Id = w.Id,
                Amount = w.Amount,
                FeeAmount = w.FeeAmount,
                TotalDeducted = w.Amount + w.FeeAmount,
                BankName = w.BankName,
                BankAccountNumber = w.BankAccountNumber,
                BankAccountName = w.BankAccountName,
                Status = w.Status.ToString(),
                AdminMessage = w.AdminMessage,
                EvidenceUrl = w.EvidenceUrl,
                ShopName = w.User?.ShopProfile?.ShopName,
                UserId = w.UserId,
                RequestedAt = w.RequestedAt.ConvertToLocalTime(),
                ProcessedAt = w.ProcessedAt?.ConvertToLocalTime()
            };
        }

        public async Task CreditPlatformFeeAsync(decimal amount, string description)
        {
            if (amount <= 0) return;
            var txCode = TransactionHelper.GenerateTxCode();
            await CreditSystemWalletAsync(amount, txCode, description, TransactionType.PlatformFee);
            await _unitOfWork.CommitAsync();
        }

        private async Task CreditSystemWalletAsync(decimal amount, string sharedTxCode, string description, TransactionType type)
        {
            if (amount <= 0) return;

            var systemWalletId = _walletSettings.SystemWalletId;
            var systemWallet = await _unitOfWork.Wallets.GetByIdAsync(systemWalletId);

            if (systemWallet == null) return;

            var (_, oldBal, oldHeld) = await _unitOfWork.Wallets.UpdateBalancesAsync(systemWallet.Id, amount, 0);

            var sysTx = new Transaction
            {
                TransactionCode = TransactionHelper.GenerateTxCode(),
                IdempotencyKey = $"SYS_FEE_{sharedTxCode}",
                WalletId = systemWallet.Id,
                Amount = amount,
                Direction = TransactionDirection.In,
                Type = type,
                Description = description,
                BalanceBeforeTransaction = oldBal,
                BalanceAfterTransaction = oldBal + amount,
                HeldBalanceBeforeTransaction = oldHeld,
                HeldBalanceAfterTransaction = oldHeld,
                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.Transactions.AddAsync(sysTx);
        }

    }
}
