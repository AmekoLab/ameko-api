using FPTU.Capstone.AMKCollective.Application.DTOs;
using FPTU.Capstone.AMKCollective.Application.DTOs.Common;
using FPTU.Capstone.AMKCollective.Application.DTOs.AssembledProduct;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Api.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class AssembledProductController : BaseApiController
    {
        private readonly IAssembledProductService _service;

        public AssembledProductController(IAssembledProductService service)
        {
            _service = service;
        }

        /// <summary>
        /// Retrieves a paginated list of all assembled products.
        /// </summary>
        /// <param name="currentPage">The page number to retrieve.</param>
        /// <param name="pageSize">The number of items per page.</param>
        /// <returns>A paginated result containing assembled products.</returns>
        [Authorize]
        [HttpGet]
        [SwaggerOperation(
            Summary = "Get all assembled products",
            Description = "Returns a paginated list of all assembled products that are not deleted."
        )]
        [SwaggerResponse(200, "Successfully retrieved list", typeof(PaginatedResult<AssembledProductResponse>))]
        public async Task<IActionResult> GetAll([FromQuery] int currentPage = 1, [FromQuery] int pageSize = 10)
        {
            var result = await _service.GetAllAsync(currentPage, pageSize);
            return SuccessResponse(result);
        }

        /// <summary>
        /// Retrieves all assembled products belonging to a specific shop.
        /// </summary>
        /// <param name="shopId">The unique identifier of the shop.</param>
        /// <returns>A list of assembled products for the shop.</returns>
        [Authorize]
        [HttpGet("shop/{shopId}")]
        [SwaggerOperation(
            Summary = "Get assembled products by shop",
            Description = "Returns all assembled products that contain components or base kits belonging to the specified shop."
        )]
        [SwaggerResponse(200, "Successfully retrieved list", typeof(IEnumerable<AssembledProductResponse>))]
        public async Task<IActionResult> GetByShop(Guid shopId)
        {
            var result = await _service.GetByShopIdAsync(shopId);
            return SuccessResponse(result);
        }

        /// <summary>
        /// Retrieves details of a specific assembled product by its ID.
        /// </summary>
        /// <param name="id">The unique identifier of the assembled product.</param>
        /// <returns>The detailed information of the assembled product.</returns>
        [Authorize]
        [HttpGet("{id}")]
        [SwaggerOperation(
            Summary = "Get assembled product detail",
            Description = "Returns detailed information of an assembled product, including its components."
        )]
        [SwaggerResponse(200, "Successfully retrieved detail", typeof(AssembledProductDetailResponse))]
        [SwaggerResponse(404, "Assembled product not found")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _service.GetByIdAsync(id);
            if (result == null)
                return NotFoundResponse<AssembledProductDetailResponse>("Assembled product not found");

            return SuccessResponse(result);
        }

        /// <summary>
        /// Creates a new assembled product.
        /// </summary>
        /// <param name="request">The details for creating the assembled product.</param>
        /// <returns>The ID of the newly created assembled product.</returns>
        [Authorize(Roles = "Shop")]
        [HttpPost]
        [SwaggerOperation(
            Summary = "Create assembled product",
            Description = "Creates a new assembled product with the provided name, price, and components."
        )]
        [SwaggerResponse(200, "Successfully created", typeof(Guid))]
        [SwaggerResponse(400, "Validation failed (e.g. quantity <= 0)")]
        public async Task<IActionResult> Create([FromBody] CreateAssembledProductRequest request)
        {
            var result = await _service.CreateAsync(request);
            if (!string.IsNullOrEmpty(result.ErrorMessage))
            {
                return ErrorResponse<object>(result.ErrorMessage);
            }

            return SuccessResponse(result.Id, "Assembled product created successfully");
        }

        /// <summary>
        /// Updates an existing assembled product.
        /// </summary>
        /// <param name="id">The ID of the assembled product to update.</param>
        /// <param name="request">The updated information.</param>
        /// <returns>A success response if updated.</returns>
        [Authorize(Roles = "Shop")]
        [HttpPut("{id}")]
        [SwaggerOperation(
            Summary = "Update assembled product",
            Description = "Updates the basic information (Name, Price, 3D View) of an existing assembled product."
        )]
        [SwaggerResponse(200, "Successfully updated")]
        [SwaggerResponse(404, "Assembled product not found")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAssembledProductRequest request)
        {
            var result = await _service.UpdateAsync(id, request);
            if (!result)
                return NotFoundResponse<object>("Assembled product not found");

            return SuccessResponse("Assembled product updated successfully");
        }

        /// <summary>
        /// Soft deletes an assembled product.
        /// </summary>
        /// <param name="id">The ID of the assembled product to delete.</param>
        /// <returns>A success response if deleted.</returns>
        [Authorize(Roles = "Shop")]
        [HttpDelete("{id}")]
        [SwaggerOperation(
            Summary = "Soft delete assembled product",
            Description = "Marks an assembled product as deleted without removing it from the database."
        )]
        [SwaggerResponse(200, "Successfully deleted")]
        [SwaggerResponse(404, "Assembled product not found")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var result = await _service.DeleteAsync(id);
            if (!result)
                return NotFoundResponse<object>("Assembled product not found");

            return SuccessResponse("Assembled product deleted successfully");
        }

        /// <summary>
        /// Restores a soft-deleted assembled product.
        /// </summary>
        /// <param name="id">The ID of the assembled product to restore.</param>
        /// <returns>A success response if restored.</returns>
        [Authorize(Roles = "Shop")]
        [HttpPost("restore/{id}")]
        [SwaggerOperation(
            Summary = "Restore assembled product",
            Description = "Restores an assembled product that was previously soft-deleted."
        )]
        [SwaggerResponse(200, "Successfully restored")]
        [SwaggerResponse(404, "Assembled product not found")]
        public async Task<IActionResult> Restore(Guid id)
        {
            var result = await _service.RestoreAsync(id);
            if (!result)
                return NotFoundResponse<object>("Assembled product not found");

            return SuccessResponse("Assembled product restored successfully");
        }
    }
}
