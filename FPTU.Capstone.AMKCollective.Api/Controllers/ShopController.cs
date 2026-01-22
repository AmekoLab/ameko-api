using FPTU.Capstone.AMKCollective.Api.Controllers;
using FPTU.Capstone.AMKCollective.API.Contracts;
using FPTU.Capstone.AMKCollective.Application.DTOs;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using FPTU.Capstone.AMKCollective.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FPTU.Capstone.AMKCollective.API.Controllers
{
    [Route("api/v1/shops")]
    [ApiController]
    public class ShopController : BaseApiController
    {
        private readonly IShopService _shopService;
        private readonly ILogger<ShopController> _logger;

        public ShopController(IShopService shopService, ILogger<ShopController> logger)
        {
            _shopService = shopService;
            _logger = logger;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetMarketplaceShops([FromQuery] string? searchTerm, [FromQuery] int page =1, [FromQuery] int size = 10)
        {
            try
            {
                var (items, total) = await _shopService.GetMarketplaceShopAsync(searchTerm, page, size);
                var response = new
                {
                    items, pagination = new { page, size, totalCount = total }
                };
                return SuccessResponse(response);

            }catch(Exception ex)
            {
                _logger.LogError(ex, "Error getting marketplace shops");
                return ServerErrorResponse<string>("Error getting shops");
            }
        }

        [HttpGet("{id:guid}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetShopPublicProfile(Guid id)
        {
            try
            {
                var shop = await _shopService.GetShopPublicProfileAsync(id);
                return SuccessResponse(shop);
            }catch (KeyNotFoundException ex)
            {
                return NotFoundResponse<string>(ex.Message);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Error getting shop public profile {Id}", id);
                return ServerErrorResponse<string>("Error getting shop profile");
            }
        }

        [HttpGet("my-shop")]
        //[Authorize]
        public async Task<IActionResult> GetMyShop()
        {
            try
            {
                var userId = Guid.Parse("11111111-1111-1111-1111-111111111111");
                var shop = await _shopService.GetMyShopAsync(userId);
                return SuccessResponse(shop);
            }
            catch (KeyNotFoundException)
            {
                return NotFoundResponse<string>("Your shop not found");
            }catch(Exception ex)
            {
                _logger.LogError(ex, "Error getting my shop");
                return ServerErrorResponse<string>("Error getting shop");
            }
        }

        [HttpPost("register")]
        //[Authorize]
        public async Task<IActionResult> RegisterShop([FromForm] CreateShopApiRequest apiRequest)
        {
            try
            {
                var userId = Guid.Parse("11111111-1111-1111-1111-111111111111");

                var appRequest = new CreateShopRequest
                {
                    ShopName = apiRequest.ShopName,
                    Bio = apiRequest.Bio,
                    Address = apiRequest.Address,
                    PhoneNumber = apiRequest.PhoneNumber,
                    ContactEmail = apiRequest.ContactEmail,
                    CitizenId = apiRequest.CitizenId,
                    TaxCode = apiRequest.TaxCode,
                    BankName = apiRequest.BankName,
                    BankAccountNumber = apiRequest.BankAccountNumber,
                    BankAccountName = apiRequest.BankAccountName,

                    LogoStream = apiRequest.LogoImage?.OpenReadStream(),
                    LogoFileName = apiRequest.LogoImage?.FileName,
                    BannerStream = apiRequest.BannerImage?.OpenReadStream(),
                    BannerFileName = apiRequest.BannerImage?.FileName
                };
                var result = await _shopService.RegisterShopAsync(userId, appRequest);
                return SuccessResponse(result, "Submit successfully, waiting for approve.");
            }catch (InvalidOperationException ex)
            {
                return ErrorResponse<string>(ex.Message);
            }catch (ArgumentException ex)
            {
                return ErrorResponse<string>(ex.Message);
            }catch(Exception ex)
            {
                _logger.LogError(ex, "Error registering shop");
                return ServerErrorResponse<string>("System error during shop registration ");
            }
        }

        [HttpPut("profile")]
        //[Authorize]
        public async Task<IActionResult> UpdateMyShop([FromForm] UpdateShopApiRequest apiRequest)
        {
            try
            {
                var userId = Guid.Parse("11111111-1111-1111-1111-111111111111");

                //Map API DTO -> App DTO
                var appRequest = new UpdateShopProfileRequest
                {
                    Bio = apiRequest.Bio,
                    Address = apiRequest.Address,
                    PhoneNumber = apiRequest.PhoneNumber,
                    ContactEmail = apiRequest.ContactEmail,
                    IsActive = apiRequest.IsActive,
                    BankName = apiRequest.BankName,
                    BankAccountNumber = apiRequest.BankAccountNumber,
                    BankAccountName = apiRequest.BankAccountName,

                    // image
                    LogoStream = apiRequest.LogoImage?.OpenReadStream(),
                    LogoFileName = apiRequest.LogoImage?.FileName,
                    BannerStream = apiRequest.BannerImage?.OpenReadStream(),
                    BannerFileName = apiRequest.BannerImage?.FileName
                };

                await _shopService.UpdateMyShopAsync(userId, appRequest);
                return SuccessResponse("Update shop profile successfully");
            }catch(KeyNotFoundException ex)
            {
                return NotFoundResponse<string>(ex.Message);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Error updating shop profile");
                return ServerErrorResponse<string>("Error during update shop profile");
            }
        }

        [HttpGet("admin/list")]
       // [Authorize(Roles ="Admin")]
       public async Task<IActionResult> GetShopForAdmin([FromQuery] string? searchTerm, 
          [FromQuery] ShopStatus? status,
          [FromQuery] int page = 1,
          [FromQuery] int size = 20)
        {
            try
            {
                var (items, total) = await _shopService.GetShopForAdminAsync(searchTerm, status, page, size);
                var response = new
                {
                    items,
                    pagination = new { page, size, totalCount = total }
                };
                return SuccessResponse(response);
            }catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting admin shop list");
                return ServerErrorResponse<string>("system error");
            }
        }

        [HttpPost("admin/{id:guid}/approve")]
        //[Authorize(Roles="Admin")]
        public async Task<IActionResult> ApproveShop(Guid id, [FromBody] ApproveShopRequest request)
        {
            try
            {
                await _shopService.ApproveShopAsync(id, request);
                return SuccessResponse($"Updated shop status: {request.Status}"); ;
            }catch (KeyNotFoundException ex)
            {
                return NotFoundResponse<string>(ex.Message);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Error approving shop {Id}", id);
                return ServerErrorResponse<string>("Error during approve shop");
            }
        }

    }
}
