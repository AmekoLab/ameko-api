namespace FPTU.Capstone.AMKCollective.Application.DTOs.Reputation
{
    public class CustomerReputationGate
    {
        public int Score { get; set; }
        public string Tier { get; set; } = string.Empty;
        public bool IsLocked { get; set; }
        public int MonthlyOrderLimit { get; set; }
        public bool CanUseAdminVoucher { get; set; }
    }
}
