using FPTU.Capstone.AMKCollective.Api.Controllers;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FPTU.Capstone.AMKCollective.API.Controllers
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class ReputationController : BaseApiController
    {
        private readonly IQualityScoreService _qualityScoreService;
        private readonly IShopService _shopService;

        public ReputationController(IQualityScoreService qualityScoreService, IShopService shopService)
        {
            _qualityScoreService = qualityScoreService;
            _shopService = shopService;
        }

        // USER
        [HttpGet("{shopId}/current")]
        [AllowAnonymous]
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


        //SHOP
        [HttpGet("my-breakdown")]
        [Authorize]
        public async Task<IActionResult> GetMyReputationBreakdown()
        {
            try
            {
                var userId = GetCurrentUserId();
                var shop = await _shopService.GetMyShopAsync(userId);
                if (shop == null) return UnauthorizedResponse<object>("User does not own any shop.");
                var data = await _qualityScoreService.GetReputationBreakdownAsync(shop.Id);
                if (data == null) return SuccessResponse<object>(null, "No reputation data available yet.");

                return SuccessResponse(data, "Fetched reputation breakdown successfully.");
            }
            catch (Exception ex)
            {
                return ServerErrorResponse<object>(ex.Message);
            }
        }

        [HttpGet("my-trend")]
        [Authorize]
        public async Task<IActionResult> GetMyReputationTrend()
        {
            try
            {
                var userId = GetCurrentUserId();
                var shop = await _shopService.GetMyShopAsync(userId);
                if (shop == null) return UnauthorizedResponse<object>("User does not own any shop.");
                var data = await _qualityScoreService.GetReputationTrendAsync(shop.Id);
                return SuccessResponse(data, "Fetched reputation trend successfully.");
            }
            catch (Exception ex)
            {
                return ServerErrorResponse<object>(ex.Message);
            }
        }

        [HttpGet("my-badge-history")]
        [Authorize]
        public async Task<IActionResult> GetMyBadgeHistory()
        {
            try
            {
                var userId = GetCurrentUserId();
                var shop = await _shopService.GetMyShopAsync(userId);
                if (shop == null) return UnauthorizedResponse<object>("User does not own any shop.");
                var data = await _qualityScoreService.GetBadgeHistoryAsync(shop.Id);
                return SuccessResponse(data, "Fetched badge history successfully.");
            }
            catch (Exception ex)
            {
                return ServerErrorResponse<object>(ex.Message);
            }
        }
    }
}