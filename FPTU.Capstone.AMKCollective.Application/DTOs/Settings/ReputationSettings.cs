namespace FPTU.Capstone.AMKCollective.Application.DTOs.Settings
{
    public class ReputationSettings
    {
        public int MinScore { get; set; } = 0;
        public int MaxScore { get; set; } = 100;
        public int DefaultCustomerScore { get; set; } = 100;
        public int DefaultShopScore { get; set; } = 100;

        public int CustomerHighMinScore { get; set; } = 90;
        public int CustomerMidMinScore { get; set; } = 80;
        public int CustomerLowMinScore { get; set; } = 50;
        public int CustomerHighMonthlyOrderLimit { get; set; } = 0;
        public int CustomerMidMonthlyOrderLimit { get; set; } = 10;
        public int CustomerLowMonthlyOrderLimit { get; set; } = 3;
        public bool CustomerHighCanUseAdminVoucher { get; set; } = true;
        public bool CustomerMidCanUseAdminVoucher { get; set; } = false;
        public bool CustomerLowCanUseAdminVoucher { get; set; } = false;

        public int ShopHighMinScore { get; set; } = 90;
        public int ShopMidMinScore { get; set; } = 80;
        public int ShopLowMinScore { get; set; } = 60;
        public int ShopHighMonthlyOrderLimit { get; set; } = 0;
        public int ShopMidMonthlyOrderLimit { get; set; } = 20;
        public int ShopLowMonthlyOrderLimit { get; set; } = 10;
        public bool ShopHighCanCreateVoucher { get; set; } = true;
        public bool ShopMidCanCreateVoucher { get; set; } = true;
        public bool ShopLowCanCreateVoucher { get; set; } = false;

        public int MaxMonthlyAutoCancels { get; set; } = 10;
        public int SlowResponseResetSuccessCount { get; set; } = 3;

        public int PointsPerSuccessfulOrder { get; set; } = 1;
        public int PointsDeductArtisanFault { get; set; } = 5;
        public int PointsDeductNoResponseAfterAccept { get; set; } = 5;
        public int PointsDeductValidComplaint { get; set; } = 5;
    }
}
