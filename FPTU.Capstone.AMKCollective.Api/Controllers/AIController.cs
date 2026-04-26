using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FPTU.Capstone.AMKCollective.Application.Contracts.AI;
using FPTU.Capstone.AMKCollective.Application.Interfaces.AI;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Api.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class AIController : BaseApiController
    {
        private readonly IAIService _aiService;

        public AIController(IAIService aiService)
        {
            _aiService = aiService;
        }

        /// <summary>
        /// Get a personalized keyboard build recommendation.
        /// </summary>
        [HttpPost("recommend")]
        public async Task<IActionResult> GetRecommendation([FromBody] AIRecommendationRequestDTO request)
        {
            var result = await _aiService.GetRecommendationAsync(request);
            return Ok(result);
        }

        /// <summary>
        /// Semantic search for shops.
        /// </summary>
        [HttpGet("search-shops")]
        public async Task<IActionResult> SearchShops([FromQuery] string query, [FromQuery] int limit = 10)
        {
            if (string.IsNullOrWhiteSpace(query)) return BadRequest("Query is required.");
            var result = await _aiService.SearchShopsAsync(query, limit);
            return Ok(result);
        }

        /// <summary>
        /// Semantic search for assembled products (builds).
        /// </summary>
        [HttpGet("search-builds")]
        public async Task<IActionResult> SearchBuilds([FromQuery] string query, [FromQuery] int limit = 10)
        {
            if (string.IsNullOrWhiteSpace(query)) return BadRequest("Query is required.");
            var result = await _aiService.SearchBuildsAsync(query, limit);
            return Ok(result);
        }

        /// <summary>
        /// Manually trigger a full sync of entities to Qdrant (Admin only).
        /// </summary>
        [HttpPost("sync-all")]
        // [Authorize(Roles = "Admin")] // Uncomment when ready
        public async Task<IActionResult> SyncAll()
        {
            await _aiService.SyncAllEntitiesToQdrantAsync();
            return Ok(new { message = "Sync process started." });
        }

        // ── AI Chatbot endpoints ─────────────────────────────────────────

        /// <summary>
        /// Gửi tin nhắn tới AI chatbot tư vấn bàn phím.
        /// ConversationId = null → tạo conversation mới.
        /// ConversationId có giá trị → tiếp tục hội thoại cũ (AI nhớ lịch sử).
        /// </summary>
        [Authorize]
        [HttpPost("chat")]
        public async Task<IActionResult> Chat([FromBody] AIChatRequestDTO request)
        {
            if (string.IsNullOrWhiteSpace(request.Message))
                return ErrorResponse<object>("Message is required.");

            try
            {
                var userId = GetCurrentUserId();
                var result = await _aiService.ChatAsync(userId, request);
                return SuccessResponse(result);
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
        /// Danh sách các phiên hội thoại AI của user hiện tại, mới nhất trước.
        /// </summary>
        [Authorize]
        [HttpGet("chat/conversations")]
        public async Task<IActionResult> GetChatConversations([FromQuery] int limit = 20)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _aiService.GetChatConversationsAsync(userId, limit);
                return SuccessResponse(result);
            }
            catch (Exception ex)
            {
                return ServerErrorResponse<object>(ex.Message);
            }
        }

        /// <summary>
        /// Toàn bộ tin nhắn trong một phiên hội thoại AI theo thứ tự thời gian.
        /// Assistant messages kèm Items (sản phẩm đề xuất) nếu có.
        /// </summary>
        [Authorize]
        [HttpGet("chat/conversations/{conversationId:int}/messages")]
        public async Task<IActionResult> GetChatMessages(int conversationId)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _aiService.GetChatMessagesAsync(userId, conversationId);
                return SuccessResponse(result);
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
