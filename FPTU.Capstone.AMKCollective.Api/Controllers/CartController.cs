using FPTU.Capstone.AMKCollective.Api.Controllers;
using FPTU.Capstone.AMKCollective.Application.DTOs;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FPTU.Capstone.AMKCollective.API.Controllers
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class CartController : BaseApiController
    {
        private readonly ICartService _cartService;
        private readonly ILogger<CartController> _logger;

        public CartController(
            ICartService cartService,
            ILogger<CartController> logger)
        {
            _cartService = cartService;
            _logger = logger;
        }

        // GET: api/cart
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<CartDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetCart(CancellationToken token)
        {
            try
            {
                //TODO: wait for auth
                var userId = GetUserId();
                var cart = await _cartService.GetCartAsync(userId, token);
                return SuccessResponse(cart);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching cart");
                return ServerErrorResponse<string>("An error occurred while fetching cart");
            }
        }

        // POST: api/cart
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> AddToCart([FromBody] AddToCartDto request, CancellationToken token)
        {
            try
            {
                var userId = GetUserId(); 
                await _cartService.AddToCartAsync(userId, request, token);

                return SuccessResponse("Item added to cart successfully");
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundResponse<string>(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding item to cart");
                return ServerErrorResponse<string>("An error occurred while adding item to cart");
            }
        }

        // DELETE: api/cart/{itemId}
        [HttpDelete("{itemId}")]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status403Forbidden)] 
        public async Task<IActionResult> RemoveItem(Guid itemId, CancellationToken token)
        {
            try
            {
                var userId = GetUserId();
                await _cartService.RemoveItemAsync(userId, itemId, token);

                return SuccessResponse("Item removed from cart successfully");
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundResponse<string>(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return ErrorResponse<string>(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing item from cart");
                return ServerErrorResponse<string>("An error occurred while removing item");
            }
        }

        // PATCH: api/cart/{itemId}
        [HttpPatch("{itemId}")]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
        public async Task<IActionResult> UpdateQuantity(Guid itemId, [FromBody] int quantity, CancellationToken token)
        {
            try
            {
                var userId = GetUserId();
                await _cartService.UpdateQuantityAsync(userId, itemId, quantity, token);

                return SuccessResponse("Cart updated successfully");
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundResponse<string>(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating cart quantity");
                return ServerErrorResponse<string>("An error occurred while updating cart");
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
    