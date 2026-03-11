using FPTU.Capstone.AMKCollective.Application.DTOs.Withdrawal;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Services
{
    public interface IWithdrawalService
    {
        Task<WithdrawalRequestResponseDto> CreateWithdrawalAsync(Guid userId, CreateWithdrawalRequestDto dto);
        Task ApproveWithdrawalAsync(Guid adminId, Guid withdrawalId, ApproveWithdrawalRequestDto dto);
        Task RejectWithdrawalAsync(Guid adminId, Guid withdrawalId, RejectWithdrawalRequestDto dto);
        Task<List<WithdrawalRequestResponseDto>> GetUserWithdrawalsAsync(Guid userId);
        Task<List<WithdrawalRequestResponseDto>> GetPendingWithdrawalsAsync();
    }
}
