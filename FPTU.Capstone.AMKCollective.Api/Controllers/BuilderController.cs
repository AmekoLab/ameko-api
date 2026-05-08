using FPTU.Capstone.AMKCollective.Api.Controllers;
using FPTU.Capstone.AMKCollective.Application.DTOs;
using FPTU.Capstone.AMKCollective.Application.DTOs.Builder;
using FPTU.Capstone.AMKCollective.Application.DTOs.Common;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using FPTU.Capstone.AMKCollective.Application.Services;
using Microsoft.AspNetCore.Authorization;
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
        [Authorize] // [Fix #1] Builder session chỉ tạo được khi đã đăng nhập
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
        [Authorize] // [Fix #1] Chỉ user đã login mới được chọn linh kiện
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
                var userId = GetCurrentUserId();
                var result = await _service.SelectPartAsync(request, userId);
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
        [Authorize] // [Fix #1] Chỉ Admin/Shop mới cần xem config — yêu cầu đăng nhập tối thiểu
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
        /// Update an existing Option (Update Rule, Tags, or Image).
        /// </summary>
        [HttpPut("options/{id}")]
        [Authorize(Roles = "Admin,Shop")]
        [SwaggerOperation(Summary = "Update Kit Option", Description = "Updates the filter rules or image of an existing kit option.")]
        [SwaggerResponse(200, "Option updated successfully")]
        [SwaggerResponse(404, "Option not found")]
        public async Task<IActionResult> UpdateOption(Guid id, [FromForm] UpdateKitOptionRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var isAdmin = User.IsInRole("Admin");
                await _service.UpdateOptionAsync(id, request, userId, isAdmin);
                return SuccessResponse("Update option successful");
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundResponse<string>(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating builder option {Id}", id);
                return ServerErrorResponse<string>("An error occurred while updating option.");
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
        [Authorize(Roles = "Admin,Shop")]
        [SwaggerOperation(
    Summary = "Create Kit Option",
    Description = "Define a new selectable option slot for a keyboard kit."
)]
        [SwaggerResponse(200, "Option created successfully")]
        public async Task<IActionResult> CreateOption([FromForm] CreateKitOptionRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var isAdmin = User.IsInRole("Admin");
                await _service.CreateOptionAsync(request, userId, isAdmin);
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
        [Authorize(Roles = "Admin,Shop")]
        [SwaggerOperation(
    Summary = "Delete Kit Option",
    Description = "Removes a configuration option from a kit."
)]
        [SwaggerResponse(200, "Option deleted successfully")]
        public async Task<IActionResult> DeleteOption(Guid id)
        {
            try
            {
                var userId = GetCurrentUserId();
                var isAdmin = User.IsInRole("Admin");
                await _service.DeleteOptionAsync(id, userId, isAdmin);
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
        [Authorize(Roles = "Admin,Shop")]
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

                var userId = GetCurrentUserId();
                var isAdmin = User.IsInRole("Admin");
                await _service.BulkCreateOptionsAsync(requests, userId, isAdmin);

                return SuccessResponse($"Bulk import successful: {requests.Count} items.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error bulk creating options");
                return ServerErrorResponse<string>("An error occurred while bulk creating options.");
            }
        }

        /// <summary>
        /// Batch save options for a Kit (replace-all strategy).
        /// </summary>
        /// <remarks>
        /// Replaces ALL existing rules of the kit with the new list in a single transaction.
        /// <br/>
        /// <b>Logic:</b>
        /// 1. Validate all items — StepName must not be empty.
        /// 2. Extract BaseKitId from the first item.
        /// 3. Inside a transaction: delete old rules → insert new rules.
        /// <br/>
        /// <b>Note:</b> This endpoint accepts JSON only. Use <c>ExistingLayerUrl</c> to pass image URLs.
        /// File uploads are not supported here — use <c>POST /options</c> for individual options with images.
        /// </remarks>
        [HttpPost("options/batch")]
        [Authorize(Roles = "Admin,Shop")]
        [SwaggerOperation(
            Summary = "Batch Save Kit Options",
            Description = "Atomically replaces all existing options of a Base Kit with the provided list. Runs inside a transaction.")]
        [SwaggerResponse(200, "Batch save successful")]
        [SwaggerResponse(400, "Payload is empty or contains invalid StepName")]
        [SwaggerResponse(500, "Transaction failed")]
        public async Task<IActionResult> BatchSaveOptions([FromBody] List<BatchKitOptionItem> items)
        {
            try
            {
                if (items == null || items.Count == 0)
                    return ErrorResponse<string>("Payload is empty.");

                var userId = GetCurrentUserId();
                var isAdmin = User.IsInRole("Admin");
                await _service.BatchSaveOptionsAsync(items, userId, isAdmin);
                return SuccessResponse($"Batch save successful: {items.Count} options.");
            }
            catch (ArgumentException ex)
            {
                return ErrorResponse<string>(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error batch saving options for BaseKitId {BaseKitId}", items?.FirstOrDefault()?.BaseKitId);
                return ServerErrorResponse<string>("An error occurred while saving options.");
            }
        }

        /// <summary>
        /// Upload a layer image for a builder option. Returns the URL to use in batch save.
        /// </summary>
        /// <remarks>
        /// Dùng trước khi gọi <c>POST /options/batch</c>.
        /// FE upload từng ảnh song song → nhận URL → đưa vào <c>ExistingLayerUrl</c> khi batch save.
        /// </remarks>
        [HttpPost("options/upload-layer")]
        [Authorize]
        [SwaggerOperation(
            Summary = "Upload Layer Image",
            Description = "Uploads a layer image to storage and returns the URL. Use this before calling batch save.")]
        [SwaggerResponse(200, "Upload successful", typeof(ApiResponse<object>))]
        [SwaggerResponse(400, "No file provided")]
        public async Task<IActionResult> UploadLayerImage([FromForm] UploadLayerImageRequest request)
        {
            try
            {
                var file = request.File;
                if (file == null || file.Length == 0)
                    return ErrorResponse<string>("No file provided.");

                var url = await _service.UploadLayerImageAsync(file);

                return SuccessResponse(new { url }, "Upload successful.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading layer image");
                return ServerErrorResponse<string>("An error occurred while uploading the image.");
            }
        }

        /// <summary>
        ///  Reset the entire configuration of a Kit.
        /// </summary>
        /// <remarks>
        /// <b>Warning:</b> This deletes all links/options for the specified Kit. Use with caution.
        /// </remarks>
        [HttpDelete("config/{baseKitId}")]
        [Authorize(Roles = "Admin,Shop")]
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
                var userId = GetCurrentUserId();
                var isAdmin = User.IsInRole("Admin");
                await _service.ResetBuilderConfigAsync(baseKitId, userId, isAdmin);
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
        [Authorize] // [Fix #1] Chỉ user đã login mới được resume session
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
                var userId = GetCurrentUserId();
                var result = await _service.GetExistingSessionAsync(sessionId, stepName, userId);
                return SuccessResponse(result);
            }
            catch (KeyNotFoundException)
            {
                return NotFoundResponse<string>("Session expired or not found. Please start over.");
            }
        }

        /// <summary>
        /// Remove a selected part from the session.
        /// </summary>
        /// <remarks>
        /// Removes the item at the specified step and clears all subsequent steps to maintain compatibility.
        /// Also reverts the preview image and recalculates the price.
        /// </remarks>
        [HttpDelete("session/{sessionId}/part/{stepName}")]
        [Authorize] // [Fix #1] Chỉ user đã login mới được xóa part khỏi session
        [SwaggerOperation(Summary = "Remove Part from Session")]
        [SwaggerResponse(200, "Part removed successfully", typeof(ApiResponse<BuilderStepResponse>))]
        [SwaggerResponse(404, "Session not found")]
        public async Task<IActionResult> RemovePart(Guid sessionId, string stepName)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _service.RemovePartFromSessionAsync(sessionId, stepName, userId);
                return SuccessResponse(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundResponse<string>(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing part {Step} from Session {SessionId}", stepName, sessionId);
                return ServerErrorResponse<string>("An error occurred while removing the part.");
            }
        }

        /// <summary>
        /// [USER] Get list of active builder sessions.
        /// </summary>
        /// <remarks>
        /// Returns a history of unfinished builds so the user can choose to resume one.
        /// </remarks>
        [HttpGet("sessions")]
        [Authorize] // [Fix #1] Chỉ user đã login mới xem được danh sách session của mình
        [SwaggerOperation(Summary = "Get User Sessions")]
        [SwaggerResponse(200, "List retrieved successfully", typeof(ApiResponse<List<BuilderSessionSummaryResponse>>))]
        public async Task<IActionResult> GetUserSessions()
        {
            try
            {
                var userId = GetCurrentUserId(); 
                var result = await _service.GetUserSessionsAsync(userId);
                return SuccessResponse(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting sessions for user");
                return ServerErrorResponse<string>("An error occurred while retrieving sessions.");
            }
        }


        /// <summary>
        /// [USER] Re-creates a Builder Session from an existing Order Item.
        /// </summary>
        /// <remarks>
        /// This endpoint is used when a user wants to "Edit" a custom keyboard configuration in their Cart 
        /// or "Re-order" a previously purchased configuration from Order History.
        /// <br/>
        /// <b>Logic:</b>
        /// 1. Validates that the Order Item exists and belongs to the current user.
        /// 2. Retrieves all components (Case, Switch, Keycap, etc.) from the Order Item snapshot.
        /// 3. Creates a NEW active Builder Session initialized with these components.
        /// 4. Returns the <c>sessionId</c>. Frontend should redirect the user to <c>/builder?sessionId={newSessionId}</c>.
        /// </remarks>
        /// <param name="orderItemId">The unique identifier of the Order Item (Cart Item) to be edited or copied.</param>
        /// <returns>An object containing the new <c>sessionId</c>.</returns>
        [HttpPost("from-order/{orderItemId}")]
        [Authorize]
        [SwaggerOperation(
            Summary = "Create Session from Order Item",
            Description = "Generates a new builder session based on the configuration of an existing order item. Useful for editing cart items or re-ordering."
        )]
        [SwaggerResponse(200, "Session created successfully", typeof(ApiResponse<object>))]
        [SwaggerResponse(401, "Unauthorized (User not logged in or invalid token)")]
        [SwaggerResponse(404, "Order item not found or does not belong to user")]
        [SwaggerResponse(500, "Internal server error")]
        public async Task<IActionResult> CreateSessionFromOrder(Guid orderItemId)
        {
            try
            {
                // 1. Get current User ID from Token (Method inherited from BaseApiController)
                var userId = GetCurrentUserId();

                // 2. Call Service to create new session
                var newSessionId = await _service.CreateSessionFromOrderAsync(orderItemId, userId);

                // 3. Return Success Response with Data
                return SuccessResponse(new { sessionId = newSessionId }, "Session re-created successfully.");
            }
            catch (KeyNotFoundException ex)
            {
                // Returns 404 if order item is not found
                return NotFoundResponse<string>(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                // Returns 401 if user tries to access someone else's order item
                return UnauthorizedResponse<string>(ex.Message);
            }
            catch (Exception ex)
            {
                // Log the error and return 500
                _logger.LogError(ex, "Error re-creating session from OrderItem {Id}", orderItemId);
                return ServerErrorResponse<string>("An error occurred while re-creating the session.");
            }
        }

        /// <summary>
        /// Converts an existing Builder Session into a Custom Commission Request directed to the Shop.
        /// This is used when a customer requires advanced customization (e.g., mix switches, lube, modding) 
        /// that goes beyond standard builder options.
        /// </summary>
        /// <param name="request">Contains the SessionId and the customer's special notes.</param>
        /// <returns>Returns the ID of the newly created Commission Request if successful.</returns>
        /// <response code="200">Successfully converted the session to a commission request.</response>
        /// <response code="400">If the session is invalid, empty, or missing a base kit.</response>
        [Authorize(Roles = "Customer")]
        [HttpPost("to-commission")]
        [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ConvertToCommission([FromBody] BuilderToCommissionRequest request)
        {
            var userId = GetCurrentUserId();

            var result = await _service.ConvertSessionToCommissionAsync(userId, request);

            if (!result.Success)
                return ErrorResponse<string>(result.ErrorMessage); // [Fix #5] Dùng chuẩn BaseApiController

            return SuccessResponse(result.CommissionRequestId!.Value, "Your custom request has been successfully submitted to the shop for quotation."); // [Fix #5]
        }

        /// <summary>
        /// [USER] Thêm linh kiện mua lẻ (Add-on) vào Bàn phím ảo.
        /// </summary>
        /// <remarks>
        /// FE gọi API này khi khách hàng click chọn 1 phím lẻ (hoặc 1 cụm phím) trên Bàn phím ảo để đổi Switch/Keycap.
        /// Việc thêm này không làm ảnh hưởng đến luồng chọn Case -> Plate gốc, mà chỉ cộng thêm tiền và ghi chú vị trí.
        /// </remarks>
        /// <param name="sessionId">ID của phiên build hiện tại</param>
        /// <param name="request">Chứa ComponentId (ID của phím lẻ), Số lượng, và Vị trí (VD: WASD)</param>
        [HttpPost("session/{sessionId}/add-addon")]
        [Authorize]
        [SwaggerOperation(
            Summary = "Add Custom Individual Part (Add-on)",
            Description = "Adds specific parts like single switches or artisan keycaps to a builder session based on virtual keyboard interactions."
        )]
        [SwaggerResponse(200, "Add-on added successfully", typeof(ApiResponse<BuilderStepResponse>))]
        [SwaggerResponse(400, "Not enough stock or invalid data")]
        [SwaggerResponse(404, "Session or Component not found")]
        public async Task<IActionResult> AddExtraPart(Guid sessionId, [FromBody] BuilderAddonRequest request)
        {
            try
            {
                // Đảm bảo SessionId trong đường dẫn (route) khớp với body gửi lên
                request.SessionId = sessionId;
                var userId = GetCurrentUserId();
                var result = await _service.AddExtraPartToSessionAsync(request, userId);
                return SuccessResponse(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundResponse<string>(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                // Bắt lỗi không đủ số lượng tồn kho
                return ErrorResponse<string>(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding extra part for Session {SessionId}", sessionId);
                return ServerErrorResponse<string>("An error occurred while adding the extra part.");
            }
        }

        /// <summary>
        /// [USER] Xóa một linh kiện lẻ (Add-on) khỏi Bàn phím ảo.
        /// </summary>
        /// <remarks>
        /// FE gọi API này khi khách hàng muốn Hủy custom ở cái nút đó (trả về switch base mặc định).
        /// </remarks>
        [HttpDelete("session/{sessionId}/remove-addon/{addonKey}")]
        [Authorize]
        [SwaggerOperation(Summary = "Remove Custom Individual Part (Add-on)")]
        [SwaggerResponse(200, "Add-on removed successfully", typeof(ApiResponse<BuilderStepResponse>))]
        [SwaggerResponse(404, "Session not found")]
        public async Task<IActionResult> RemoveExtraPart(Guid sessionId, string addonKey)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _service.RemoveExtraPartFromSessionAsync(sessionId, addonKey, userId);
                return SuccessResponse(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundResponse<string>(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing extra part {AddonKey} from Session {SessionId}", addonKey, sessionId);
                return ServerErrorResponse<string>("An error occurred while removing the extra part.");
            }
        }
        /// <summary>
        /// [USER] Lấy danh sách linh kiện có thể add-on khi click vào phím trên bàn phím ảo.
        /// </summary>
        /// <remarks>
        /// Tất cả addon-eligible parts đều được query từ một nguồn duy nhất: <c>Model</c> với <c>IsAddonEligible = true</c>.
        /// Shop tự đánh dấu khi tạo sản phẩm (artisan keycap, switch đặc biệt, v.v.).
        /// <br/>
        /// <b>addonType</b> là optional PartType filter — không phải routing.
        /// Không truyền → trả toàn bộ addon-eligible parts của shop.
        /// "switch" → chỉ lấy IsAddonEligible parts có PartType = "switch".
        /// "keycap" → chỉ lấy IsAddonEligible parts có PartType = "keycap".
        /// </remarks>
        /// <param name="sessionId">ID của builder session hiện tại</param>
        /// <param name="addonType">Optional PartType filter: "switch", "keycap", "all" hoặc để trống</param>
        /// <param name="searchTerm">Tìm kiếm theo tên (optional)</param>
        /// <param name="page">Trang (default = 1)</param>
        /// <param name="pageSize">Số item mỗi trang (default = 20)</param>
        [HttpGet("session/{sessionId}/addon-options")]
        [Authorize]
        [SwaggerOperation(
            Summary = "Get Addon Options",
            Description = "Returns all parts marked IsAddonEligible=true from the session's shop. " +
                          "addonType is an optional PartType filter (switch/keycap/all), not a routing mechanism. " +
                          "All results come from the same Model table — no KitDesignOption routing needed.")]
        [SwaggerResponse(200, "Options retrieved", typeof(ApiResponse<AddonOptionsResponse>))]
        [SwaggerResponse(404, "Session not found")]
        public async Task<IActionResult> GetAddonOptions(
            Guid sessionId,
            [FromQuery] string addonType = "all",
            [FromQuery] string? searchTerm = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(addonType))
                    return ErrorResponse<string>("addonType is required. Supported values: 'switch', 'keycap'.");
                var userId = GetCurrentUserId();
                var result = await _service.GetAddonOptionsAsync(sessionId, addonType, userId, searchTerm, page, pageSize);
                return SuccessResponse(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundResponse<string>(ex.Message);
            }
            catch (ArgumentException ex)
            {
                return ErrorResponse<string>(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting addon options for Session {SessionId}, Type {AddonType}", sessionId, addonType);
                return ServerErrorResponse<string>("An error occurred while retrieving addon options.");
            }
        }
    }
}