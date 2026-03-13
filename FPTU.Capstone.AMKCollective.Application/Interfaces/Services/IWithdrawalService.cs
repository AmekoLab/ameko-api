using FPTU.Capstone.AMKCollective.Application.DTOs.Withdrawal;
using FPTU.Capstone.AMKCollective.Application.DTOs.Common;
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
        Task<WithdrawalRequestResponseDto> GetWithdrawalByIdAsync(Guid id);
        Task<PaginatedResult<WithdrawalRequestResponseDto>> GetUserWithdrawalsAsync(Guid userId, int pageIndex, int pageSize);
        Task<PaginatedResult<WithdrawalRequestResponseDto>> GetPendingWithdrawalsAsync(int pageIndex, int pageSize);
        Task<PaginatedResult<WithdrawalRequestResponseDto>> GetProcessedWithdrawalsAsync(int pageIndex, int pageSize);
    }
}
