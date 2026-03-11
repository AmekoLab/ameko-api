using FPTU.Capstone.AMKCollective.Application.DTOs.Withdrawal;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Domain.Enums;
using FPTU.Capstone.AMKCollective.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Services
{
    public class WithdrawalService : IWithdrawalService
    {
        private readonly IUnitOfWork _unitOfWork;

        public WithdrawalService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<WithdrawalRequestResponseDto> CreateWithdrawalAsync(Guid userId, CreateWithdrawalRequestDto dto)
        {
            // Step 1: Business Validation - Prevent queueing multiple pending requests
            var pendingRequests = await _unitOfWork.WithdrawalRequests.GetPendingAsync();
            if (pendingRequests.Any(r => r.UserId == userId))
            {
                throw new InvalidOperationException("You already have an active withdrawal request pending approval.");
            }

            try
            {
                WithdrawalRequest? request = null;

                await _unitOfWork.ExecuteTransactionAsync(async () =>
                {
                    // Step 3: Load current state
                    var wallet = await _unitOfWork.Wallets.GetByUserIdAsync(userId);
                    if (wallet == null) throw new InvalidOperationException("Wallet not found.");

                    // Step 4: Validate Balance
                    if (wallet.Balance < dto.Amount)
                        throw new InvalidOperationException("Insufficient wallet balance.");

                    // Step 5: Deduct from Balance, Move to HoldBalance
                    wallet.Balance -= dto.Amount;
                    wallet.HeldBalance += dto.Amount;

                    _unitOfWork.Wallets.Update(wallet);

                    // Step 6: Create the Request
                    request = new WithdrawalRequest
                    {
                        UserId = userId,
                        Amount = dto.Amount,
                        BankName = dto.BankName,
                        BankAccountNumber = dto.BankAccountNumber,
                        BankAccountName = dto.BankAccountName,
                        Status = WithdrawalStatus.Pending,
                        RequestedAt = DateTime.UtcNow
                    };

                    await _unitOfWork.WithdrawalRequests.AddAsync(request);

                    // Step 7: Commit to DB. OCC catches parallel execution modifying the same Wallet.
                    await _unitOfWork.CommitAsync(); 
                });

                // Step 8: Return DTO outside of transaction boundary
                return MapToResponse(request!);
            }
            catch (Exception ex) when (ex.GetType().Name == "DbUpdateConcurrencyException")
            {
                // Step 9: Handle the Double-Entry Race Condition Gracefully
                throw new InvalidOperationException("Transaction failed: A concurrent update modified your wallet balance. Please try again.");
            }
        }

        public async Task ApproveWithdrawalAsync(Guid adminId, Guid withdrawalId, ApproveWithdrawalRequestDto dto)
        {
            try
            {
                await _unitOfWork.ExecuteTransactionAsync(async () =>
                {
                    var request = await _unitOfWork.WithdrawalRequests.GetByIdAsync(withdrawalId);
                    if (request == null)
                        throw new KeyNotFoundException("Withdrawal request not found.");

                    if (request.Status != WithdrawalStatus.Pending)
                        throw new InvalidOperationException("Only pending requests can be approved.");

                    var wallet = await _unitOfWork.Wallets.GetByUserIdAsync(request.UserId);
                    if (wallet == null)
                        throw new InvalidOperationException("User wallet not found.");

                    // Update Status and Admin data
                    request.Status = WithdrawalStatus.Completed;
                    request.AdminId = adminId;
                    request.AdminMessage = dto.AdminMessage;
                    request.EvidenceUrl = dto.EvidenceUrl;
                    request.ProcessedAt = DateTime.UtcNow;

                    _unitOfWork.WithdrawalRequests.Update(request);

                    // Update Wallet (permanently deduct from HoldBalance)
                    wallet.HeldBalance -= request.Amount;
                    if (wallet.HeldBalance < 0) wallet.HeldBalance = 0;

                    _unitOfWork.Wallets.Update(wallet);

                    await _unitOfWork.CommitAsync();
                });
            }
            catch (Exception ex) when (ex.GetType().Name == "DbUpdateConcurrencyException")
            {
                throw new InvalidOperationException("Transaction failed: A concurrent update modified the wallet balance. Please try again.");
            }
        }

        public async Task RejectWithdrawalAsync(Guid adminId, Guid withdrawalId, RejectWithdrawalRequestDto dto)
        {
            try
            {
                await _unitOfWork.ExecuteTransactionAsync(async () =>
                {
                    var request = await _unitOfWork.WithdrawalRequests.GetByIdAsync(withdrawalId);
                    if (request == null)
                        throw new KeyNotFoundException("Withdrawal request not found.");

                    if (request.Status != WithdrawalStatus.Pending)
                        throw new InvalidOperationException("Only pending requests can be rejected.");

                    var wallet = await _unitOfWork.Wallets.GetByUserIdAsync(request.UserId);
                    if (wallet == null)
                        throw new InvalidOperationException("User wallet not found.");

                    // Update Status
                    request.Status = WithdrawalStatus.Rejected;
                    request.AdminId = adminId;
                    request.AdminMessage = dto.AdminMessage;
                    request.ProcessedAt = DateTime.UtcNow;

                    _unitOfWork.WithdrawalRequests.Update(request);

                    // Release funds back to user
                    wallet.HeldBalance -= request.Amount;
                    if (wallet.HeldBalance < 0) wallet.HeldBalance = 0;
                    
                    wallet.Balance += request.Amount;

                    _unitOfWork.Wallets.Update(wallet);

                    await _unitOfWork.CommitAsync();
                });
            }
            catch (Exception ex) when (ex.GetType().Name == "DbUpdateConcurrencyException")
            {
                throw new InvalidOperationException("Transaction failed: A concurrent update modified the wallet balance. Please try again.");
            }
        }

        public async Task<List<WithdrawalRequestResponseDto>> GetUserWithdrawalsAsync(Guid userId)
        {
            var requests = await _unitOfWork.WithdrawalRequests.GetByUserIdAsync(userId);
            return requests.Select(MapToResponse).ToList();
        }

        public async Task<List<WithdrawalRequestResponseDto>> GetPendingWithdrawalsAsync()
        {
            var requests = await _unitOfWork.WithdrawalRequests.GetPendingAsync();
            return requests.Select(MapToResponse).ToList();
        }

        // Helper Map Method
        private WithdrawalRequestResponseDto MapToResponse(WithdrawalRequest request)
        {
            return new WithdrawalRequestResponseDto
            {
                Id = request.Id,
                UserId = request.UserId,
                Username = request.User?.Username,
                ShopName = request.User?.ShopProfile?.ShopName,
                Amount = request.Amount,
                BankName = request.BankName,
                BankAccountNumber = request.BankAccountNumber,
                BankAccountName = request.BankAccountName,
                Status = request.Status.ToString(),
                AdminId = request.AdminId,
                AdminMessage = request.AdminMessage,
                EvidenceUrl = request.EvidenceUrl,
                RequestedAt = request.RequestedAt,
                ProcessedAt = request.ProcessedAt
            };
        }
    }
}
