using AutoMapper;
using FPTU.Capstone.AMKCollective.Application.DTOs.Settings;
using FPTU.Capstone.AMKCollective.Application.DTOs.Wallet;
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
        private readonly WalletSettings _walletSettings;
        private readonly FrontendUrls _frontendUrls;

        public WalletService (IUnitOfWork unitOfWork, IMapper mapper, //UserManager<User> userManager,
            IPasswordHasher<User> passwordHasher, IEmailService emailService, IPaymentService paymentService, IOptions<WalletSettings> walletOptions, IOptions<FrontendUrls> urlOptions)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
           // _userManager = userManager;
            _passwordHasher = passwordHasher;
            _emailService = emailService;
            _paymentService = paymentService;
            _walletSettings = walletOptions.Value;
            _frontendUrls = urlOptions.Value;
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
            return _mapper.Map<List<WalletTransactionResponse>>(transactions);
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
            bool success = await _unitOfWork.Wallets.UpdateBalancesAsync(wallet.Id, -totalDeduct, 0);
            if (!success) throw new InvalidOperationException("Transaction failed. Wallet balance changed concurrently.");

            // 6. TẠO RECORD VÀO BẢNG WithdrawalRequest (Thay vì bảng Payment)
            var withdrawalReq = new WithdrawalRequest
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Amount = request.Amount,
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
                WalletId = wallet.Id,
                Amount = totalDeduct,
                Type = TransactionType.Withdrawal,
                Description = $"Withdrawal request to {shop.BankName} - {shop.BankAccountNumber} (Amount: {request.Amount:N0}, Fee: {feeAmount:N0})",
                Currency = "VND",
                CreatedAt = DateTime.UtcNow
            };
            await _unitOfWork.Transactions.AddAsync(transaction);

            await _unitOfWork.CommitAsync();
            // TODO: gửi thông báo cho admin khi có đơn rút mới.
        }

        public async Task PayOrderWithWalletAsync(Guid userId, Guid orderId, decimal amount)
        {
            var wallet = await _unitOfWork.Wallets.GetByUserIdAsync(userId);
            if (wallet == null) throw new Exception("Wallet does not exist.");

            if (wallet.Balance < amount)
                throw new Exception("Wallet balance is insufficient for payment.");

            bool success = await _unitOfWork.Wallets.UpdateBalancesAsync(wallet.Id, -amount, 0);
            if (!success) throw new Exception("Transaction failed. Wallet balance changed concurrently.");

            var transaction = new Transaction
            {
                WalletId = wallet.Id,
                RelatedOrderId = orderId,
                Amount = amount,
                Type = TransactionType.OrderPayment,
                Description = $"Payment for order #{orderId}",
                Currency = "VND",
                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.Transactions.AddAsync(transaction);
            await _unitOfWork.CommitAsync();
        }

        public async Task AddPendingSalesToWalletAsync(Guid shopId, Guid orderId, decimal amount)
        {
            var wallet = await _unitOfWork.Wallets.GetByUserIdAsync(shopId);
            if (wallet == null) return;

            var existing = (await _unitOfWork.Transactions.GetByWalletIdAsync(wallet.Id))
                .Any(t => t.RelatedOrderId == orderId && t.Type == TransactionType.SalesPending);
            if (existing) return;

            await _unitOfWork.Wallets.UpdateBalancesAsync(wallet.Id, 0, amount);

            var transaction = new Transaction
            {
                WalletId = wallet.Id,
                RelatedOrderId = orderId,
                Amount = amount,
                Type = TransactionType.SalesPending,
                Description = $"Pending sales revenue from order #{orderId}",
                Currency = "VND",
                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.Transactions.AddAsync(transaction);
            await _unitOfWork.CommitAsync();
        }

        public async Task ReleaseHeldMoneyAsync(Guid shopId, Guid orderId, decimal amount)
        {
            var wallet = await _unitOfWork.Wallets.GetByUserIdAsync(shopId);
            if (wallet == null) return;

            if (wallet.HeldBalance >= amount)
            {
                bool success = await _unitOfWork.Wallets.UpdateBalancesAsync(wallet.Id, amount, -amount);
                if (!success) return;

                var transaction = new Transaction
                {
                    WalletId = wallet.Id,
                    RelatedOrderId = orderId,
                    Amount = amount,
                    Type = TransactionType.SalesRevenue,
                    Description = $"Released revenue for order #{orderId}",
                    Currency = "VND",
                    CreatedAt = DateTime.UtcNow
                };

                await _unitOfWork.Transactions.AddAsync(transaction);
                await _unitOfWork.CommitAsync();
            }
        }

        public async Task RefundToWalletAsync(Guid userId, decimal amount, string reason)
        {
            var wallet = await _unitOfWork.Wallets.GetByUserIdAsync(userId);
            if (wallet == null)
            {
                await CreateWalletAsync(userId);
                wallet = await _unitOfWork.Wallets.GetByUserIdAsync(userId);
                if (wallet == null) return;
            }

            await _unitOfWork.Wallets.UpdateBalancesAsync(wallet.Id, amount, 0);

            var transaction = new Transaction
            {
                WalletId = wallet.Id,
                Amount = amount,
                Type = TransactionType.OrderRefund,
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

            if (isOrderCompleted)
            {
                await _unitOfWork.Wallets.UpdateBalancesAsync(wallet.Id, -amount, 0, true);
            }
            else
            {
                await _unitOfWork.Wallets.UpdateBalancesAsync(wallet.Id, 0, -amount, true);
            }

            var transaction = new Transaction
            {
                WalletId = wallet.Id,
                RelatedOrderId = orderId,
                Amount = amount,
                Type = TransactionType.OrderRefund,
                Description = $"Refund deduction for Order #{orderId}",
                Currency = "VND",
                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.Transactions.AddAsync(transaction);
            await _unitOfWork.CommitAsync();
        }



        public async Task<PaginatedResult<WalletTransactionResponse>> GetTransactionsByFilterAsync(PaymentFilterRequest filter)
        {
            var (items, totalCount) = await _unitOfWork.Transactions.GetTransactionsByFilterAsync(filter);
            var mappedItems = _mapper.Map<List<WalletTransactionResponse>>(items);
            return new PaginatedResult<WalletTransactionResponse>(mappedItems, totalCount, filter.PageNumber, filter.PageSize);
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

            // TODO: Gửi email thông báo cho Shop là tiền đã về tài khoản ngân hàng hoặc notification idk
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
                // Hoàn lại tiền gốc + phí rút (Tính lại phí dựa trên Amount vì bảng WithdrawalRequest không lưu FeeAmount)
                decimal feePercent = _walletSettings.WithdrawalFeePercent;
                decimal feeAmount = withdrawalReq.Amount * feePercent;
                decimal refundAmount = withdrawalReq.Amount + feeAmount;

                await _unitOfWork.Wallets.UpdateBalancesAsync(wallet.Id, refundAmount, 0);

                // Ghi log Transaction hoàn tiền vào sổ cái ví
                var transaction = new Transaction
                {
                    WalletId = wallet.Id,
                    Amount = refundAmount,
                    Type = TransactionType.ManualAdjustment,
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

            // TODO: Gửi email thông báo cho Shop lý do bị từ chối hoặc notification idk
        }

        public async Task AdjustBalanceAsync(Guid adminId, AdjustBalanceRequest request)
        {
            var wallet = await _unitOfWork.Wallets.GetByUserIdAsync(request.UserId);
            if (wallet == null)
            {
                await CreateWalletAsync(request.UserId);
                wallet = await _unitOfWork.Wallets.GetByUserIdAsync(request.UserId);
            }

            await _unitOfWork.Wallets.UpdateBalancesAsync(wallet.Id, request.Amount, 0, true);

            var transaction = new Transaction
            {
                WalletId = wallet.Id,
                Amount = request.Amount,
                Type = TransactionType.ManualAdjustment,
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

        public async Task<string> CreateDepositTransactionAsync(Guid userId, DepositRequest request)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null) throw new KeyNotFoundException("User not found");

            // TODO: Thay URL này bằng URL thật của Frontend
            var successUrl = _frontendUrls.DepositSuccessPath;
            var cancelUrl = _frontendUrls.DepositCancelPath;

            // Gọi PaymentService
            var stripeResult = await _paymentService.CreateDepositSessionAsync(
                request.Amount,
                user.Email,
                userId.ToString(),
                successUrl,
                cancelUrl
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

            // Trả về URL thanh toán
            return stripeResult.PaymentUrl;
        }


        public async Task<WalletStatisticsResponse> GetWalletStatisticsAsync(Guid userId)
        {
            var wallet = await _unitOfWork.Wallets.GetByUserIdAsync(userId);
            if (wallet == null) return new WalletStatisticsResponse();

            var transactions = await _unitOfWork.Transactions.GetByWalletIdAsync(wallet.Id);
            var withdrawals = await _unitOfWork.WithdrawalRequests.GetByUserIdAsync(userId);
            var now = DateTime.UtcNow;

            return new WalletStatisticsResponse
            {
                AvailableBalance = wallet.Balance,
                HeldBalance = wallet.HeldBalance,
                // Doanh thu = Tổng các giao dịch SalesRevenue
                TotalRevenue = transactions.Where(t => t.Type == TransactionType.SalesRevenue).Sum(t => t.Amount),
                ThisMonthRevenue = transactions.Where(t => t.Type == TransactionType.SalesRevenue && t.CreatedAt.Month == now.Month && t.CreatedAt.Year == now.Year).Sum(t => t.Amount),

                // Rút tiền lấy từ bảng WithdrawalRequests
                TotalWithdrawn = withdrawals.Where(w => w.Status == WithdrawalStatus.Completed).Sum(w => w.Amount),
                PendingWithdrawal = withdrawals.Where(w => w.Status == WithdrawalStatus.Pending).Sum(w => w.Amount)
            };
        }

        public async Task<List<HeldTransactionResponse>> GetHeldTransactionsAsync(Guid userId)
        {
            var wallet = await _unitOfWork.Wallets.GetByUserIdAsync(userId);
            if (wallet == null) return new List<HeldTransactionResponse>();

            var transactions = await _unitOfWork.Transactions.GetByWalletIdAsync(wallet.Id);

            // Lọc ra các giao dịch đang treo (SalesPending)
            var heldTransactions = transactions.Where(t => t.Type == TransactionType.SalesPending).ToList();

            return _mapper.Map<List<HeldTransactionResponse>>(heldTransactions);
        }
        public async Task PayOrderGroupWithWalletAsync(Guid userId, Guid orderGroupId, decimal amount)
        {
            var wallet = await _unitOfWork.Wallets.GetByUserIdAsync(userId);
            if (wallet == null) throw new InvalidOperationException("Wallet does not exist.");

            if (wallet.Balance < amount)
                throw new InvalidOperationException("Your wallet balance is insufficient to complete this checkout.");

            // Trừ tiền trong ví
            bool success = await _unitOfWork.Wallets.UpdateBalancesAsync(wallet.Id, -amount, 0);
            if (!success) throw new InvalidOperationException("Transaction failed due to concurrent update. Please try again.");

            // Ghi nhận vào Sổ cái (Transaction) bằng OrderGroupId
            var transaction = new Transaction
            {
                WalletId = wallet.Id,
                OrderGroupId = orderGroupId,
                Amount = amount,
                Type = TransactionType.OrderPayment,
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

        private string CreatePasswordHash(string password)
        {
            using var hmac = new HMACSHA512();
            var salt = Convert.ToBase64String(hmac.Key);
            var hash = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(password)));
            return $"{salt}:{hash}";
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
    }
}