using FPTU.Capstone.AMKCollective.Domain.Enums;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Shop
{
    public class ShopFilterRequest
    {
        public string? SearchTerm { get; set; }
        public double? MinRating { get; set; }
        public int? MinReviews { get; set; }
        public ShopBadge? Badge { get; set; }
        public ShopSortBy SortBy { get; set; } = ShopSortBy.Rating;
        public int Page { get; set; } = 1;
        public int Size { get; set; } = 10;
    }

    public enum ShopSortBy
    {
        Rating = 0,
        TotalReviews = 1,
        TotalSales = 2,
        Newest = 3,
        QualityScore = 4
    }
}
