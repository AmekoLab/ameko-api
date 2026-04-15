using AutoMapper;
using FPTU.Capstone.AMKCollective.Application.Helpers;
using FPTU.Capstone.AMKCollective.Application.DTOs.Settings;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Domain.Enums;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Services
{
    public class QualityScoreService : IQualityScoreService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly QualityScoreSettings _settings;
        private readonly IMapper _mapper;

        // Tiêm cấu hình IOptions vào đây
        public QualityScoreService(IUnitOfWork unitOfWork, IOptions<QualityScoreSettings> options, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _settings = options.Value;
            _mapper = mapper;
        }

        public async Task<QualityScoreResultDto> CalculateShopScoreAsync(Guid shopId, DateTime startDate, DateTime endDate)
        {
            var rawMetrics = await _unitOfWork.ShopAnalytics.GetMetricsAsync(shopId, startDate, endDate);
            var result = new QualityScoreResultDto { RawMetrics = rawMetrics };

            // KỊCH BẢN COLD START: Shop mới hoặc không có đủ đơn hàng
            if (rawMetrics.TotalOrders < _settings.MinimumOrdersRequired)
            {
                result.TotalScore = _settings.DefaultScore;
                result.Badge = ShopBadge.Basic;
                return result;
            }

            // 1. Tỷ lệ lỗi - Nghịch đảo
            double issueScore = 100 - (rawMetrics.IssueRate / _settings.MaxIssueRateThreshold * 100);
            result.IssueScore = Math.Clamp(issueScore, 0, 100);

            // 2. Tốc độ phản hồi - Nghịch đảo
            double responseScore = 100;
            if (rawMetrics.AvgResponseHours > _settings.IdealResponseHours)
            {
                double penaltyRange = _settings.MaxResponseHoursThreshold - _settings.IdealResponseHours;
                double excessHours = rawMetrics.AvgResponseHours - _settings.IdealResponseHours;

                responseScore = 100 - (excessHours / penaltyRange * 100);
            }
            result.ResponseScore = Math.Clamp(responseScore, 0, 100);

            // 3. Khối lượng hoàn tiền - Nghịch đảo
            double refundScore = 100 - (rawMetrics.RefundRate / _settings.MaxRefundRateThreshold * 100);
            result.RefundScore = Math.Clamp(refundScore, 0, 100);

            // 4. Tỷ lệ mua lại - Tỷ lệ thuận
            double repurchaseScore = (rawMetrics.RepurchaseRate / _settings.IdealRepurchaseRateThreshold) * 100;
            result.RepurchaseScore = Math.Clamp(repurchaseScore, 0, 100);

            // 5. Tỷ lệ Feedback tích cực - Tỷ lệ thuận
            result.FeedbackScore = Math.Clamp(rawMetrics.PositiveFeedbackRate, 0, 100);

            // --- TỔNG HỢP VÀ NHÂN TRỌNG SỐ ---
            double totalWeightedScore =
                (result.IssueScore * _settings.Weights.Issue) +
                (result.ResponseScore * _settings.Weights.Response) +
                (result.RefundScore * _settings.Weights.Refund) +
                (result.RepurchaseScore * _settings.Weights.Repurchase) +
                (result.FeedbackScore * _settings.Weights.Feedback);

            result.TotalScore = (int)Math.Round(totalWeightedScore);

            // --- PHÂN HẠNG BADGE ---
            if (result.TotalScore >= _settings.BadgeThresholds.Premium)
            {
                result.Badge = ShopBadge.Premium;
            }
            else if (result.TotalScore >= _settings.BadgeThresholds.Verified)
            {
                result.Badge = ShopBadge.Verified;
            }
            else
            {
                result.Badge = ShopBadge.Basic;
            }

            return result;
        }
        public async Task<CurrentReputationDto?> GetCurrentReputationAsync(Guid shopId)
        {
            var shop = await _unitOfWork.Shops.GetByIdAsync(shopId);
            if (shop == null) return null;
            return _mapper.Map<CurrentReputationDto>(shop);
        }

        public async Task<QualityScoreSnapshotDto?> GetReputationBreakdownAsync(Guid shopId)
        {
            var snapshot = await _unitOfWork.QualityScoreSnapshots.GetLatestSnapshotAsync(shopId);
            if (snapshot == null) return null;

            return _mapper.Map<QualityScoreSnapshotDto>(snapshot).ConvertDatesToLocal();
        }

        public async Task<IEnumerable<ReputationTrendDto>> GetReputationTrendAsync(Guid shopId)
        {
            var fromDate = DateTime.UtcNow.AddDays(-_settings.TimeWindowDays);
            var history = await _unitOfWork.QualityScoreSnapshots.GetHistoryAsync(shopId, fromDate);

            return _mapper.Map<IEnumerable<ReputationTrendDto>>(history);
        }

        public async Task<IEnumerable<BadgeHistoryDto>> GetBadgeHistoryAsync(Guid shopId)
        {
            var fromDate = DateTime.UtcNow.AddDays(-365);
            var history = await _unitOfWork.QualityScoreSnapshots.GetHistoryAsync(shopId, fromDate);

            if (!history.Any()) return new List<BadgeHistoryDto>();

            var badgeChanges = history.OrderBy(h => h.CapturedAt).ToList();

            // Logic tính toán thay đổi Badge vẫn giữ nguyên, trả về BadgeHistoryDto
            var timeline = badgeChanges
                .Where((current, index) => index == 0 || current.Badge != badgeChanges[index - 1].Badge)
                .Select(h => new BadgeHistoryDto
                {
                    Date = h.CapturedAt.ToString("yyyy-MM-dd"),
                    NewBadge = h.Badge.ToString(),
                    Reason = $"Badge updated to {h.Badge.ToString()} based on quality score of {h.TotalScore}."
                })
                .ToList();

            return timeline;
        }
    }
}