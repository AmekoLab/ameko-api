namespace FPTU.Capstone.AMKCollective.Domain.Enums
{
    public enum NotificationType
    {
        System = 0,
        PostCreated = 1,
        Comment = 2,
        Reaction = 3,
        OrderCreated = 4,
        OrderStatusUpdated = 5,
        Warranty = 6,
        Product = 7,
        Refund = 8,
        WalletTransaction = 9,   // Doanh thu giải ngân, nạp tiền, rút tiền
        ShopStatusUpdated = 10,  // Duyệt / từ chối / ban shop
        CommissionRequest = 11,  // Yêu cầu commission mới hoặc bị hủy
        QuoteReceived = 12,      // Shop gửi báo giá
        QuoteStatusUpdated = 13  // Báo giá được accept / reject / revoke
    }
}
