using FPTU.Capstone.AMKCollective.Api.Controllers;
using FPTU.Capstone.AMKCollective.Application.DTOs;
using FPTU.Capstone.AMKCollective.Application.DTOs.Builder;
using FPTU.Capstone.AMKCollective.Application.DTOs.Common;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
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
        /// [USER] Starts a new Builder Session.
        /// </summary>
        /// <remarks>
        /// Call this when the user clicks "Build Now".
        /// <br/>
        /// <b>Logic:</b>
        /// 1. Generates a new `SessionId`.
        /// 2. Reads the `workflow` JSON from the Base Kit (e.g., finding that Step 1 is "case").
        /// 3. Returns the list of compatible parts for Step 1.
        /// </remarks>
        /// <param name="request">Contains the `BaseKitId`.</param>
        /// <returns>The Session ID and the configuration for the FIRST step.</returns>
        [HttpPost("start")]
        [SwaggerOperation(
            Summary = "Start Builder Session",
            Description = "Initiates a new custom keyboard building session for a specific Base Kit. Returns the first step of the building process.")]
        [SwaggerResponse(200, "Session started successfully", typeof(ApiResponse<BuilderStepResponse>))]
        [SwaggerResponse(404, "Base Kit not found")]
        [SwaggerResponse(500, "Internal server error")]
        public async Task<IActionResult> StartSession([FromBody] BuilderStartRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
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
        /// [USER] Selects a part and moves to the Next Step.
        /// </summary>
        /// <remarks>
        /// <b>CORE API:</b> Call this when the user clicks on a component card.
        /// <br/>
        /// <b>Backend Logic:</b>
        /// 1. Saves the selected part to the Session.
        /// 2. Updates the `CurrentPreviewImage` (Visual Branching logic).
        /// 3. Calculates the `TotalPrice`.
        /// 4. Determines the <b>Next Step</b> based on the workflow JSON.
        /// 5. Returns the <b>Filtered List</b> of parts for the next step (e.g., if Case Black is selected, only show Plates compatible with Case Black).
        /// </remarks>
        /// <param name="request">SessionId, Current StepName, and Selected PartId.</param>
        /// <returns>The Next Step configuration, updated Preview Image URL, and updated Total Price.</returns>
        [HttpPost("select")]
        [SwaggerOperation(
    Summary = "Select Part for Builder",
    Description = "Records a user's choice for a specific component in the builder session and returns the next step configuration."
)]
        [SwaggerResponse(200, "Part selected successfully", typeof(ApiResponse<BuilderStepResponse>))]
        [SwaggerResponse(400, "Invalid selection or step")]
        [SwaggerResponse(404, "Session or part not found")]
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




        /// <summary>
        /// [ADMIN] Get the configuration rules of a Base Kit.
        /// </summary>
        /// <remarks>
        /// Used by the Shop Owner dashboard to see what parts are linked to this Kit.
        /// </remarks>
        [HttpGet("config/{baseKitId}")]
        [SwaggerOperation(
    Summary = "Get Builder Config",
    Description = "Retrieves the initial configuration and available slots for a specific Base Kit."
)]
        [SwaggerResponse(200, "Configuration retrieved", typeof(ApiResponse<object>))]
        [SwaggerResponse(404, "Base Kit not found")]
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

        /// <summary>
        /// [USER] Search for compatible parts within the current session.
        /// </summary>
        /// <remarks>
        /// Useful for a Search Bar inside the Builder UI. 
        /// It ensures returned parts are compatible with previous selections (e.g., searching "FR4" only returns FR4 plates that fit the selected Case).
        /// </remarks>
        [HttpGet("search")]
        [SwaggerOperation(
    Summary = "Search Compatible Parts",
    Description = "Searches for parts that are compatible with the current builder session context."
)]
        [SwaggerResponse(200, "Search results retrieved", typeof(ApiResponse<object>))]
        public async Task<IActionResult> SearchParts([FromQuery] GetCompatiblePartsRequest query)
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

        /// <summary>
        /// [USER] Validates the entire configuration.
        /// </summary>
        /// <remarks>
        /// Call this before "Add to Cart" to ensure all selected parts are compatible and stock is available.
        /// </remarks>
        [HttpPost("validate")]
        [SwaggerOperation(
    Summary = "Validate Configuration",
    Description = "Validates if the selected list of components form a valid and compatible keyboard configuration."
)]
        [SwaggerResponse(200, "Configuration is valid")]
        [SwaggerResponse(400, "Configuration is invalid (details in response)")]
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

        /// <summary>
        /// [UI HELPER] Quick check if a part fits the Kit.
        /// </summary>
        /// <remarks>
        /// Can be used by UI to grey out incompatible items in a list without reloading the whole step.
        /// </remarks>
        [HttpGet("check-match")]
        [SwaggerOperation(
    Summary = "Check Compatibility",
    Description = "Checks if a specific component ID is compatible with the given Base Kit ID."
)]
        [SwaggerResponse(200, "Check successful", typeof(ApiResponse<object>))]
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

        /// <summary>
        ///  Create a specific Option (Rule) for a Kit.
        /// </summary>
        /// <remarks>
        /// Links a Component to a Step of the Kit.
        /// <br/>
        /// <b>Payload fields:</b>
        /// <ul>
        /// <li>`StepName`: Which step this part belongs to (e.g., "case").</li>
        /// <li>`NextStepFilterRule`: Tags to filter the NEXT step (e.g., "plate:case-black").</li>
        /// <li>`LayerImage`: The accumulated image to display when this is selected.</li>
        /// </ul>
        /// </remarks>
        [HttpPost("options")]
        [SwaggerOperation(
    Summary = "Create Kit Option",
    Description = "Define a new selectable option slot for a keyboard kit."
)]
        [SwaggerResponse(200, "Option created successfully")]
        public async Task<IActionResult> CreateOption([FromForm] CreateKitOptionRequest request)
        {
            try
            {
                await _service.CreateOptionAsync(request);
                return SuccessResponse("Create option successful");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating builder option");
                return ServerErrorResponse<string>("An error occurred while creating option.");
            }
        }

        /// <summary>
        /// Delete a specific Option.
        /// </summary>
        [HttpDelete("options/{id}")]
        [SwaggerOperation(
    Summary = "Delete Kit Option",
    Description = "Removes a configuration option from a kit."
)]
        [SwaggerResponse(200, "Option deleted successfully")]
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

        /// <summary>
        /// Bulk create options (Import).
        /// </summary>
        /// <remarks>
        /// Useful for importing configuration from Excel/CSV.
        /// </remarks>
        [HttpPost("options/bulk")]
        [SwaggerOperation(
    Summary = "Bulk Create Options",
    Description = "Import multiple kit options at once."
)]
        [SwaggerResponse(200, "Bulk import successful")]
        [SwaggerResponse(400, "Request list is empty")]
        public async Task<IActionResult> BulkCreateOptions([FromForm] List<CreateKitOptionRequest> requests)
        {
            try
            {
                if (requests == null || requests.Count == 0)
                {
                    return ErrorResponse<string>("List is empty");
                }

                await _service.BulkCreateOptionsAsync(requests);

                return SuccessResponse($"Bulk import successful: {requests.Count} items.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error bulk creating options");
                return ServerErrorResponse<string>("An error occurred while bulk creating options.");
            }
        }

        /// <summary>
        ///  Reset the entire configuration of a Kit.
        /// </summary>
        /// <remarks>
        /// <b>Warning:</b> This deletes all links/options for the specified Kit. Use with caution.
        /// </remarks>
        [HttpDelete("config/{baseKitId}")]
        [SwaggerOperation(
    Summary = "Reset Builder Config",
    Description = "Resets the configuration of a base kit to its default state."
)]
        [SwaggerResponse(200, "Reset successful")]
        [SwaggerResponse(404, "Base Kit not found")]
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

        /// <summary>
        /// [USER] Resumes an existing Session.
        /// </summary>
        /// <remarks>
        /// Call this if the user refreshes the page (F5) or returns to the builder later.
        /// It restores the exact state (Current Step, Selected Parts, Preview Image).
        /// </remarks>
        /// <param name="sessionId">The ID of the active session.</param>
        /// <param name="stepName">Optional: If provided, jumps to specific step view (if allowed).</param>
        /// <returns>The full state of the session.</returns>
        [HttpGet("session/{sessionId}")]
        [SwaggerOperation(
    Summary = "Resume Session",
    Description = "Retrieves the state of an existing builder session."
)]
        [SwaggerResponse(200, "Session resumed", typeof(ApiResponse<BuilderStepResponse>))]
        [SwaggerResponse(404, "Session expired or not found")]
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
    }
}