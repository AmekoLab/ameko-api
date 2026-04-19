using FPTU.Capstone.AMKCollective.Api.Controllers;
using FPTU.Capstone.AMKCollective.Application.DTOs;
using FPTU.Capstone.AMKCollective.Application.DTOs.Category;
using FPTU.Capstone.AMKCollective.Application.DTOs.Common;
using FPTU.Capstone.AMKCollective.Application.DTOs.Part;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace FPTU.Capstone.AMKCollective.API.Controllers
{
    // [Fix #1] XÓA [AllowAnonymous] class-level — nó override hết [Authorize] actions bên trong.
    // GET endpoints vẫn public (không có attribute = dùng policy mặc định, hoặc thêm [AllowAnonymous] riêng nếu cần).
    [ApiController]
    [Route("api/v1/catalog/categories")]
    public class CategoriesController : BaseApiController
    {
        private readonly ICategoryService _categoryService;
        private readonly IShopService _shopService;
        private readonly ILogger<CategoriesController> _logger;

        public CategoriesController(
            ICategoryService categoryService,
            ILogger<CategoriesController> logger,
            IShopService shopService)
        {
            _categoryService = categoryService;
            _logger = logger;
            _shopService = shopService;
        }

        // Helper to get current user role and shopId
        //private async Task<(string Role, Guid? ShopId)> GetCurrentUserContextAsync()
        //{
        //    var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "Guest";
        //    Guid? shopId = null;

        //    if (role == "Shop")
        //    {
        //        var userIdString = User.FindFirst("id")?.Value 
        //                           ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        //        if (Guid.TryParse(userIdString, out var userId))
        //        {
        //            try
        //            {
        //                var shop = await _shopService.GetMyShopAsync(userId);
        //                if (shop != null)
        //                {
        //                    shopId = shop.Id;
        //                }
        //            }
        //            catch (Exception ex)
        //            {
        //                _logger.LogWarning(ex, "Could not resolve ShopId for Shop User {UserId}", userId);
        //            }
        //        }
        //    }

        //    return (role, shopId);
        //}

        [HttpGet]
        [AllowAnonymous] // Public — Guest/Customer/Shop đều xem được danh sách category
        [SwaggerOperation(Summary = "Get Categories (Public & Shop Context)")]
        public async Task<IActionResult> GetCategories(
    [FromQuery] GetCategoriesFilterRequest queryParams,
    CancellationToken cancellationToken)
        {
            var userId = GetCurrentUserId();
            var categories = await _categoryService.GetCategoriesAsync(queryParams, userId, cancellationToken);
            return SuccessResponse(categories);
        }

        [HttpGet("{id:guid}")]
        [AllowAnonymous] // Public — ai cũng có thể xem chi tiết category
        [SwaggerOperation(
    Summary = "Get Category by ID",
    Description = "Retrieves details of a specific category, optionally including its sub-categories."
)]
        [SwaggerResponse(200, "Category details retrieved", typeof(ApiResponse<CategoryResponse>))]
        [SwaggerResponse(404, "Category not found")]
        public async Task<IActionResult> GetCategoryById(
            Guid id,
            [FromQuery] bool includeSubCategories = false,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var category = await _categoryService.GetCategoryByIdAsync(id, includeSubCategories, cancellationToken);

                if (category == null)
                {
                    return NotFoundResponse<CategoryResponse>($"Category with id {id} not found");
                }

                return SuccessResponse(category);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while fetching category {CategoryId}", id);
                return ServerErrorResponse<string>("An error occurred while fetching the category");
            }
        }

        [HttpGet("{id:guid}/parts")]
        [AllowAnonymous] // Public — ai cũng có thể xem parts trong category
        [SwaggerOperation(
    Summary = "Get Parts in Category",
    Description = "Returns a paginated list of parts/products belonging to a specific category."
)]
        [SwaggerResponse(200, "Parts retrieved successfully", typeof(ApiResponse<object>))]
        [SwaggerResponse(404, "Category not found")]
        public async Task<IActionResult> GetPartsInCategory(
            Guid id,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] bool? isActive = null,
            [FromQuery] string? partType = null,
            [FromQuery] Guid? shopId = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var queryParams = new GetPartsFilterRequest
                {
                    CategoryId = id,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    IsActive = isActive,
                    PartType = partType,
                    ShopId = shopId
                };

                var (items, totalCount, totalPages) = await _categoryService.GetPartsInCategoryAsync(queryParams, cancellationToken);

                var response = new
                {
                    items,
                    pagination = new
                    {
                        currentPage = pageNumber,
                        pageSize,
                        totalPages,
                        totalCount
                    }
                };
                return SuccessResponse(response);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundResponse<string>(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while fetching parts for category {CategoryId}", id);
                return ServerErrorResponse<string>("An error occurred while fetching parts");
            }
        }

        [HttpPost]
        [Authorize] // [Fix #1] Chỉ Admin hoặc Shop đăng nhập mới được tạo category
        [SwaggerOperation(
            Summary = "Create Category (Admin or Shop)",
            Description = @"Creates a new product category.
            
        - Admin: Creates GLOBAL categories available to all shops (ShopId automatically set to null).
        - Shop: Creates PRIVATE categories for their own shop only ."
        )]
        [SwaggerResponse(200, "Category created successfully", typeof(ApiResponse<CategoryResponse>))]
        [SwaggerResponse(400, "Validation error")]
        [SwaggerResponse(401, "Unauthorized - Shop ID not found in token for Shop user")]
        public async Task<IActionResult> CreateCategory(
            [FromForm] CreateCategoryRequest request,
            CancellationToken cancellationToken)
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty) return Unauthorized();

            var category = await _categoryService.CreateCategoryAsync(request, userId, cancellationToken);
            return SuccessResponse(category, "Category created successfully");
        }

        [HttpPatch("{id:guid}")]
        [Authorize] // [Fix #1] Chỉ Admin hoặc Shop đăng nhập mới được sửa category
        [SwaggerOperation(
            Summary = "Update Category (Admin or Shop)",
            Description = @"Updates an existing category's information.

        Authorization Rules:
        - Admin: Can update GLOBAL categories.
        - Shop: Can ONLY update their own PRIVATE categories (Enforced via token)."
        )]
        [SwaggerResponse(200, "Category updated successfully", typeof(ApiResponse<CategoryResponse>))]
        [SwaggerResponse(404, "Category not found")]
        [SwaggerResponse(401, "Unauthorized - Cannot update category")]
        public async Task<IActionResult> UpdateCategory(
            Guid id,
            [FromForm] UpdateCategoryRequest request,
            CancellationToken cancellationToken)
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty) return Unauthorized();

            var category = await _categoryService.UpdateCategoryAsync(id, request, userId, cancellationToken);
            return SuccessResponse(category);
        }

        [HttpDelete("{id:guid}")]
        [Authorize] // [Fix #1] Chỉ Admin hoặc Shop đăng nhập mới được xóa category
        [SwaggerOperation(
            Summary = "Delete Category (Admin or Shop)",
            Description = @"Soft deletes a category.

        Authorization Rules:
        - Admin: Can delete GLOBAL categories.
        - Shop: Can ONLY delete their own PRIVATE categories (Enforced via token).

        Restrictions:
        - Cannot delete category if it has sub-categories
        - Cannot delete category if it has products/parts"
        )]
        [SwaggerResponse(200, "Category deleted successfully")]
        [SwaggerResponse(404, "Category not found")]
        [SwaggerResponse(401, "Unauthorized - Cannot delete category")]
        public async Task<IActionResult> DeleteCategory(
             Guid id,
             CancellationToken cancellationToken)
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty) return Unauthorized();

            var result = await _categoryService.DeleteCategoryAsync(id, userId, cancellationToken);
            if (!result) return NotFoundResponse<string>($"Category with id {id} not found");
            return SuccessResponse<string>("Category deleted successfully");
        }

        [HttpGet("root")]
        [AllowAnonymous] // Public — ai cũng có thể xem root categories
        [SwaggerOperation(
    Summary = "Get Root Categories",
    Description = "Retrieves only the top-level categories."
)]
        [SwaggerResponse(200, "Root categories retrieved", typeof(IEnumerable<CategorySummaryResponse>))]
        public async Task<IActionResult> GetRootCategories(
            [FromQuery] bool includeInactive = false,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var categories = await _categoryService.GetRootCategoriesAsync(includeInactive, cancellationToken);
                return SuccessResponse(categories);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while fetching root categories");
                return ServerErrorResponse<string>("An error occurred while fetching root categories");
            }
        }

        [HttpGet("slug/{slug}/parts")]
        [AllowAnonymous] // Public — browse by slug cho storefront
        [SwaggerOperation(
    Summary = "Get Parts by Category Slug",
    Description = "Retrieves parts using the category's URL-friendly slug instead of ID."
)]
        [SwaggerResponse(200, "Parts retrieved successfully")]
        [SwaggerResponse(404, "Category slug not found")]
        public async Task<IActionResult> GetPartsByCategorySlug(
    string slug,
    [FromQuery] int pageNumber = 1,
    [FromQuery] int pageSize = 20,
    CancellationToken cancellationToken = default)
        {
            var category = await _categoryService.GetCategoryBySlugAsync(slug, cancellationToken);

            if (category == null)
                return NotFoundResponse<string>($"Category with slug '{slug}' not found");


            var queryParams = new GetPartsFilterRequest
            {
                CategoryId = category.Id,
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            var (items, totalCount, totalPages) = await _categoryService.GetPartsInCategoryAsync(queryParams, cancellationToken);
            var responseData = new
            {
                category = new { category.Id, category.Name, category.Slug },
                products = items,
                pagination = new { currentPage = pageNumber, pageSize, totalPages, totalCount }
            };
            return SuccessResponse(responseData);
        }
    }
}


