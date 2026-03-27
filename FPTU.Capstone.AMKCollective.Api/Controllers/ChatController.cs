using FPTU.Capstone.AMKCollective.Application.DTOs.Chat;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace FPTU.Capstone.AMKCollective.Api.Controllers
{
    /// <summary>
    /// APIs for 1-1 chat including conversation listing, message sending, read receipts, and reactions.
    /// </summary>
    [ApiController]
    [Route("api/v1/chat")]
    [Authorize]
    public class ChatController : BaseApiController
    {
        private readonly IChatService _chatService;

        public ChatController(IChatService chatService)
        {
            _chatService = chatService;
        }

        /// <summary>
        /// Get current user's conversation list with cursor pagination.
        /// </summary>
        /// <param name="cursor">Cursor returned from previous page.</param>
        /// <param name="pageSize">Number of items to return.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Paged conversation list with unread and last-message metadata.</returns>
        [SwaggerOperation(Summary = "Get conversation list", Description = "Returns current user's 1-1 conversations with cursor pagination.")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [HttpGet("conversations")]
        public async Task<IActionResult> GetConversations([FromQuery] string? cursor, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _chatService.GetUserConversationsAsync(userId, cursor, pageSize, cancellationToken);
                return SuccessResponse(result);
            }
            catch (Exception ex)
            {
                return ServerErrorResponse<object>(ex.Message);
            }
        }

        /// <summary>
        /// Get messages of a conversation with cursor pagination.
        /// </summary>
        /// <param name="conversationId">Conversation id.</param>
        /// <param name="cursor">Cursor returned from previous page.</param>
        /// <param name="pageSize">Number of items to return.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Paged messages in ascending display order.</returns>
        [SwaggerOperation(Summary = "Get conversation messages", Description = "Returns paged messages of a conversation the current user is in.")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [HttpGet("conversations/{conversationId:int}/messages")]
        public async Task<IActionResult> GetMessages(int conversationId, [FromQuery] string? cursor, [FromQuery] int pageSize = 30, CancellationToken cancellationToken = default)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _chatService.GetConversationMessagesAsync(userId, conversationId, cursor, pageSize, cancellationToken);
                return SuccessResponse(result);
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
        /// Create or get a direct conversation between current user and target user.
        /// </summary>
        /// <param name="targetUserId">Target user id.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Conversation metadata for the current-target user pair.</returns>
        [SwaggerOperation(Summary = "Create or get direct conversation", Description = "Creates direct conversation if missing, otherwise returns existing one.")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [HttpPost("conversations/direct/{targetUserId:guid}")]
        public async Task<IActionResult> CreateOrGetDirectConversation(Guid targetUserId, CancellationToken cancellationToken = default)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _chatService.GetOrCreateDirectConversationAsync(userId, targetUserId, cancellationToken);
                return SuccessResponse(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundResponse<object>(ex.Message);
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

        /// <summary>
        /// Send a message in an existing conversation, or create direct conversation via targetUserId.
        /// </summary>
        /// <param name="request">Message payload. Use parentMessageId for reply.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Persisted message payload.</returns>
        [SwaggerOperation(Summary = "Send message", Description = "Sends normal/reply message. Reply is sent by setting parentMessageId.")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [HttpPost("messages")]
        public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest request, CancellationToken cancellationToken = default)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                return ErrorResponse<object>("Validation failed", errors);
            }

            try
            {
                var userId = GetCurrentUserId();
                var result = await _chatService.SendMessageAsync(userId, request, cancellationToken);
                return SuccessResponse(result, "Message sent successfully.");
            }
            catch (UnauthorizedAccessException ex)
            {
                return UnauthorizedResponse<object>(ex.Message);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundResponse<object>(ex.Message);
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

        /// <summary>
        /// Mark messages as read up to the provided message id.
        /// </summary>
        /// <param name="conversationId">Conversation id.</param>
        /// <param name="request">Read boundary payload.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Read operation result.</returns>
        [SwaggerOperation(Summary = "Mark messages as read", Description = "Marks recipient rows as read up to request.upToMessageId.")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [HttpPost("conversations/{conversationId:int}/read")]
        public async Task<IActionResult> MarkRead(int conversationId, [FromBody] MarkReadRequest request, CancellationToken cancellationToken = default)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                return ErrorResponse<object>("Validation failed", errors);
            }

            try
            {
                var userId = GetCurrentUserId();
                await _chatService.MarkMessagesAsReadAsync(userId, conversationId, request.UpToMessageId, cancellationToken);
                return SuccessResponse(new { ConversationId = conversationId, request.UpToMessageId }, "Messages marked as read.");
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
        /// Set or clear reaction of current user on a message.
        /// </summary>
        /// <param name="conversationId">Conversation id.</param>
        /// <param name="messageId">Message id.</param>
        /// <param name="request">Reaction payload. Use null to unreact.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Updated reaction state.</returns>
        [SwaggerOperation(Summary = "Set message reaction", Description = "Set reaction by enum value, or clear reaction by sending null.")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [HttpPut("conversations/{conversationId:int}/messages/{messageId:int}/reaction")]
        public async Task<IActionResult> SetReaction(int conversationId, int messageId, [FromBody] MessageReactionRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                return ErrorResponse<object>("Request body is required.");
            }

            try
            {
                var userId = GetCurrentUserId();
                var result = await _chatService.SetMessageReactionAsync(userId, conversationId, messageId, request.Reaction, cancellationToken);
                return SuccessResponse(result, "Message reaction updated.");
            }
            catch (UnauthorizedAccessException ex)
            {
                return UnauthorizedResponse<object>(ex.Message);
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
    }
}
