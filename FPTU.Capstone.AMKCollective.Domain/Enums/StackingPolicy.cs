namespace FPTU.Capstone.AMKCollective.Domain.Enums
{
    /// <summary>
    /// Chính sách stacking của một Voucher — ai nó được kết hợp cùng.
    /// </summary>
    public enum StackingPolicy
    {
        /// <summary>Không cho phép kết hợp với bất kỳ voucher nào khác.</summary>
        None = 0,

        /// <summary>Chỉ được kết hợp với Compensation voucher.</summary>
        WithCompensationOnly = 1,

        /// <summary>Kết hợp được với mọi Stackable voucher.</summary>
        All = 2
    }
}
