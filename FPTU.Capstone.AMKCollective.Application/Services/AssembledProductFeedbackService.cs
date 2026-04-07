using AutoMapper;
using FPTU.Capstone.AMKCollective.Application.DTOs.AssembledProductFeedback;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Domain.Enums;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Services
{
    public class AssembledProductFeedbackService : IAssembledProductFeedbackService
    {
        private const int MaxImages = 5;
        private const string UploadFolder = "assembled-feedbacks";

        private readonly IUnitOfWork _unitOfWork;
        private readonly IStorageService _storageService;
        private readonly IMapper _mapper;
        private readonly HashSet<string> _badWords;

        public AssembledProductFeedbackService(
            IUnitOfWork unitOfWork,
            IStorageService storageService,
            IMapper mapper,
            IHostEnvironment environment)
        {
            _unitOfWork = unitOfWork;
            _storageService = storageService;
            _mapper = mapper;
            _badWords = LoadBadWords(environment.ContentRootPath);
        }

        public async Task<AssembledProductFeedbackResponse> CreateAsync(Guid userId, Guid orderItemId, CreateAssembledProductFeedbackRequest request)
        {
            var orderItem = await _unitOfWork.Orders.GetOrderItemByIdAsync(orderItemId);
            if (orderItem == null)
                throw new Exception("Order item not found.");

            if (orderItem.IsCustom || !orderItem.AssembledProductId.HasValue)
                throw new Exception("Order item is not an assembled product.");

            var order = await _unitOfWork.Orders.GetByIdAsync(orderItem.OrderId);
            if (order == null)
                throw new Exception("Order not found.");

            if (order.CustomerId != userId)
                throw new Exception("You do not have permission to review this order item.");

            if (order.OrderStatus != OrderStatus.Completed)
                throw new Exception("Only completed orders can be reviewed.");

            if (order.ShopId == null)
                throw new Exception("Order does not belong to any shop.");

            var exists = await _unitOfWork.AssembledProductFeedbacks.ExistsByOrderItemIdAsync(orderItemId);
            if (exists)
                throw new Exception("This item has already been reviewed.");

            ValidateNoBadWords(request.Comment, "Comment");

            var feedbackImages = new List<FeedbackImage>();
            if (request.Images != null && request.Images.Any())
            {
                if (request.Images.Count > MaxImages)
                    throw new Exception("You can only upload a maximum of 5 images.");

                foreach (var file in request.Images)
                {
                    using var stream = file.OpenReadStream();
                    var imageUrl = await _storageService.UploadAsync(stream, file.FileName, UploadFolder);
                    if (!string.IsNullOrEmpty(imageUrl))
                    {
                        feedbackImages.Add(new FeedbackImage { ImageUrl = imageUrl });
                    }
                }
            }

            var feedback = new AssembledProductFeedback
            {
                OrderItemId = orderItemId,
                AssembledProductId = orderItem.AssembledProductId.Value,
                FromUserId = userId,
                ShopId = order.ShopId.Value,
                Rating = request.Rating,
                Comment = request.Comment,
                Images = feedbackImages
            };

            await _unitOfWork.AssembledProductFeedbacks.AddAsync(feedback);

            var product = await _unitOfWork.AssembledProducts.GetByIdWithDetailsAsync(orderItem.AssembledProductId.Value);
            if (product != null)
            {
                double totalScore = (product.Rating * product.TotalReviews) + request.Rating;
                product.TotalReviews += 1;
                product.Rating = Math.Round(totalScore / product.TotalReviews, 1);
                await _unitOfWork.AssembledProducts.UpdateAsync(product);
            }

            await _unitOfWork.CommitAsync();

            var savedFeedback = await _unitOfWork.AssembledProductFeedbacks.GetByIdAsync(feedback.Id);
            var response = _mapper.Map<AssembledProductFeedbackResponse>(savedFeedback);
            response.ImageUrls = savedFeedback!.Images.Select(img => img.ImageUrl).ToList();

            return response;
        }

        public async Task<AssembledProductFeedbackResponse> UpdateAsync(Guid userId, Guid feedbackId, UpdateAssembledProductFeedbackRequest request)
        {
            var feedback = await _unitOfWork.AssembledProductFeedbacks.GetByIdAsync(feedbackId);
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
                if (request.Images.Count > MaxImages)
                    throw new Exception("You can only upload a maximum of 5 images.");

                feedback.Images.Clear();
                foreach (var file in request.Images)
                {
                    using var stream = file.OpenReadStream();
                    var imageUrl = await _storageService.UploadAsync(stream, file.FileName, UploadFolder);
                    if (!string.IsNullOrEmpty(imageUrl))
                    {
                        feedback.Images.Add(new FeedbackImage { ImageUrl = imageUrl });
                    }
                }
            }

            _unitOfWork.AssembledProductFeedbacks.Update(feedback);
            await _unitOfWork.CommitAsync();

            var response = _mapper.Map<AssembledProductFeedbackResponse>(feedback);
            response.ImageUrls = feedback.Images.Select(img => img.ImageUrl).ToList();
            return response;
        }

        public async Task<AssembledProductFeedbackResponse> ReplyAsync(Guid shopUserId, Guid feedbackId, ReplyAssembledProductFeedbackRequest request)
        {
            var feedback = await _unitOfWork.AssembledProductFeedbacks.GetByIdAsync(feedbackId);
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

            _unitOfWork.AssembledProductFeedbacks.Update(feedback);
            await _unitOfWork.CommitAsync();

            var response = _mapper.Map<AssembledProductFeedbackResponse>(feedback);
            response.ImageUrls = feedback.Images.Select(img => img.ImageUrl).ToList();
            return response;
        }

        public async Task<PaginatedResult<AssembledProductFeedbackResponse>> GetByProductIdAsync(Guid productId, int pageNumber, int pageSize)
        {
            var (feedbacks, totalCount) = await _unitOfWork.AssembledProductFeedbacks.GetByProductIdAsync(productId, pageNumber, pageSize);
            var responses = new List<AssembledProductFeedbackResponse>();

            foreach (var fb in feedbacks)
            {
                var dto = _mapper.Map<AssembledProductFeedbackResponse>(fb);
                dto.ImageUrls = fb.Images.Select(img => img.ImageUrl).ToList();
                responses.Add(dto);
            }

            return new PaginatedResult<AssembledProductFeedbackResponse>(responses, totalCount, pageNumber, pageSize);
        }

        public async Task<PaginatedResult<AssembledProductFeedbackResponse>> GetMyShopFeedbacksAsync(Guid userId, int pageNumber, int pageSize)
        {
            var shop = await _unitOfWork.Shops.GetByUserIdAsync(userId);
            if (shop == null)
            {
                return new PaginatedResult<AssembledProductFeedbackResponse>(new List<AssembledProductFeedbackResponse>(), 0, pageNumber, pageSize);
            }

            var (feedbacks, totalCount) = await _unitOfWork.AssembledProductFeedbacks.GetByShopIdAsync(shop.Id, pageNumber, pageSize);
            var responses = new List<AssembledProductFeedbackResponse>();

            foreach (var fb in feedbacks)
            {
                var dto = _mapper.Map<AssembledProductFeedbackResponse>(fb);
                dto.ImageUrls = fb.Images.Select(img => img.ImageUrl).ToList();
                responses.Add(dto);
            }

            return new PaginatedResult<AssembledProductFeedbackResponse>(responses, totalCount, pageNumber, pageSize);
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
    }
}
