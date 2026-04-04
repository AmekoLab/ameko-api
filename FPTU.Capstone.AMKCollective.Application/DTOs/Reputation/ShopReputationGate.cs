namespace FPTU.Capstone.AMKCollective.Application.DTOs.Reputation
{
    public class ShopReputationGate
    {
        public int Score { get; set; }
        public string Tier { get; set; } = string.Empty;
        public bool IsServiceSuspended { get; set; }
        public int MonthlyOrderLimit { get; set; }
        public bool CanCreateVoucher { get; set; }
    }
}
