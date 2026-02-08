using AutoMapper;
using FPTU.Capstone.AMKCollective.Application.DTOs.Wallet;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Services
{
    public class WalletService : IWalletService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public WalletService (IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<WalletResponse?> GetWalletByUserIdAsync(Guid userId)
        {
            var wallet = await _unitOfWork.Wallets.GetByUserIdAsync(userId);
            if (wallet == null) return null;

            return _mapper.Map<WalletResponse>(wallet);
        }

        public async Task<List<WalletTransactionResponse>> GetTransactionsAsync(Guid userId)
        {
            var payments = await _unitOfWork.Payments.GetByUserIdAsync(userId);
            return _mapper.Map<List<WalletTransactionResponse>>(payments);
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
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            var wallet = await _unitOfWork.Wallets.GetByUserIdAsync(userId);

            if (user == null || wallet == null)
                throw new Exception("Invalid user or wallet data.");

            decimal fee = 0;
            decimal minBalanceRequirement = 0;

            // [FIXED] Compare Enum directly, not string
            if (user.Role.Name == RoleType.Shop)
            {
                // Rule 2: Shop only withdraws on 15th
                if (DateTime.Now.Day != 15)
                    throw new Exception("Shops can only withdraw money on the 15th of each month.");

                // Rule 3: 0.2% fee
                fee = request.Amount * 0.002m;

                // Rule 1: Min balance 1,000,000
                minBalanceRequirement = 1_000_000;
            }
            else // Customer
            {
                // Rule 4: Min withdrawal 10k, no fee
                if (request.Amount < 10_000)
                    throw new Exception("Minimum withdrawal amount is 10,000 VND.");

                fee = 0;
                minBalanceRequirement = 0;
            }

            decimal totalDeduction = request.Amount + fee;
            if (wallet.Balance < totalDeduction + minBalanceRequirement)
            {
                throw new Exception($"Insufficient balance. You must maintain at least {minBalanceRequirement:N0} VND and pay a fee of {fee:N0} VND.");
            }

            // Deduct balance
            wallet.Balance -= totalDeduction;
            _unitOfWork.Wallets.Update(wallet);

            // Create Payment Log
            var transaction = _mapper.Map<Payment>(request);
            transaction.UserId = userId;
            transaction.FeeAmount = fee;
            transaction.Type = PaymentType.Withdrawal;
            transaction.Status = PaymentStatus.Pending;

            // [FIXED] Use proposed enum value
            transaction.Method = PaymentMethod.BankTransfer;

            transaction.Description = $"Withdrawal to {request.BankName} - Account: {request.BankAccountNumber}. Fee: {fee:N0}";
            transaction.Currency = "VND";

            await _unitOfWork.Payments.AddAsync(transaction);
            await _unitOfWork.CommitAsync();
        }

        public async Task PayOrderWithWalletAsync(Guid userId, Guid orderId, decimal amount)
        {
            var wallet = await _unitOfWork.Wallets.GetByUserIdAsync(userId);
            if (wallet == null) throw new Exception("Wallet does not exist.");

            if (wallet.Balance < amount)
                throw new Exception("Wallet balance is insufficient for payment.");

            wallet.Balance -= amount;
            _unitOfWork.Wallets.Update(wallet);

            var transaction = new Payment
            {
                UserId = userId,
                RelatedOrderId = orderId,
                Amount = amount,
                Type = PaymentType.PaymentByWallet,
                Status = PaymentStatus.Paid,

                // [FIXED] Use proposed enum value
                Method = PaymentMethod.Wallet,

                Description = $"Payment for order #{orderId}",
                Currency = "VND"
            };

            await _unitOfWork.Payments.AddAsync(transaction);
            await _unitOfWork.CommitAsync();
        }

        public async Task AddPendingSalesToWalletAsync(Guid shopId, Guid orderId, decimal amount)
        {
            var wallet = await _unitOfWork.Wallets.GetByUserIdAsync(shopId);
            if (wallet == null) return;

            wallet.HeldBalance += amount;
            _unitOfWork.Wallets.Update(wallet);

            var log = new Payment
            {
                UserId = shopId,
                RelatedOrderId = orderId,
                Amount = amount,
                Type = PaymentType.SalesPending,
                Status = PaymentStatus.Paid,
                Description = $"Pending sales revenue from order #{orderId}",
                Currency = "VND"
            };

            await _unitOfWork.Payments.AddAsync(log);
            await _unitOfWork.CommitAsync();
        }

        public async Task ReleaseHeldMoneyAsync(Guid shopId, Guid orderId, decimal amount)
        {
            var wallet = await _unitOfWork.Wallets.GetByUserIdAsync(shopId);
            if (wallet == null) return;

            if (wallet.HeldBalance >= amount)
            {
                wallet.HeldBalance -= amount;
                wallet.Balance += amount;
                _unitOfWork.Wallets.Update(wallet);

                var log = new Payment
                {
                    UserId = shopId,
                    RelatedOrderId = orderId,
                    Amount = amount,
                    Type = PaymentType.SalesReleased,
                    Status = PaymentStatus.Paid,
                    Description = $"Released revenue for order #{orderId}",
                    Currency = "VND"
                };

                await _unitOfWork.Payments.AddAsync(log);
                await _unitOfWork.CommitAsync();
            }
        }

        public async Task RefundToWalletAsync(Guid userId, decimal amount, string reason)
        {
            var wallet = await _unitOfWork.Wallets.GetByUserIdAsync(userId);
            if (wallet == null) return;

            wallet.Balance += amount;
            _unitOfWork.Wallets.Update(wallet);

            var log = new Payment
            {
                UserId = userId,
                Amount = amount,
                Type = PaymentType.RefundToWallet,
                Status = PaymentStatus.Paid,
                Description = reason,
                Currency = "VND"
            };

            await _unitOfWork.Payments.AddAsync(log);
            await _unitOfWork.CommitAsync();
        }

        public async Task DeductFundsForRefundAsync(Guid shopId, Guid orderId, decimal amount, bool isOrderCompleted)
        {
            var wallet = await _unitOfWork.Wallets.GetByUserIdAsync(shopId);
            if (wallet == null) return; 

            if (isOrderCompleted)
            {
                // Nếu đơn đã hoàn thành -> Tiền đã về Balance -> Trừ Balance
                // Cho phép âm nếu shop rút hết tiền rồi (shop nợ sàn)
                wallet.Balance -= amount;
            }
            else
            {
                // Nếu đơn chưa hoàn thành -> Tiền còn treo ở Held -> Trừ Held
                wallet.HeldBalance -= amount;

                // Safety check: Không để HeldBalance âm (nếu logic sai đâu đó)
                // if (wallet.HeldBalance < 0) wallet.HeldBalance = 0; 
            }

            _unitOfWork.Wallets.Update(wallet);

            // Ghi log giao dịch
            var log = new Payment
            {
                Id = Guid.NewGuid(),
                UserId = shopId,
                RelatedOrderId = orderId,
                Amount = -amount, // Số âm thể hiện bị trừ
                Type = PaymentType.RefundToWallet, 
                Status = PaymentStatus.Paid,
                Method = PaymentMethod.Wallet,
                Description = $"Refund deduction for Order #{orderId}",
                Currency = "VND",
                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.Payments.AddAsync(log);
            await _unitOfWork.CommitAsync();
        }
    }
}