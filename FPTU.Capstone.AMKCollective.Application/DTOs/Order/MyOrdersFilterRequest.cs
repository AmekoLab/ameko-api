namespace FPTU.Capstone.AMKCollective.Application.DTOs.Order
{
    public class MyOrdersFilterRequest : OrderBaseFilterRequest
    {
        /// <summary>Pending | Processing | Shipped | Completed | Cancelled | Returning | Returned | Refunded</summary>
        public string? Status { get; set; }
        public string? ShopName { get; set; }
    }
}
