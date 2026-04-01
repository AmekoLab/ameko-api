using FPTU.Capstone.AMKCollective.Api.Controllers;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FPTU.Capstone.AMKCollective.API.Controllers
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class ReputationController : BaseApiController
    {
        private readonly IQualityScoreService _qualityScoreService;

        public ReputationController(IQualityScoreService qualityScoreService)
        {
            _qualityScoreService = qualityScoreService;
        }

        [HttpGet("current")]
        public async Task<IActionResult> GetCurrentReputation(Guid shopId)
        {
            try
            {
                var data = await _qualityScoreService.GetCurrentReputationAsync(shopId);
                if (data == null) return NotFoundResponse<object>("Shop not found.");

                return SuccessResponse(data, "Fetched current reputation successfully.");
            }
            catch (Exception ex)
            {
                return ServerErrorResponse<object>(ex.Message);
            }
        }

        [HttpGet("breakdown")]
        public async Task<IActionResult> GetReputationBreakdown(Guid shopId)
        {
            try
            {
                var data = await _qualityScoreService.GetReputationBreakdownAsync(shopId);
                if (data == null) return SuccessResponse<object>(null, "No reputation data available yet.");

                return SuccessResponse(data, "Fetched reputation breakdown successfully.");
            }
            catch (Exception ex)
            {
                return ServerErrorResponse<object>(ex.Message);
            }
        }

        [HttpGet("trend")]
        public async Task<IActionResult> GetReputationTrend(Guid shopId)
        {
            try
            {
                var data = await _qualityScoreService.GetReputationTrendAsync(shopId);
                return SuccessResponse(data, "Fetched reputation trend successfully.");
            }
            catch (Exception ex)
            {
                return ServerErrorResponse<object>(ex.Message);
            }
        }

        [HttpGet("badge-history")]
        public async Task<IActionResult> GetBadgeHistory(Guid shopId)
        {
            try
            {
                var data = await _qualityScoreService.GetBadgeHistoryAsync(shopId);
                return SuccessResponse(data, "Fetched badge history successfully.");
            }
            catch (Exception ex)
            {
                return ServerErrorResponse<object>(ex.Message);
            }
        }
    }
}
