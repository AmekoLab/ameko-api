using AutoMapper;
using FPTU.Capstone.AMKCollective.Application.DTOs.Feedback;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Services
{
    public class FeedbackService : IFeedbackService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IStorageService _storageService;
        private readonly IMapper _mapper;

        public FeedbackService(IUnitOfWork unitOfWork, IStorageService storageService, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _storageService = storageService;
            _mapper = mapper;
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

            feedback.ShopReply = request.Reply;
            feedback.ShopRepliedAt = DateTime.UtcNow;

            _unitOfWork.Feedbacks.Update(feedback);
            await _unitOfWork.CommitAsync();

            var response = _mapper.Map<FeedbackResponse>(feedback);
            response.ImageUrls = feedback.Images.Select(img => img.ImageUrl).ToList();
            return response;
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
    }
}
