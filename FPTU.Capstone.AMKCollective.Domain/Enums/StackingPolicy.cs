namespace FPTU.Capstone.AMKCollective.Domain.Enums
{
    /// <summary>
    /// Stacking policy for a voucher - defines which vouchers it can be combined with.
    /// </summary>
    public enum StackingPolicy
    {
        /// <summary>Does not allow combining with any other voucher.</summary>
        None = 0,

        /// <summary>Can only be combined with Compensation voucher.</summary>
        WithCompensationOnly = 1,

        /// <summary>Can be combined with any stackable voucher.</summary>
        All = 2
    }
}
