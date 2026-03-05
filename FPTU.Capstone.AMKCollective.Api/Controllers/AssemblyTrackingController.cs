using FPTU.Capstone.AMKCollective.Api.Controllers;
using FPTU.Capstone.AMKCollective.Application.DTOs.AssemblyTracking;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FPTU.Capstone.AMKCollective.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AssemblyTrackingController : BaseApiController
    {
        private readonly IAssemblyTrackingService _trackingService;

        public AssemblyTrackingController(IAssemblyTrackingService trackingService)
        {
            _trackingService = trackingService;
        }

        #region Template Management (For Shop Owners)

        /// <summary>
        /// Retrieves all predefined assembly step templates for a specific shop.
        /// </summary>
        [HttpGet("templates/shop/{shopId}")]
        public async Task<IActionResult> GetTemplatesByShop(Guid shopId)
        {
            try
            {
                var templates = await _trackingService.GetTemplatesByShopIdAsync(shopId);
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
        [HttpPost("templates/shop/{shopId}")]
        public async Task<IActionResult> CreateTemplate(Guid shopId, [FromBody] SaveAssemblyStepTemplateRequest request)
        {
            if (!ModelState.IsValid)
                return ErrorResponse<object>("Validation failed", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList());

            try
            {
                var template = await _trackingService.CreateTemplateAsync(shopId, request);
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
        public async Task<IActionResult> UpdateTemplate(Guid templateId, [FromBody] SaveAssemblyStepTemplateRequest request)
        {
            if (!ModelState.IsValid)
                return ErrorResponse<object>("Validation failed", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList());

            try
            {
                var template = await _trackingService.UpdateTemplateAsync(templateId, request);
                return SuccessResponse(template, "Template updated successfully.");
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundResponse<object>(ex.Message);
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
        public async Task<IActionResult> DeleteTemplate(Guid templateId)
        {
            try
            {
                await _trackingService.DeleteTemplateAsync(templateId);
                return SuccessResponse("Template deleted successfully.");
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundResponse<object>(ex.Message);
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
        public async Task<IActionResult> GetTrackingLogs(Guid orderItemId)
        {
            try
            {
                var logs = await _trackingService.GetTrackingLogsAsync(orderItemId);
                return SuccessResponse(logs, "Get tracking logs successfully.");
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
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UpdateProgressLog(Guid progressLogId, [FromForm] UpdateAssemblyProgressRequest request)
        {
            if (!ModelState.IsValid)
                return ErrorResponse<object>("Validation failed", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList());

            try
            {
                var updatedLog = await _trackingService.UpdateProgressLogAsync(progressLogId, request);
                return SuccessResponse(updatedLog, "Progress log updated successfully.");
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundResponse<object>(ex.Message);
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
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> AddAdhocStep(Guid orderItemId, [FromForm] AddAdhocStepRequest request)
        {
            if (!ModelState.IsValid)
                return ErrorResponse<object>("Validation failed", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList());

            try
            {
                var newLog = await _trackingService.AddAdhocStepAsync(orderItemId, request);
                return SuccessResponse(newLog, "Adhoc step added successfully.");
            }
            catch (Exception ex)
            {
                return ServerErrorResponse<object>(ex.Message);
            }
        }

        #endregion
    }
}