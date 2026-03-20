using FPTU.Capstone.AMKCollective.Api.Controllers;
using FPTU.Capstone.AMKCollective.Application.DTOs.AssemblyTracking;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FPTU.Capstone.AMKCollective.API.Controllers
{
    [Route("api/v1/assembly-tracking")]
    [ApiController]
    public class AssemblyTrackingController : BaseApiController
    {
        private readonly IAssemblyTrackingService _trackingService;
        private readonly IShopService _shopService;

        public AssemblyTrackingController(IAssemblyTrackingService trackingService, IShopService shopService)
        {
            _trackingService = trackingService;
            _shopService = shopService;
        }
        /// <summary>
        /// [Shop Owner] Khởi tạo quy trình lắp ráp cho một sản phẩm Custom
        /// </summary>
        /// <remarks>
        /// Gọi API này khi Shop bắt đầu xử lý đơn hàng (VD: Chuyển Order sang Processing).
        /// Hệ thống sẽ copy các bước từ Template mặc định của Shop sang đơn hàng này để khách bắt đầu theo dõi.
        /// </remarks>
        [HttpPost("logs/order-item/{orderItemId}/initialize")]
        [Authorize]
        public async Task<IActionResult> InitializeTracking(Guid orderItemId)
        {
            try
            {
                var userId = GetCurrentUserId();
                var shop = await _shopService.GetMyShopAsync(userId);

                if (shop == null)
                    return ErrorResponse<object>("This account does not own a shop.");

                await _trackingService.GenerateTrackingLogsForOrderItemAsync(orderItemId, shop.Id);

                return SuccessResponse("Tracking timeline initialized successfully.");
            }
            catch (InvalidOperationException ex)
            {
                return ErrorResponse<object>(ex.Message);
            }
            catch (Exception ex)
            {
                return ServerErrorResponse<object>(ex.Message);
            }
        }

        #region Template Management (For Shop Owners)

        /// <summary>
        /// Retrieves all predefined assembly step templates for a specific shop.
        /// </summary>
        [HttpGet("templates/shop")]
        [Authorize]
        public async Task<IActionResult> GetTemplatesByShop()
        {
            try
            {
                var userId = GetCurrentUserId();

                var shop = await _shopService.GetMyShopAsync(userId);

                var templates = await _trackingService.GetTemplatesByShopIdAsync(shop.Id);
                return SuccessResponse(templates, "Get templates successfully.");
            }
            catch (Exception ex)
            {
                return ServerErrorResponse<object>(ex.Message);
            }
        }

        /// <summary>
        /// Creates a new default assembly step template for a shop.
        /// </summary>
        [HttpPost("templates/shop")]
        [Authorize]
        public async Task<IActionResult> CreateTemplate([FromBody] SaveAssemblyStepTemplateRequest request)
        {
            if (!ModelState.IsValid)
                return ErrorResponse<object>("Validation failed", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList());

            try
            {
                var userId = GetCurrentUserId();
                var shop = await _shopService.GetMyShopAsync(userId);
                var template = await _trackingService.CreateTemplateAsync(shop.Id, request);
                return SuccessResponse(template, "Template created successfully.");
            }
            catch (Exception ex)
            {
                return ServerErrorResponse<object>(ex.Message);
            }
        }

        /// <summary>
        /// Updates an existing assembly step template.
        /// </summary>
        [HttpPut("templates/{templateId}")]
        [Authorize]
        public async Task<IActionResult> UpdateTemplate(Guid templateId, [FromBody] SaveAssemblyStepTemplateRequest request)
        {
            if (!ModelState.IsValid)
                return ErrorResponse<object>("Validation failed", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList());

            try
            {
                var userId = GetCurrentUserId();
                var shop = await _shopService.GetMyShopAsync(userId);
                if (shop == null)
                    return ErrorResponse<object>("This account does not own a shop.");

                var template = await _trackingService.UpdateTemplateAsync(templateId, shop.Id, request);
                return SuccessResponse(template, "Template updated successfully.");
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundResponse<object>(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return UnauthorizedResponse<object>(ex.Message);
            }
            catch (Exception ex)
            {
                return ServerErrorResponse<object>(ex.Message);
            }
        }

        /// <summary>
        /// Deletes an assembly step template.
        /// </summary>
        [HttpDelete("templates/{templateId}")]
        [Authorize]
        public async Task<IActionResult> DeleteTemplate(Guid templateId)
        {
            try
            {
                var userId = GetCurrentUserId();
                var shop = await _shopService.GetMyShopAsync(userId);
                if (shop == null)
                    return ErrorResponse<object>("This account does not own a shop.");

                await _trackingService.DeleteTemplateAsync(templateId, shop.Id);
                return SuccessResponse("Template deleted successfully.");
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundResponse<object>(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return UnauthorizedResponse<object>(ex.Message);
            }
            catch (Exception ex)
            {
                return ServerErrorResponse<object>(ex.Message);
            }
        }

        /// <summary>
        /// Xóa một bước (step) khỏi quy trình lắp ráp hiện tại.
        /// </summary>
        [HttpDelete("logs/{progressLogId}")]
        [Authorize]
        public async Task<IActionResult> DeleteProgressLog(Guid progressLogId)
        {
            try
            {
                var userId = GetCurrentUserId();
                var shop = await _shopService.GetMyShopAsync(userId);

                if (shop == null)
                    return ErrorResponse<object>("This account does not own a shop.");

                await _trackingService.DeleteProgressLogAsync(progressLogId, shop.Id);

                return SuccessResponse("Assembly step deleted successfully.");
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundResponse<object>(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return UnauthorizedResponse<object>(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return ErrorResponse<object>(ex.Message);
            }
            catch (Exception ex)
            {
                return ServerErrorResponse<object>(ex.Message);
            }
        }


        #endregion

        #region Tracking Logs Management (For Customer & Shop Staff)

        /// <summary>
        /// Retrieves the entire assembly progress timeline for a specific order item.
        /// </summary>
        [HttpGet("logs/order-item/{orderItemId}")]
        [Authorize]
        public async Task<IActionResult> GetTrackingLogs(Guid orderItemId)
        {
            try
            {
                var userId = GetCurrentUserId();
                var logs = await _trackingService.GetTrackingLogsAsync(orderItemId, userId);
                return SuccessResponse(logs, "Get tracking logs successfully.");
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundResponse<object>(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return UnauthorizedResponse<object>(ex.Message);
            }
            catch (Exception ex)
            {
                return ServerErrorResponse<object>(ex.Message);
            }
        }

        /// <summary>
        /// Updates the status, note, and uploads a media file (image/video) for a specific assembly step.
        /// </summary>
        [HttpPut("logs/{progressLogId}")]
        [Authorize]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UpdateProgressLog(Guid progressLogId, [FromForm] UpdateAssemblyProgressRequest request)
        {
            if (!ModelState.IsValid)
                return ErrorResponse<object>("Validation failed", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList());

            try
            {
                var userId = GetCurrentUserId();
                var shop = await _shopService.GetMyShopAsync(userId);
                if (shop == null)
                    return ErrorResponse<object>("This account does not own a shop.");

                var updatedLog = await _trackingService.UpdateProgressLogAsync(progressLogId, shop.Id, request);
                return SuccessResponse(updatedLog, "Progress log updated successfully.");
            }
            catch (InvalidOperationException ex)
            {
                return ErrorResponse<object>(ex.Message);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundResponse<object>(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return UnauthorizedResponse<object>(ex.Message);
            }
            catch (Exception ex)
            {
                return ServerErrorResponse<object>(ex.Message);
            }
        }

        /// <summary>
        /// Adds a spontaneous or extra assembly step (ad-hoc step).
        /// </summary>
        [HttpPost("logs/order-item/{orderItemId}/adhoc")]
        [Authorize]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> AddAdhocStep(Guid orderItemId, [FromForm] AddAdhocStepRequest request)
        {
            if (!ModelState.IsValid)
                return ErrorResponse<object>("Validation failed", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList());

            try
            {
                var userId = GetCurrentUserId();
                var shop = await _shopService.GetMyShopAsync(userId);
                if (shop == null)
                    return ErrorResponse<object>("This account does not own a shop.");

                var newLog = await _trackingService.AddAdhocStepAsync(orderItemId, shop.Id, request);
                return SuccessResponse(newLog, "Adhoc step added successfully.");
            }
            catch (ArgumentException ex)
            {
                return ErrorResponse<object>(ex.Message);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundResponse<object>(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return UnauthorizedResponse<object>(ex.Message);
            }
            catch (Exception ex)
            {
                return ServerErrorResponse<object>(ex.Message);
            }
        }

        #endregion
    }
}