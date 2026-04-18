using FPTU.Capstone.AMKCollective.Api.Controllers;
using FPTU.Capstone.AMKCollective.Application.DTOs.Reputation;
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
        private readonly IReputationService _reputationService;

        public ReputationController(IQualityScoreService qualityScoreService, IShopService shopService, IReputationService reputationService)
        {
            _qualityScoreService = qualityScoreService;
            _shopService = shopService;
            _reputationService = reputationService;
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
        [HttpGet("my-current")]
        [Authorize]
        public async Task<IActionResult> GetMyCurrentReputation()
        {
            try
            {
                var userId = GetCurrentUserId();
                var shop = await _shopService.GetMyShopAsync(userId);
                if (shop == null) return UnauthorizedResponse<object>("User does not own any shop.");

                var data = await _qualityScoreService.GetCurrentReputationAsync(shop.Id);
                if (data == null) return NotFoundResponse<object>("Shop not found.");

                return SuccessResponse(data, "Fetched current reputation successfully.");
            }
            catch (Exception ex)
            {
                return ServerErrorResponse<object>(ex.Message);
            }
        }

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

        // CUSTOMER
        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> GetMyReputation()
        {
            try
            {
                var userId = GetCurrentUserId();
                var data = await _reputationService.GetUserReputationAsync(userId);
                return SuccessResponse(data, "Fetched user reputation successfully.");
            }
            catch (Exception ex)
            {
                return ServerErrorResponse<object>(ex.Message);
            }
        }

        [HttpGet("me/logs")]
        [Authorize]
        public async Task<IActionResult> GetMyReputationLogs([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
        {
            try
            {
                var userId = GetCurrentUserId();
                var data = await _reputationService.GetUserReputationLogsAsync(userId, pageNumber, pageSize);
                return SuccessResponse(data, "Fetched reputation logs successfully.");
            }
            catch (Exception ex)
            {
                return ServerErrorResponse<object>(ex.Message);
            }
        }

        [HttpGet("my-shop/logs")]
        [Authorize]
        public async Task<IActionResult> GetMyShopReputationLogs([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
        {
            try
            {
                var userId = GetCurrentUserId();
                var shop = await _shopService.GetMyShopAsync(userId);
                if (shop == null) return UnauthorizedResponse<object>("User does not own any shop.");

                var data = await _reputationService.GetShopReputationLogsAsync(shop.Id, pageNumber, pageSize);
                return SuccessResponse(data, "Fetched shop reputation logs successfully.");
            }
            catch (Exception ex)
            {
                return ServerErrorResponse<object>(ex.Message);
            }
        }

        // ADMIN
        [HttpPost("adjust")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AdjustReputationByAdmin([FromBody] AdminAdjustReputationDto request)
        {
            var newScore = await _reputationService.AdjustReputationAsync(
                request.TargetType,
                request.TargetId,
                request.Delta,
                request.Reason,
                "AdminAdjustment",
                GetCurrentUserId().ToString()
                );

            return SuccessResponse(new { NewScore = newScore }, "Reputation adjusted successfully.");
        }
    }
}