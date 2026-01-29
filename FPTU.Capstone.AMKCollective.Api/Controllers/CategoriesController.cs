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
    [AllowAnonymous]
    [ApiController]
    [Route("api/v1/catalog/categories")]
    public class CategoriesController : BaseApiController
    {
        private readonly ICategoryService _categoryService;
        private readonly ILogger<CategoriesController> _logger;

        public CategoriesController(
            ICategoryService categoryService,
            ILogger<CategoriesController> logger)
        {
            _categoryService = categoryService;
            _logger = logger;
        }
        [HttpGet]
        [SwaggerOperation(
    Summary = "Get All Categories",
    Description = "Returns a hierarchical list of categories with optional filtering."
)]
        [SwaggerResponse(200, "Categories retrieved successfully", typeof(ApiResponse<IEnumerable<CategoryListDto>>))]
        public async Task<IActionResult> GetCategories(
            [FromQuery] CategoryQueryParams queryParams,
            CancellationToken cancellationToken)
        {
            try
            {
                var categories = await _categoryService.GetCategoriesAsync(queryParams, cancellationToken);
                return SuccessResponse(categories);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while fetching categories");
                return ServerErrorResponse<string>("An error occurred while fetching categories");
            }
        }
        [HttpGet("{id:guid}")]
        [SwaggerOperation(
    Summary = "Get Category by ID",
    Description = "Retrieves details of a specific category, optionally including its sub-categories."
)]
        [SwaggerResponse(200, "Category details retrieved", typeof(ApiResponse<CategoryDto>))]
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
                    return NotFoundResponse<CategoryDto>($"Category with id {id} not found");
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
                var queryParams = new PartQueryParams
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
        [SwaggerOperation(
    Summary = "Create Category (Admin)",
    Description = "Creates a new product category."
)]
        [SwaggerResponse(200, "Category created successfully", typeof(ApiResponse<CategoryDto>))]
        [SwaggerResponse(400, "Validation error")]
        public async Task<IActionResult> CreateCategory(
            [FromForm] CreateCategoryRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var category = await _categoryService.CreateCategoryAsync(request, cancellationToken);
                return SuccessResponse(category, "Category created successfully");
            }
            catch (ArgumentException ex)
            {
                return ErrorResponse<string>(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while creating category");
                return ServerErrorResponse<string>("An error occurred while creating the category");
            }
        }

        [HttpPatch("{id:guid}")]
        [SwaggerOperation(
    Summary = "Update Category (Admin)",
    Description = "Updates an existing category's information."
)]
        [SwaggerResponse(200, "Category updated successfully", typeof(ApiResponse<CategoryDto>))]
        [SwaggerResponse(404, "Category not found")]
        public async Task<IActionResult> UpdateCategory(
            Guid id,
            [FromForm] UpdateCategoryRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var category = await _categoryService.UpdateCategoryAsync(id, request, cancellationToken);
                return SuccessResponse(category);
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
                _logger.LogError(ex, "Error occurred while updating category {CategoryId}", id);
                return ServerErrorResponse<string>("An error occurred while updating the category");
            }
        }

        [HttpDelete("{id:guid}")]
        [SwaggerOperation(
    Summary = "Delete Category (Admin)",
    Description = "Soft deletes a category."
)]
        [SwaggerResponse(200, "Category deleted successfully")]
        [SwaggerResponse(404, "Category not found")]
        public async Task<IActionResult> DeleteCategory(
             Guid id,
             CancellationToken cancellationToken)
        {
            try
            {
                var result = await _categoryService.DeleteCategoryAsync(id, cancellationToken);

                if (!result)
                {
                    return NotFoundResponse<string>($"Category with id {id} not found");
                }
                return SuccessResponse("Category deleted successfully");
            }
            catch (InvalidOperationException ex)
            {
                return ErrorResponse<string>(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while deleting category {CategoryId}", id);
                return ServerErrorResponse<string>("An error occurred while deleting the category");
            }
        }

        [HttpGet("root")]
        [SwaggerOperation(
    Summary = "Get Root Categories",
    Description = "Retrieves only the top-level categories."
)]
        [SwaggerResponse(200, "Root categories retrieved", typeof(IEnumerable<CategoryListDto>))]
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


            var queryParams = new PartQueryParams
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


