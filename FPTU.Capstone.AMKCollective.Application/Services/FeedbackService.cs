using AutoMapper;
using FPTU.Capstone.AMKCollective.Application.DTOs.Feedback;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Domain.Enums;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Services
{
    public class FeedbackService : IFeedbackService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IStorageService _storageService;
        private readonly IMapper _mapper;
        private readonly HashSet<string> _badWords;

        public FeedbackService(IUnitOfWork unitOfWork, IStorageService storageService, IMapper mapper, IHostEnvironment environment)
        {
            _unitOfWork = unitOfWork;
            _storageService = storageService;
            _mapper = mapper;
            _badWords = LoadBadWords(environment.ContentRootPath);
        }

        public async Task<FeedbackResponse> CreateFeedbackAsync(Guid userId, Guid orderId, CreateFeedbackRequest request)
        {
            // 1. Validate Order
            var order = await _unitOfWork.Orders.GetByIdAsync(orderId);
            if (order == null)
                throw new Exception("Order not found.");

            if (order.CustomerId != userId)
                throw new Exception("You do not have permission to review this order.");

            // Check status Completed directly from your enum
            if (order.OrderStatus != OrderStatus.Completed)
                throw new Exception("Only completed orders can be reviewed.");

            // 2. Check if feedback already exists
            var isExist = await _unitOfWork.Feedbacks.ExistsByOrderIdAsync(orderId);
            if (isExist)
                throw new Exception("This order has already been reviewed.");

            if (order.ShopId == null)
                throw new Exception("Order does not belong to any shop.");

            ValidateNoBadWords(request.Comment, "Comment");

            // 3. Handle image upload via IStorageService.UploadAsync
            var feedbackImages = new List<FeedbackImage>();
            if (request.Images != null && request.Images.Any())
            {
                if (request.Images.Count > 5)
                    throw new Exception("You can only upload a maximum of 5 images.");

                foreach (var file in request.Images)
                {
                    using var stream = file.OpenReadStream();
                    var imageUrl = await _storageService.UploadAsync(stream, file.FileName, "feedbacks");
                    if (!string.IsNullOrEmpty(imageUrl))
                    {
                        feedbackImages.Add(new FeedbackImage { ImageUrl = imageUrl });
                    }
                }
            }

            // 4. Create Entity Feedback
            var feedback = new Feedback
            {
                OrderId = orderId,
                FromUserId = userId,
                ShopId = order.ShopId.Value,
                Rating = request.Rating,
                Comment = request.Comment,
                Images = feedbackImages
            };

            await _unitOfWork.Feedbacks.AddAsync(feedback);

            // 5. Update Shop Rating & Reviews
            var shop = await _unitOfWork.Shops.GetByIdAsync(order.ShopId.Value);
            if (shop != null)
            {
                double totalScore = (shop.Rating * shop.TotalReviews) + request.Rating;
                shop.TotalReviews += 1;
                shop.Rating = Math.Round(totalScore / shop.TotalReviews, 1);

                // Changed from Update() to UpdateAsync()
                await _unitOfWork.Shops.UpdateAsync(shop);
            }

            // 6. Commit Transaction
            await _unitOfWork.CommitAsync();

            var savedFeedback = await _unitOfWork.Feedbacks.GetByIdAsync(feedback.Id);
            var response = _mapper.Map<FeedbackResponse>(savedFeedback);
            response.ImageUrls = savedFeedback!.Images.Select(img => img.ImageUrl).ToList();

            return response;
        }

        public async Task<FeedbackResponse> UpdateFeedbackAsync(Guid userId, Guid feedbackId, UpdateFeedbackRequest request)
        {
            var feedback = await _unitOfWork.Feedbacks.GetByIdAsync(feedbackId);
            if (feedback == null)
                throw new Exception("Feedback not found.");

            if (feedback.FromUserId != userId)
                throw new UnauthorizedAccessException("You do not have permission to update this feedback.");

            if (feedback.EditCount >= 1)
                throw new Exception("You have already edited this feedback once.");

            if (!string.IsNullOrEmpty(feedback.ShopReply))
                throw new Exception("Feedback cannot be edited after the shop has replied.");

            ValidateNoBadWords(request.Comment, "Comment");

            feedback.Rating = request.Rating;
            feedback.Comment = request.Comment;
            feedback.EditCount += 1;

            if (request.Images != null && request.Images.Any())
            {
                if (request.Images.Count > 5)
                    throw new Exception("You can only upload a maximum of 5 images.");

                feedback.Images.Clear();
                foreach (var file in request.Images)
                {
                    using var stream = file.OpenReadStream();
                    var imageUrl = await _storageService.UploadAsync(stream, file.FileName, "feedbacks");
                    if (!string.IsNullOrEmpty(imageUrl))
                    {
                        feedback.Images.Add(new FeedbackImage { ImageUrl = imageUrl });
                    }
                }
            }

            _unitOfWork.Feedbacks.Update(feedback);
            await _unitOfWork.CommitAsync();

            var response = _mapper.Map<FeedbackResponse>(feedback);
            response.ImageUrls = feedback.Images.Select(img => img.ImageUrl).ToList();
            return response;
        }

        public async Task<FeedbackResponse> ReplyFeedbackAsync(Guid shopUserId, Guid feedbackId, ReplyFeedbackRequest request)
        {
            var feedback = await _unitOfWork.Feedbacks.GetByIdAsync(feedbackId);
            if (feedback == null)
                throw new Exception("Feedback not found.");

            var shop = await _unitOfWork.Shops.GetByIdAsync(feedback.ShopId);
            if (shop == null || shop.UserId != shopUserId)
                throw new Exception("You do not have permission to reply to this shop's feedback.");

            if (!string.IsNullOrEmpty(feedback.ShopReply))
                throw new Exception("This feedback has already been replied to.");

            ValidateNoBadWords(request.Reply, "Reply");

            feedback.ShopReply = request.Reply;
            feedback.ShopRepliedAt = DateTime.UtcNow;

            _unitOfWork.Feedbacks.Update(feedback);
            await _unitOfWork.CommitAsync();

            var response = _mapper.Map<FeedbackResponse>(feedback);
            response.ImageUrls = feedback.Images.Select(img => img.ImageUrl).ToList();
            return response;
        }

        private HashSet<string> LoadBadWords(string contentRootPath)
        {
            var filePath = Path.Combine(contentRootPath, "VietnameseBadWord.txt");
            if (!File.Exists(filePath))
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            var words = File.ReadAllLines(filePath)
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrEmpty(line))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return words;
        }

        private void ValidateNoBadWords(string? text, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            foreach (var badWord in _badWords)
            {
                if (text.Contains(badWord, StringComparison.OrdinalIgnoreCase))
                {
                    throw new Exception($"{fieldName} contains inappropriate language.");
                }
            }
        }

        public async Task<PaginatedResult<FeedbackResponse>> GetShopFeedbacksAsync(Guid shopId, int pageNumber, int pageSize)
        {
            var (feedbacks, totalCount) = await _unitOfWork.Feedbacks.GetFeedbacksByShopIdAsync(shopId, pageNumber, pageSize);
            var responses = new List<FeedbackResponse>();

            foreach (var fb in feedbacks)
            {
                var dto = _mapper.Map<FeedbackResponse>(fb);
                dto.ImageUrls = fb.Images.Select(img => img.ImageUrl).ToList();
                responses.Add(dto);
            }

            return new PaginatedResult<FeedbackResponse>(responses, totalCount, pageNumber, pageSize);
        }

        public async Task<PaginatedResult<FeedbackResponse>> GetMyShopFeedbacksAsync(Guid userId, int pageNumber, int pageSize)
        {
            var shop = await _unitOfWork.Shops.GetByUserIdAsync(userId);
            if (shop == null)
            {
                return new PaginatedResult<FeedbackResponse>(new List<FeedbackResponse>(), 0, pageNumber, pageSize);
            }

            return await GetShopFeedbacksAsync(shop.Id, pageNumber, pageSize);
        }

        public async Task<FeedbackEligibilityResponse> GetOrderFeedbackEligibilityAsync(Guid userId, Guid orderId)
        {
            var order = await _unitOfWork.Orders.GetByIdAsync(orderId);
            if (order == null)
                throw new Exception("Order not found.");

            if (order.CustomerId != userId)
                throw new UnauthorizedAccessException("You do not have permission to access this order.");

            var isCompleted = order.OrderStatus == OrderStatus.Completed;
            var hasCustomItems = order.OrderItems.Any(i => i.IsCustom);
            var hasShopFeedback = await _unitOfWork.Feedbacks.ExistsByOrderIdAsync(orderId);
            var canReviewShop = isCompleted && hasCustomItems && order.ShopId.HasValue && !hasShopFeedback;

            var assembledItems = order.OrderItems
                .Where(i => !i.IsCustom && i.AssembledProductId.HasValue)
                .ToList();

            var assembledItemIds = assembledItems.Select(i => i.Id).ToList();
            var existingFeedbacks = await _unitOfWork.AssembledProductFeedbacks.GetByOrderItemIdsAsync(assembledItemIds);
            var feedbackMap = existingFeedbacks
                .GroupBy(f => f.OrderItemId)
                .ToDictionary(g => g.Key, g => g.First().Id);

            var assembledEligibility = assembledItems.Select(item =>
            {
                var hasFeedback = feedbackMap.TryGetValue(item.Id, out var feedbackId);
                return new AssembledItemFeedbackEligibility
                {
                    OrderItemId = item.Id,
                    AssembledProductId = item.AssembledProductId!.Value,
                    HasFeedback = hasFeedback,
                    CanReview = isCompleted && !hasFeedback,
                    FeedbackId = hasFeedback ? feedbackId : null
                };
            }).ToList();

            return new FeedbackEligibilityResponse
            {
                OrderId = order.Id,
                IsOrderCompleted = isCompleted,
                CanReviewShop = canReviewShop,
                HasShopFeedback = hasShopFeedback,
                AssembledItems = assembledEligibility
            };
        }
        public async Task<FeedbackResponse?> GetMyFeedbackByOrderAsync(Guid userId, Guid orderId)
        {
            var feedback = await _unitOfWork.Feedbacks.GetByOrderIdAsync(orderId);

            if (feedback == null) return null;

            // Bảo mật: Chỉ cho phép chính người mua xem lại đánh giá của mình
            if (feedback.FromUserId != userId)
                throw new UnauthorizedAccessException("You do not have permission to access this order.");

            var response = _mapper.Map<FeedbackResponse>(feedback);
            response.ImageUrls = feedback.Images.Select(img => img.ImageUrl).ToList();

            return response;
        }
    }
}
