using FPTU.Capstone.AMKCollective.Application.DTOs.Common;
using FPTU.Capstone.AMKCollective.Application.DTOs.Reputation;
using FPTU.Capstone.AMKCollective.Application.DTOs.Settings;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Services
{
    public class ReputationService : IReputationService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ReputationSettings _settings;

        public ReputationService(IUnitOfWork unitOfWork, IOptions<ReputationSettings> options)
        {
            _unitOfWork = unitOfWork;
            _settings = options.Value;
        }

        public async Task<int> AdjustCustomerScoreAsync(Guid userId, int delta, string? reason = null)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null) throw new KeyNotFoundException("User not found");

            var newScore = ClampScore(user.CurrentReputationScore + delta);
            user.CurrentReputationScore = newScore;

            await _unitOfWork.Users.UpdateAsync(user);
            await AddReputationLogAsync(ReputationTargetType.Customer, userId, delta, newScore, reason);
            await _unitOfWork.CommitAsync();

            return newScore;
        }

        public Task<int> AdjustReputationAsync(ReputationTargetType targetType, Guid targetId, int delta, string? reason = null)
        {
            return targetType switch
            {
                ReputationTargetType.Shop => AdjustShopScoreAsync(targetId, delta, reason),
                _ => AdjustCustomerScoreAsync(targetId, delta, reason)
            };
        }
        public async Task<int> AdjustShopScoreAsync(Guid shopId, int delta, string? reason = null)
        {
            var shop = await _unitOfWork.Shops.GetByIdAsync(shopId);
            if (shop == null) throw new KeyNotFoundException("Shop not found");

            var newScore = ClampScore(shop.CurrentQualityScore + delta);
            shop.CurrentQualityScore = newScore;

            await _unitOfWork.Shops.UpdateAsync(shop);
            await AddReputationLogAsync(ReputationTargetType.Shop, shopId, delta, newScore, reason);
            await _unitOfWork.CommitAsync();

            return newScore;
        }

        public async Task<UserReputationSummaryDto> GetUserReputationAsync(Guid userId)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null) throw new KeyNotFoundException("User not found");

            return new UserReputationSummaryDto
            {
                UserId = user.Id,
                CurrentScore = user.CurrentReputationScore,
                MonthlyAutoCancels = user.YMonthlyAutoCancels,
                TotalAutoCancels = user.TotalAutoCancels,
                SlowResponseViolationCount = user.SlowResponseViolationCount,
                ConsecutiveSuccesses = user.ConsecutiveSuccesses,
                Gate = GetCustomerGate(user.CurrentReputationScore)
            };
        }

        public async Task<PaginatedResult<ReputationLogDto>> GetUserReputationLogsAsync(Guid userId, int pageNumber, int pageSize)
        {
            var (items, totalCount) = await _unitOfWork.ReputationLogs.GetByTargetAsync(
                ReputationTargetType.Customer.ToString(), userId, pageNumber, pageSize);

            var mapped = items.Select(MapLog).ToList();
            return new PaginatedResult<ReputationLogDto>(mapped, totalCount, pageNumber, pageSize);
        }

        public async Task<PaginatedResult<ReputationLogDto>> GetShopReputationLogsAsync(Guid shopId, int pageNumber, int pageSize)
        {
            var (items, totalCount) = await _unitOfWork.ReputationLogs.GetByTargetAsync(
                ReputationTargetType.Shop.ToString(), shopId, pageNumber, pageSize);

            var mapped = items.Select(MapLog).ToList();
            return new PaginatedResult<ReputationLogDto>(mapped, totalCount, pageNumber, pageSize);
        }

        private async Task AddReputationLogAsync(ReputationTargetType targetType, Guid targetId, int delta, int scoreAfter, string? reason)
        {
            var log = new ReputationLog
            {
                TargetType = targetType.ToString(),
                TargetId = targetId,
                Delta = delta,
                ScoreAfter = scoreAfter,
                Reason = reason
            };

            await _unitOfWork.ReputationLogs.AddAsync(log);
        }

        private static ReputationLogDto MapLog(ReputationLog log)
        {
            return new ReputationLogDto
            {
                Id = log.Id,
                TargetType = log.TargetType,
                TargetId = log.TargetId,
                Delta = log.Delta,
                ScoreAfter = log.ScoreAfter,
                Reason = log.Reason,
                ReferenceType = log.ReferenceType,
                ReferenceId = log.ReferenceId,
                CreatedAt = log.CreatedAt
            };
        }

        public CustomerReputationGate GetCustomerGate(int score)
        {
            var result = new CustomerReputationGate { Score = score };

            if (score >= _settings.CustomerHighMinScore)
            {
                result.Tier = "High";
                result.IsLocked = false;
                result.MonthlyOrderLimit = _settings.CustomerHighMonthlyOrderLimit;
                result.CanUseAdminVoucher = _settings.CustomerHighCanUseAdminVoucher;
                return result;
            }

            if (score >= _settings.CustomerMidMinScore)
            {
                result.Tier = "Mid";
                result.IsLocked = false;
                result.MonthlyOrderLimit = _settings.CustomerMidMonthlyOrderLimit;
                result.CanUseAdminVoucher = _settings.CustomerMidCanUseAdminVoucher;
                return result;
            }

            if (score >= _settings.CustomerLowMinScore)
            {
                result.Tier = "Low";
                result.IsLocked = false;
                result.MonthlyOrderLimit = _settings.CustomerLowMonthlyOrderLimit;
                result.CanUseAdminVoucher = _settings.CustomerLowCanUseAdminVoucher;
                return result;
            }

            result.Tier = "Locked";
            result.IsLocked = true;
            result.MonthlyOrderLimit = 0;
            result.CanUseAdminVoucher = false;
            return result;
        }

        public ShopReputationGate GetShopGate(int score)
        {
            var result = new ShopReputationGate { Score = score };

            if (score >= _settings.ShopHighMinScore)
            {
                result.Tier = "High";
                result.IsServiceSuspended = false;
                result.MonthlyOrderLimit = _settings.ShopHighMonthlyOrderLimit;
                result.CanCreateVoucher = _settings.ShopHighCanCreateVoucher;
                return result;
            }

            if (score >= _settings.ShopMidMinScore)
            {
                result.Tier = "Mid";
                result.IsServiceSuspended = false;
                result.MonthlyOrderLimit = _settings.ShopMidMonthlyOrderLimit;
                result.CanCreateVoucher = _settings.ShopMidCanCreateVoucher;
                return result;
            }

            if (score >= _settings.ShopLowMinScore)
            {
                result.Tier = "Low";
                result.IsServiceSuspended = false;
                result.MonthlyOrderLimit = _settings.ShopLowMonthlyOrderLimit;
                result.CanCreateVoucher = _settings.ShopLowCanCreateVoucher;
                return result;
            }

            result.Tier = "Suspended";
            result.IsServiceSuspended = true;
            result.MonthlyOrderLimit = 0;
            result.CanCreateVoucher = false;
            return result;
        }

        private int ClampScore(int score)
        {
            if (score < _settings.MinScore) return _settings.MinScore;
            if (score > _settings.MaxScore) return _settings.MaxScore;
            return score;
        }
    }
}
