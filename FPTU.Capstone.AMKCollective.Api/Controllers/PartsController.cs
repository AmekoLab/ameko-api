using FPTU.Capstone.AMKCollective.Api.Controllers;
using FPTU.Capstone.AMKCollective.Application.DTOs;
using FPTU.Capstone.AMKCollective.Application.DTOs.Category;
using FPTU.Capstone.AMKCollective.Application.DTOs.Common;
using FPTU.Capstone.AMKCollective.Application.DTOs.Part;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
namespace FPTU.Capstone.AMKCollective.API.Controllers
{
    [Route("api/v1/parts")]
    [ApiController]
    public class PartsController : BaseApiController
    {
        private readonly IProductService _service;
        private readonly ILogger<PartsController> _logger;
        public PartsController(IProductService service, ILogger<PartsController> logger)
        {
            _service = service;
            _logger = logger;
        }
        //Filter and paging
        //GET: api/parts?pageNumber=36&partType=CASE&searchTerm=whut
        [HttpGet]
        [SwaggerOperation(
    Summary = "Search and Filter Parts",
    Description = "Retrieves a paginated list of parts with optional filtering by type, search term, etc."
)]
        [SwaggerResponse(200, "List of parts retrieved", typeof(PaginatedResult<PartResponse>))]
        public async Task<IActionResult> GetList([FromQuery] GetPartsFilterRequest query)
        {
            try
            {
                var userId = TryGetCurrentUserId();
                var result = await _service.GetListAsync(query, userId);
                return SuccessResponse(new { data = result.Items, total = result.TotalCount });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting parts list");
                return ServerErrorResponse<string>("An error occurred while fetching parts list");
            }

        }

        // GET: api/parts/detail/case-tkl-a-67
        [HttpGet("detail/{slug}")]
        [SwaggerOperation(
    Summary = "Get Part Details",
    Description = "Retrieves detailed information about a specific part using its slug."
)]
        [SwaggerResponse(200, "Part details retrieved", typeof(ApiResponse<PartResponse>))]
        [SwaggerResponse(404, "Part not found")]
        public async Task<IActionResult> GetBySlug(string slug)
        {
            try
            {
                var result = await _service.GetBySlugAsync(slug);
                return SuccessResponse(result);
            }
            catch (KeyNotFoundException)
            {
                return NotFoundResponse<string>("Item not found");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting part detail: {Slug}", slug);
                return ServerErrorResponse<string>("An unexpected error occurred.");
            }
        }
        
        
        /// <summary>
        /// Returns a server error response
        /// </summary>

        // POST: api/parts
        [HttpPost]
        //TODO: uncomment sau khi test xong
        //[Authorize(Roles = "Shop")] 
        [SwaggerOperation(
    Summary = "Create Part (Shop/Admin)",
    Description = "Creates a new part/product listing."
)]
        [SwaggerResponse(200, "Part created successfully", typeof(ApiResponse<PartResponse>))]
        [SwaggerResponse(401, "Unauthorized")]
        public async Task<IActionResult> Create([FromForm] CreateUpdatePartRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _service.CreateAsync(userId, request);
                return SuccessResponse(result, "Part created successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating part");
                return ServerErrorResponse<string>("An error occurred while creating part");
            }
        }

        // PUT: api/parts/{id}
        [HttpPut("{id}")]
        //TODO: uncomment authorize sau khi test xong
        //[Authorize(Roles = "Shop")] 
        [SwaggerOperation(
    Summary = "Update Part (Shop/Admin)",
    Description = "Updates an existing part's information."
)]
        [SwaggerResponse(200, "Part updated successfully")]
        [SwaggerResponse(404, "Part not found")]
        public async Task<IActionResult> Update(Guid id, [FromForm] CreateUpdatePartRequest request)
        {
            try
            {
                var userId = GetCurrentUserId(); 
                await _service.UpdateAsync(userId, id, request);
                return SuccessResponse("Part updated successfully");
            }
            catch (KeyNotFoundException)
            {
                return NotFoundResponse<string>($"Part with id {id} not found");
            }
            catch (UnauthorizedAccessException ex)
            {
                return UnauthorizedResponse<string>(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating part: {Id}", id);
                return ServerErrorResponse<string>("An error occurred while updating part");
            }
        }

        // DELETE: api/parts/{id}
        [HttpDelete("{id}")]
        //TODO: uncomment sau khi test xong
       // [Authorize(Roles = "Shop")] 
        [SwaggerOperation(
    Summary = "Delete Part (Shop/Admin)",
    Description = "Soft deletes a part."
)]
        [SwaggerResponse(200, "Part deleted successfully")]
        [SwaggerResponse(404, "Part not found")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                var userId = GetCurrentUserId(); 
                await _service.DeleteAsync(userId, id);
                return SuccessResponse("Part deleted successfully");
            }
            catch (KeyNotFoundException)
            {
                return NotFoundResponse<string>($"Part with id {id} not found");
            }
            catch (UnauthorizedAccessException ex) 
            {
                return UnauthorizedResponse<string>(ex.Message);
            }
            catch (InvalidOperationException ex) 
            {
                return ErrorResponse<string>(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting part: {Id}", id);
                return ServerErrorResponse<string>("An error occurred while deleting part");
            }
        }
        // GET: api/parts/recommendations
        [HttpGet("recommendations")]
        [SwaggerOperation(
    Summary = "Get Recommended Parts",
    Description = "Suggests compatible parts based on a base kit and part type."
)]
        [SwaggerResponse(200, "Recommendations retrieved", typeof(ApiResponse<IEnumerable<PartResponse>>))]
        public async Task<IActionResult> GetRecommendations(Guid baseKitId, string partType)
        {
            try
            {
                var result = await _service.GetRecommendationsAsync(baseKitId, partType);
                return SuccessResponse(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recommendations");
                return ServerErrorResponse<string>("An error occurred fetching recommendations");
            }
        }

        // POST: api/parts/check-stock
        [HttpPost("check-stock")]
        [SwaggerOperation(
    Summary = "Check Stock Availability",
    Description = "Checks if the requested list of product IDs are in stock."
)]
        [SwaggerResponse(200, "Stock status retrieved")]
        public async Task<IActionResult> CheckStock([FromBody] CheckStockRequest request)
        {
            try
            {
                var result = await _service.CheckStockAvailabilityAsync(request.ProductIds);
                return SuccessResponse(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking stock");
                return ServerErrorResponse<string>("An error occurred while checking stock");
            }
        }
    }
}