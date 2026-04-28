using FPTU.Capstone.AMKCollective.Api.Controllers;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace FPTU.Capstone.AMKCollective.API.Controllers
{
    [Route("api/v1/invoices")]
    [ApiController]
    public class InvoiceController : BaseApiController
    {
        private readonly IInvoiceService _invoiceService;
        private readonly ILogger<InvoiceController> _logger;

        public InvoiceController(IInvoiceService invoiceService, ILogger<InvoiceController> logger)
        {
            _invoiceService = invoiceService;
            _logger = logger;
        }

        [HttpGet("orders/{orderId:guid}")]
        [Authorize]
        [SwaggerOperation(
            Summary = "Download Order Invoice",
            Description = "Generates and returns a PDF invoice for the specified order. Accessible by the customer who placed the order or the shop owner."
        )]
        [SwaggerResponse(200, "PDF invoice file")]
        [SwaggerResponse(403, "Access denied")]
        [SwaggerResponse(404, "Order not found")]
        public async Task<IActionResult> GetOrderInvoice(Guid orderId, CancellationToken token)
        {
            try
            {
                var userId = GetCurrentUserId();
                var pdfBytes = await _invoiceService.GenerateOrderInvoiceAsync(userId, orderId, token);
                var fileName = $"invoice-{orderId.ToString()[..8].ToUpper()}.pdf";
                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundResponse<string>(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return ForbiddenResponse<string>(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating invoice for order {OrderId}", orderId);
                return ServerErrorResponse<string>("Error generating invoice");
            }
        }
    }
}
