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
    }
}
