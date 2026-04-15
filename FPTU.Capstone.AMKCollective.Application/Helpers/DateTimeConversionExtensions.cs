using FPTU.Capstone.AMKCollective.Application.DTOs.AssembledProduct;
using FPTU.Capstone.AMKCollective.Application.DTOs.AssembledProductFeedback;
using FPTU.Capstone.AMKCollective.Application.DTOs.AssemblyTracking;
using FPTU.Capstone.AMKCollective.Application.DTOs.Builder;
using FPTU.Capstone.AMKCollective.Application.DTOs.Chat;
using FPTU.Capstone.AMKCollective.Application.DTOs.Commission;
using FPTU.Capstone.AMKCollective.Application.DTOs.Community;
using FPTU.Capstone.AMKCollective.Application.DTOs.Feedback;
using FPTU.Capstone.AMKCollective.Application.DTOs.Order;
using FPTU.Capstone.AMKCollective.Application.DTOs.OrderIssues;
using FPTU.Capstone.AMKCollective.Application.DTOs.Payment;
using FPTU.Capstone.AMKCollective.Application.DTOs.Reputation;
using FPTU.Capstone.AMKCollective.Application.DTOs.Shop;
using FPTU.Capstone.AMKCollective.Application.DTOs.User;
using FPTU.Capstone.AMKCollective.Application.DTOs.Voucher;
using FPTU.Capstone.AMKCollective.Application.DTOs.Wallet;
using FPTU.Capstone.AMKCollective.Application.DTOs.Withdrawal;
using System.Collections.Generic;
using System.Linq;

namespace FPTU.Capstone.AMKCollective.Application.Helpers
{
    /// <summary>
    /// Extension methods để convert datetime UTC -> local time trên các Response DTOs.
    /// Được gọi ở Service layer ngay sau khi AutoMapper map entity -> DTO.
    /// </summary>
    public static class DateTimeConversionExtensions
    {
        // ===================== USER =====================
        public static UserResponse ConvertDatesToLocal(this UserResponse dto)
        {
            dto.CreatedAt = dto.CreatedAt.ConvertToLocalTime();
            return dto;
        }

        public static IEnumerable<UserResponse> ConvertDatesToLocal(this IEnumerable<UserResponse> dtos)
        {
            foreach (var dto in dtos) dto.ConvertDatesToLocal();
            return dtos;
        }

        // ===================== SHOP =====================
        public static ShopResponse ConvertDatesToLocal(this ShopResponse dto)
        {
            dto.CreatedAt = dto.CreatedAt.ConvertToLocalTime();
            return dto;
        }

        public static IEnumerable<ShopResponse> ConvertDatesToLocal(this IEnumerable<ShopResponse> dtos)
        {
            foreach (var dto in dtos) dto.ConvertDatesToLocal();
            return dtos;
        }

        public static ShopDetailResponse ConvertDatesToLocal(this ShopDetailResponse dto)
        {
            dto.LastResubmitTime = dto.LastResubmitTime?.ConvertToLocalTime();
            return dto;
        }

        public static IEnumerable<ShopDetailResponse> ConvertDatesToLocal(this IEnumerable<ShopDetailResponse> dtos)
        {
            foreach (var dto in dtos) dto.ConvertDatesToLocal();
            return dtos;
        }

        public static QualityScoreSnapshotDto ConvertDatesToLocal(this QualityScoreSnapshotDto dto)
        {
            dto.CapturedAt = dto.CapturedAt.ConvertToLocalTime();
            return dto;
        }

        // ===================== ORDER =====================
        public static OrderGroupResponse ConvertDatesToLocal(this OrderGroupResponse dto)
        {
            dto.CreatedAt = dto.CreatedAt.ConvertToLocalTime();
            return dto;
        }

        public static List<OrderGroupResponse> ConvertDatesToLocal(this List<OrderGroupResponse> dtos)
        {
            foreach (var dto in dtos) dto.ConvertDatesToLocal();
            return dtos;
        }

        public static OrderResponse ConvertDatesToLocal(this OrderResponse dto)
        {
            dto.CreatedAt = dto.CreatedAt.ConvertToLocalTime();
            dto.ExpectedDeliveryDate = dto.ExpectedDeliveryDate?.ConvertToLocalTime();
            return dto;
        }

        public static List<OrderResponse> ConvertDatesToLocal(this List<OrderResponse> dtos)
        {
            foreach (var dto in dtos) dto.ConvertDatesToLocal();
            return dtos;
        }

        // ===================== ORDER ISSUES =====================
        public static OrderIssueResponse ConvertDatesToLocal(this OrderIssueResponse dto)
        {
            dto.CreatedAt = dto.CreatedAt.ConvertToLocalTime();
            return dto;
        }

        public static IEnumerable<OrderIssueResponse> ConvertDatesToLocal(this IEnumerable<OrderIssueResponse> dtos)
        {
            foreach (var dto in dtos) dto.ConvertDatesToLocal();
            return dtos;
        }

        public static OrderIssueLogResponse ConvertDatesToLocal(this OrderIssueLogResponse dto)
        {
            dto.CreatedAt = dto.CreatedAt.ConvertToLocalTime();
            return dto;
        }

        public static IEnumerable<OrderIssueLogResponse> ConvertDatesToLocal(this IEnumerable<OrderIssueLogResponse> dtos)
        {
            foreach (var dto in dtos) dto.ConvertDatesToLocal();
            return dtos;
        }

        public static WarrantyIssueResponse ConvertDatesToLocal(this WarrantyIssueResponse dto)
        {
            dto.CreatedAt = dto.CreatedAt.ConvertToLocalTime();
            dto.UpdatedAt = dto.UpdatedAt?.ConvertToLocalTime();
            return dto;
        }

        public static IEnumerable<WarrantyIssueResponse> ConvertDatesToLocal(this IEnumerable<WarrantyIssueResponse> dtos)
        {
            foreach (var dto in dtos) dto.ConvertDatesToLocal();
            return dtos;
        }

        // ===================== PAYMENT =====================
        public static PaymentResponse ConvertDatesToLocal(this PaymentResponse dto)
        {
            dto.CreatedAt = dto.CreatedAt.ConvertToLocalTime();
            return dto;
        }

        public static IEnumerable<PaymentResponse> ConvertDatesToLocal(this IEnumerable<PaymentResponse> dtos)
        {
            foreach (var dto in dtos) dto.ConvertDatesToLocal();
            return dtos;
        }

        // ===================== WALLET =====================
        public static WalletTransactionResponse ConvertDatesToLocal(this WalletTransactionResponse dto)
        {
            dto.CreatedAt = dto.CreatedAt.ConvertToLocalTime();
            return dto;
        }

        public static List<WalletTransactionResponse> ConvertDatesToLocal(this List<WalletTransactionResponse> dtos)
        {
            foreach (var dto in dtos) dto.ConvertDatesToLocal();
            return dtos;
        }

        public static HeldTransactionResponse ConvertDatesToLocal(this HeldTransactionResponse dto)
        {
            dto.Date = dto.Date.ConvertToLocalTime();
            return dto;
        }

        public static List<HeldTransactionResponse> ConvertDatesToLocal(this List<HeldTransactionResponse> dtos)
        {
            foreach (var dto in dtos) dto.ConvertDatesToLocal();
            return dtos;
        }

        // ===================== VOUCHER =====================
        public static VoucherResponse ConvertDatesToLocal(this VoucherResponse dto)
        {
            dto.StartDate = dto.StartDate.ConvertToLocalTime();
            dto.EndDate = dto.EndDate.ConvertToLocalTime();
            return dto;
        }

        public static List<VoucherResponse> ConvertDatesToLocal(this List<VoucherResponse> dtos)
        {
            foreach (var dto in dtos) dto.ConvertDatesToLocal();
            return dtos;
        }

        public static IEnumerable<VoucherResponse> ConvertDatesToLocal(this IEnumerable<VoucherResponse> dtos)
        {
            foreach (var dto in dtos) dto.ConvertDatesToLocal();
            return dtos;
        }

        public static VoucherUsageResponse ConvertDatesToLocal(this VoucherUsageResponse dto)
        {
            dto.AppliedAt = dto.AppliedAt.ConvertToLocalTime();
            return dto;
        }

        public static List<VoucherUsageResponse> ConvertDatesToLocal(this List<VoucherUsageResponse> dtos)
        {
            foreach (var dto in dtos) dto.ConvertDatesToLocal();
            return dtos;
        }

        // ===================== COMMISSION =====================
        public static CommissionRequestResponse ConvertDatesToLocal(this CommissionRequestResponse dto)
        {
            dto.CreatedAt = dto.CreatedAt.ConvertToLocalTime();
            dto.ShopResponseDeadlineAt = dto.ShopResponseDeadlineAt?.ConvertToLocalTime();
            dto.LastReminderAt = dto.LastReminderAt?.ConvertToLocalTime();
            if (dto.Quotes != null)
                foreach (var q in dto.Quotes) q.ConvertDatesToLocal();
            return dto;
        }

        public static IEnumerable<CommissionRequestResponse> ConvertDatesToLocal(this IEnumerable<CommissionRequestResponse> dtos)
        {
            foreach (var dto in dtos) dto.ConvertDatesToLocal();
            return dtos;
        }

        public static CommissionQuoteResponse ConvertDatesToLocal(this CommissionQuoteResponse dto)
        {
            dto.CreatedAt = dto.CreatedAt.ConvertToLocalTime();
            dto.ExpiredAt = dto.ExpiredAt.ConvertToLocalTime();
            dto.CustomerDecisionDeadlineAt = dto.CustomerDecisionDeadlineAt?.ConvertToLocalTime();
            return dto;
        }

        public static IEnumerable<CommissionQuoteResponse> ConvertDatesToLocal(this IEnumerable<CommissionQuoteResponse> dtos)
        {
            foreach (var dto in dtos) dto.ConvertDatesToLocal();
            return dtos;
        }

        // ===================== FEEDBACK =====================
        public static FeedbackResponse ConvertDatesToLocal(this FeedbackResponse dto)
        {
            dto.CreatedDate = dto.CreatedDate.ConvertToLocalTime();
            dto.ShopRepliedAt = dto.ShopRepliedAt?.ConvertToLocalTime();
            return dto;
        }

        public static IEnumerable<FeedbackResponse> ConvertDatesToLocal(this IEnumerable<FeedbackResponse> dtos)
        {
            foreach (var dto in dtos) dto.ConvertDatesToLocal();
            return dtos;
        }

        public static AssembledProductFeedbackResponse ConvertDatesToLocal(this AssembledProductFeedbackResponse dto)
        {
            dto.CreatedDate = dto.CreatedDate.ConvertToLocalTime();
            dto.ShopRepliedAt = dto.ShopRepliedAt?.ConvertToLocalTime();
            return dto;
        }

        public static IEnumerable<AssembledProductFeedbackResponse> ConvertDatesToLocal(this IEnumerable<AssembledProductFeedbackResponse> dtos)
        {
            foreach (var dto in dtos) dto.ConvertDatesToLocal();
            return dtos;
        }

        // ===================== REPUTATION =====================
        public static ReputationLogDto ConvertDatesToLocal(this ReputationLogDto dto)
        {
            dto.CreatedAt = dto.CreatedAt.ConvertToLocalTime();
            return dto;
        }

        public static IEnumerable<ReputationLogDto> ConvertDatesToLocal(this IEnumerable<ReputationLogDto> dtos)
        {
            foreach (var dto in dtos) dto.ConvertDatesToLocal();
            return dtos;
        }

        // ===================== CHAT =====================
        public static ChatMessageResponse ConvertDatesToLocal(this ChatMessageResponse dto)
        {
            dto.CreatedAt = dto.CreatedAt.ConvertToLocalTime();
            return dto;
        }

        public static IEnumerable<ChatMessageResponse> ConvertDatesToLocal(this IEnumerable<ChatMessageResponse> dtos)
        {
            foreach (var dto in dtos) dto.ConvertDatesToLocal();
            return dtos;
        }

        public static ConversationResponse ConvertDatesToLocal(this ConversationResponse dto)
        {
            dto.LastMessageAt = dto.LastMessageAt?.ConvertToLocalTime();
            return dto;
        }

        public static IEnumerable<ConversationResponse> ConvertDatesToLocal(this IEnumerable<ConversationResponse> dtos)
        {
            foreach (var dto in dtos) dto.ConvertDatesToLocal();
            return dtos;
        }

        public static MessageReactionResponse ConvertDatesToLocal(this MessageReactionResponse dto)
        {
            dto.UpdatedAt = dto.UpdatedAt.ConvertToLocalTime();
            return dto;
        }

        // ===================== COMMUNITY =====================
        public static NotificationDto ConvertDatesToLocal(this NotificationDto dto)
        {
            dto.CreatedAt = dto.CreatedAt.ConvertToLocalTime();
            return dto;
        }

        public static PostFeedResponse ConvertDatesToLocal(this PostFeedResponse dto)
        {
            dto.CreatedAt = dto.CreatedAt.ConvertToLocalTime();
            return dto;
        }

        public static IEnumerable<PostFeedResponse> ConvertDatesToLocal(this IEnumerable<PostFeedResponse> dtos)
        {
            foreach (var dto in dtos) dto.ConvertDatesToLocal();
            return dtos;
        }

        public static CommentResponse ConvertDatesToLocal(this CommentResponse dto)
        {
            dto.CreatedAt = dto.CreatedAt.ConvertToLocalTime();
            return dto;
        }

        public static IEnumerable<CommentResponse> ConvertDatesToLocal(this IEnumerable<CommentResponse> dtos)
        {
            foreach (var dto in dtos) dto.ConvertDatesToLocal();
            return dtos;
        }

        public static PostReactionDetailResponse ConvertDatesToLocal(this PostReactionDetailResponse dto)
        {
            dto.CreatedAt = dto.CreatedAt.ConvertToLocalTime();
            return dto;
        }

        public static IEnumerable<PostReactionDetailResponse> ConvertDatesToLocal(this IEnumerable<PostReactionDetailResponse> dtos)
        {
            foreach (var dto in dtos) dto.ConvertDatesToLocal();
            return dtos;
        }

        // ===================== BUILDER =====================
        public static BuilderSessionSummaryResponse ConvertDatesToLocal(this BuilderSessionSummaryResponse dto)
        {
            dto.CreatedAt = dto.CreatedAt.ConvertToLocalTime();
            dto.UpdatedAt = dto.UpdatedAt.ConvertToLocalTime();
            dto.ExpiresAt = dto.ExpiresAt.ConvertToLocalTime();
            return dto;
        }

        public static List<BuilderSessionSummaryResponse> ConvertDatesToLocal(this List<BuilderSessionSummaryResponse> dtos)
        {
            foreach (var dto in dtos) dto.ConvertDatesToLocal();
            return dtos;
        }

        // ===================== ASSEMBLY TRACKING =====================
        public static AssemblyProgressLogResponse ConvertDatesToLocal(this AssemblyProgressLogResponse dto)
        {
            dto.CompletedAt = dto.CompletedAt?.ConvertToLocalTime();
            return dto;
        }

        public static IEnumerable<AssemblyProgressLogResponse> ConvertDatesToLocal(this IEnumerable<AssemblyProgressLogResponse> dtos)
        {
            foreach (var dto in dtos) dto.ConvertDatesToLocal();
            return dtos;
        }

        // ===================== WITHDRAWAL =====================
        public static WithdrawalRequestResponseDto ConvertDatesToLocal(this WithdrawalRequestResponseDto dto)
        {
            dto.RequestedAt = dto.RequestedAt.ConvertToLocalTime();
            dto.ProcessedAt = dto.ProcessedAt?.ConvertToLocalTime();
            return dto;
        }

        public static IEnumerable<WithdrawalRequestResponseDto> ConvertDatesToLocal(this IEnumerable<WithdrawalRequestResponseDto> dtos)
        {
            foreach (var dto in dtos) dto.ConvertDatesToLocal();
            return dtos;
        }

        // ===================== ASSEMBLED PRODUCT =====================
        public static AssembledProductResponse ConvertDatesToLocal(this AssembledProductResponse dto)
        {
            dto.CreatedAt = dto.CreatedAt.ConvertToLocalTime();
            return dto;
        }

        public static IEnumerable<AssembledProductResponse> ConvertDatesToLocal(this IEnumerable<AssembledProductResponse> dtos)
        {
            foreach (var dto in dtos) dto.ConvertDatesToLocal();
            return dtos;
        }

        // ===================== WITHDRAWAL SUMMARY =====================
        public static WithdrawalSummaryResponse ConvertDatesToLocal(this WithdrawalSummaryResponse dto)
        {
            dto.RequestedAt = dto.RequestedAt.ConvertToLocalTime();
            dto.ProcessedAt = dto.ProcessedAt?.ConvertToLocalTime();
            return dto;
        }

        public static IEnumerable<WithdrawalSummaryResponse> ConvertDatesToLocal(this IEnumerable<WithdrawalSummaryResponse> dtos)
        {
            foreach (var dto in dtos) dto.ConvertDatesToLocal();
            return dtos;
        }
    }
}
