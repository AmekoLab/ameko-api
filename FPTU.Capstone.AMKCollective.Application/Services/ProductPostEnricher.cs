using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FPTU.Capstone.AMKCollective.Application.DTOs.Community;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;

namespace FPTU.Capstone.AMKCollective.Application.Services
{
    public class ProductPostEnricher : IPostEnricher
    {
        private readonly IUnitOfWork _unitOfWork;

        public ProductPostEnricher(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task EnrichAsync(IEnumerable<PostFeedResponse> posts, CancellationToken cancellationToken = default)
        {
            var productIds = posts
                .Where(p => p.AssembledProductId.HasValue)
                .Select(p => p.AssembledProductId!.Value)
                .Distinct()
                .ToList();

            if (!productIds.Any()) return;

            // Fetch products efficiently in bulk mapping to ProductPreviewDto
            var rawProducts = await _unitOfWork.AssembledProducts.GetByIdsAsync(productIds, cancellationToken);
            var productsDict = rawProducts.ToDictionary(
                p => p.Id,
                p => new ProductPreviewDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Price = p.Price,
                    ImageUrl = p.Image1 ?? p.Image2 ?? p.Image3 ?? string.Empty, // Coalescing existing imageUrl
                    ImageUrls = new[] { p.Image1, p.Image2, p.Image3 }
                                 .Where(url => !string.IsNullOrEmpty(url))
                                 .ToList()!,
                    Quantity = p.Quantity ?? 0
                });

            foreach (var post in posts)
            {
                if (post.AssembledProductId.HasValue &&
                    productsDict.TryGetValue(post.AssembledProductId.Value, out var productData))
                {
                    post.Product = productData;
                }
            }
        }
    }
}
