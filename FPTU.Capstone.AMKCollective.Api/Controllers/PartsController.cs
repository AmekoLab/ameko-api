using FPTU.Capstone.AMKCollective.Api.Controllers;
using FPTU.Capstone.AMKCollective.API.Contracts;
using FPTU.Capstone.AMKCollective.Application.DTOs;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FPTU.Capstone.AMKCollective.API.Controllers
{
    [Route("api/v1/parts")]
    [ApiController]
    public class PartsController : BaseApiController
    {
        private readonly IProductService _service;
        private readonly ILogger<PartsController> _logger;
        public PartsController(IProductService service, ILogger<PartsController> logger) { 
            _service = service;
            _logger = logger;
        }
        //Filter and paging
        //GET: api/parts?pageNumber=36&partType=CASE&searchTerm=whut
        [HttpGet]
        public async Task<IActionResult> GetList([FromQuery] PartQueryParams query)
        {
            try
            {
                var result = await _service.GetListAsync(query);
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
        [ProducesResponseType(typeof(ApiResponse<PartDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
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
                return ServerErrorResponse<string>("An error occurred while fetching part detail");
            }
        }

        // POST: api/parts
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<PartDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Create([FromForm] CreateUpdatePartApiRequest request)
        {
            try
            {
                var dto = MapToDto(request);
            
                var userId = GetUserId();
                var result = await _service.CreateAsync(userId, dto);
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
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(Guid id, [FromForm] CreateUpdatePartApiRequest request)
        {
            try
            {
                var dto = MapToDto(request);
                await _service.UpdateAsync(id, dto);
                return SuccessResponse("Part updated successfully");
            }
            catch (KeyNotFoundException)
            {
                return NotFoundResponse<string>($"Part with id {id} not found");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating part: {Id}", id);
                return ServerErrorResponse<string>("An error occurred while updating part");
            }
        }

        // DELETE: api/parts/{id}
        [HttpDelete("{id}")]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                await _service.DeleteAsync(id);
                return SuccessResponse("Part deleted successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting part: {Id}", id);
                return ServerErrorResponse<string>("An error occurred while deleting part");
            }
        }
        // GET: api/parts/recommendations
        [HttpGet("recommendations")]
        [ProducesResponseType(typeof(ApiResponse<IEnumerable<PartDto>>), StatusCodes.Status200OK)]
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
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
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

        // Helper Mapping
        private CreateUpdatePartDto MapToDto(CreateUpdatePartApiRequest request)
        {
            var dto = new CreateUpdatePartDto
            {
                CategoryId = request.CategoryId,
                Name = request.Name,
                PartType = request.PartType,
                Price = request.Price,
                StockQuantity = request.StockQuantity,
                Description = request.Description,
                Specifications = request.Specifications
            };

            if (request.ThumbnailImage != null)
            {
                dto.ImageStream = request.ThumbnailImage.OpenReadStream();
                dto.ImageFileName = request.ThumbnailImage.FileName;
            }
            if (request.LayerImage != null)
            {
                dto.LayerImageStream = request.LayerImage.OpenReadStream();
                dto.LayerImageFileName = request.LayerImage.FileName;
            }

            return dto;
        }

        private Guid GetUserId()
        {
            var idClaim = User.FindFirst("id") ?? User.FindFirst(ClaimTypes.NameIdentifier);
            if (idClaim == null) throw new UnauthorizedAccessException("Invalid Token");
            return Guid.Parse(idClaim.Value);
        }
    }
}