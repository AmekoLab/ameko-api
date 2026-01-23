using FPTU.Capstone.AMKCollective.Api.Controllers;
using FPTU.Capstone.AMKCollective.API.Contracts;
using FPTU.Capstone.AMKCollective.Application.DTOs;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FPTU.Capstone.AMKCollective.API.Controllers
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class BuilderController : BaseApiController
    {
        private readonly ICustomBuilderService _service;
        private readonly ILogger<BuilderController> _logger;
        public BuilderController(ICustomBuilderService service, ILogger<BuilderController> logger)
        {
            _service = service;
            _logger = logger;
        }

        /// <summary>
        /// Bắt đầu quy trình Build. Tạo Session mới và trả về bước 1.
        /// </summary>
        [HttpPost("start")]
        [ProducesResponseType(typeof(ApiResponse<BuilderStepResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> StartSession([FromBody] BuilderStartRequest request)
        {
            try
            {
                var userId = GetUserId();
                var result = await _service.StartBuilderSessionAsync(request, userId);
                return SuccessResponse(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundResponse<string>(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting builder session for Kit {KitId}", request.BaseKitId);
                return ServerErrorResponse<string>("Could not start builder session.");
            }
        }

        /// <summary>
        /// Chọn một linh kiện. Backend sẽ lưu lại và trả về bước tiếp theo.
        /// </summary>
        [HttpPost("select")]
        [ProducesResponseType(typeof(ApiResponse<BuilderStepResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> SelectPart([FromBody] BuilderSelectRequest request)
        {
            try
            {
                var result = await _service.SelectPartAsync(request);
                return SuccessResponse(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundResponse<string>(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing step {Step} for Session {SessionId}", request.StepName, request.SessionId);
                return ServerErrorResponse<string>("An error occurred while processing selection.");
            }
        }




        // GET: api/builder/config/{baseKitId}
        [HttpGet("config/{baseKitId}")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetConfig(Guid baseKitId)
        {
            try
            {
                var result = await _service.GetBuilderConfigAsync(baseKitId);
                return SuccessResponse(result);
            }
            catch (KeyNotFoundException)
            {
                return NotFoundResponse<string>("Base Kit not found.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting builder config for {BaseKitId}", baseKitId);
                return ServerErrorResponse<string>("An error occurred while getting builder config.");
            }
        }

        // GET: api/builder/search
        [HttpGet("search")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        public async Task<IActionResult> SearchParts([FromQuery] CompatiblePartsQuery query)
        {
            try
            {
                var result = await _service.SearchPartsInBuilderAsync(query);

                return SuccessResponse(new { data = result.Items, total = result.TotalCount });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching parts in builder");
                return ServerErrorResponse<string>("An error occurred while searching parts.");
            }
        }

        // POST: api/builder/validate
        [HttpPost("validate")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Validate([FromBody] ValidateBuilderRequest request)
        {
            try
            {
                bool isValid = await _service.ValidateConfigurationAsync(request.BaseKitId, request.ComponentIds);

                if (!isValid)
                {
                    return ErrorResponse<object>("Invalid configuration", new List<string> { "Configuration parts are not compatible." });
                }

                return SuccessResponse(new { isValid = true, message = "Valid configuration." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating configuration");
                return ServerErrorResponse<string>("An error occurred while validating configuration.");
            }
        }

        // GET: api/builder/check-match
        [HttpGet("check-match")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        public async Task<IActionResult> CheckMatch(Guid baseKitId, Guid componentId)
        {
            try
            {
                bool isMatch = await _service.IsMatchAsync(baseKitId, componentId);
                return SuccessResponse(new { isMatch });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking match");
                return ServerErrorResponse<string>("An error occurred while checking match.");
            }
        }

        // POST: api/builder/options
        [HttpPost("options")]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
        public async Task<IActionResult> CreateOption([FromForm] CreateKitOptionRequest request)
        {
            try
            {
                var dto = new CreateKitOptionDto
                {
                    BaseKitId = request.BaseKitId,
                    ComponentId = request.ComponentId,
                    StepName = request.StepName,
                    StepOrder = request.StepOrder,
                    IsDefault = request.IsDefault,
                };

                if (request.LayerImageFile != null)
                {
                    dto.FileStream = request.LayerImageFile.OpenReadStream();
                    dto.FileName = request.LayerImageFile.FileName;
                }

                await _service.CreateOptionAsync(dto);
                return SuccessResponse("Create option successful");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating builder option");
                return ServerErrorResponse<string>("An error occurred while creating option.");
            }
        }

        // DELETE: api/builder/options/{id}
        [HttpDelete("options/{id}")]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
        public async Task<IActionResult> DeleteOption(Guid id)
        {
            try
            {
                await _service.DeleteOptionAsync(id);
                return SuccessResponse("Option deleted successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting builder option {Id}", id);
                return ServerErrorResponse<string>("An error occurred while deleting option.");
            }
        }

        // POST: api/builder/options/bulk
        [HttpPost("options/bulk")]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> BulkCreateOptions([FromForm] List<CreateKitOptionRequest> requests)
        {
            try
            {
                if (requests == null || requests.Count == 0)
                {
                    return ErrorResponse<string>("List is empty");
                }

                var dtos = new List<CreateKitOptionDto>();

                foreach (var req in requests)
                {
                    var dto = new CreateKitOptionDto
                    {
                        BaseKitId = req.BaseKitId,
                        ComponentId = req.ComponentId,
                        StepName = req.StepName,
                        StepOrder = req.StepOrder,
                        IsDefault = req.IsDefault,
                    };

                    if (req.LayerImageFile != null)
                    {
                        dto.FileStream = req.LayerImageFile.OpenReadStream();
                        dto.FileName = req.LayerImageFile.FileName;
                    }

                    dtos.Add(dto);
                }

                await _service.BulkCreateOptionsAsync(dtos);

                return SuccessResponse($"Bulk import successful: {requests.Count} items.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error bulk creating options");
                return ServerErrorResponse<string>("An error occurred while bulk creating options.");
            }
        }

        // DELETE: api/builder/config/{baseKitId}
        [HttpDelete("config/{baseKitId}")]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ResetConfig(Guid baseKitId)
        {
            try
            {
                await _service.ResetBuilderConfigAsync(baseKitId);
                return SuccessResponse("Reset successfully");
            }
            catch (KeyNotFoundException)
            {
                return NotFoundResponse<string>("Base kit not found");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting config for {BaseKitId}", baseKitId);
                return ServerErrorResponse<string>("An error occurred while resetting config.");
            }
        }

        [HttpGet("session/{sessionId}")]
        public async Task<IActionResult> ResumeSession(Guid sessionId, [FromQuery] string? stepName = null)
        {
            try
            {
                var result = await _service.GetExistingSessionAsync(sessionId, stepName);
                return SuccessResponse(result);
            }
            catch (KeyNotFoundException)
            {
                return NotFoundResponse<string>("Session expired or not found. Please start over.");
            }
        }

        private Guid GetUserId()
        {
            var idClaim = User.FindFirst("id") ?? User.FindFirst(ClaimTypes.NameIdentifier);
            if (idClaim == null) throw new UnauthorizedAccessException("Invalid Token");
            return Guid.Parse(idClaim.Value);
        }

    }
}