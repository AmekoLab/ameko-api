namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Services
{
    public interface IInvoiceService
    {
        /// <summary>
        /// Generate invoice PDF for an order.
        /// Caller must be the customer who placed the order OR the shop owner.
        /// </summary>
        Task<byte[]> GenerateOrderInvoiceAsync(Guid requesterId, Guid orderId, CancellationToken token = default);
    }
}
