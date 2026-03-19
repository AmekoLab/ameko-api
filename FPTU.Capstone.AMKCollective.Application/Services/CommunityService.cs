using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FPTU.Capstone.AMKCollective.Application.Helpers;
using FPTU.Capstone.AMKCollective.Application.DTOs.Community;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using FPTU.Capstone.AMKCollective.Application.BackgroundServices;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;

namespace FPTU.Capstone.AMKCollective.Application.Services
{
    public class CommunityService : ICommunityService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IPostEnricher _postEnricher;
        private readonly INotificationQueue _notificationQueue;

        public CommunityService(
            IUnitOfWork unitOfWork,
            IPostEnricher postEnricher,
            INotificationQueue notificationQueue)
        {
            _unitOfWork = unitOfWork;
            _postEnricher = postEnricher;
            _notificationQueue = notificationQueue;
        }

        public async Task<CursorPagedResult<PostFeedResponse>> GetFeedAsync(string? cursor, int pageSize, CancellationToken cancellationToken)
        {
            var decodedCursor = CursorHelper.DecodeCursor(cursor);
            var posts = await _unitOfWork.CommunityPosts.GetFeedCursorPagedAsync(decodedCursor?.CreatedAt, decodedCursor?.Id, pageSize, cancellationToken);
            
            var responseItems = posts.Select(p => new PostFeedResponse
            {
                Id = p.Id,
                UserId = p.UserId,
                Title = p.Title,
                CreatedAt = p.CreatedAt,
                AssembledProductId = p.AssembledProductId,
                AttachmentUrls = p.Attachments.Select(a => a.FileUrl).ToList(),
                ReactionCount = p.PostReactions.Count,
                CommentCount = p.PostComments.Count
            }).ToList();

            bool hasMore = responseItems.Count > pageSize;
            var itemsToReturn = responseItems.Take(pageSize).ToList();

            string? nextCursor = null;
            if (itemsToReturn.Any() && hasMore)
            {
                var lastItem = itemsToReturn.Last();
                nextCursor = CursorHelper.EncodeCursor(lastItem.CreatedAt, lastItem.Id);
            }

            // Enrich product data
            await _postEnricher.EnrichAsync(itemsToReturn, cancellationToken);

            return new CursorPagedResult<PostFeedResponse>
            {
                Items = itemsToReturn,
                HasMore = hasMore,
                NextCursor = nextCursor
            };
        }

        public async Task<PostFeedResponse> CreatePostAsync(Guid userId, CreatePostDto request, CancellationToken cancellationToken)
        {
            var post = new CommunityPost
            {
                UserId = userId,
                Title = request.Title,
                AssembledProductId = request.AssembledProductId,
            };

            if (request.AttachmentUrls != null && request.AttachmentUrls.Any())
            {
                foreach (var url in request.AttachmentUrls)
                {
                    post.Attachments.Add(new CommunityAttachment { FileUrl = url });
                }
            }

            await _unitOfWork.CommunityPosts.AddAsync(post, cancellationToken);
            await _unitOfWork.CommitAsync();

            // Queue background notification strictly replacing Task.Run
            var notificationItem = new NotificationDispatchItem
            {
                ActorId = userId,
                Type = FPTU.Capstone.AMKCollective.Domain.Enums.NotificationType.PostCreated,
                ReferenceId = post.Id.ToString(),
                ReferenceType = NotificationReferenceHelper.TypePost,
                RedirectUrl = $"/posts/{post.Id}"
            };
            await _notificationQueue.QueueNotificationAsync(notificationItem);

            var response = new PostFeedResponse
            {
                Id = post.Id,
                UserId = post.UserId,
                Title = post.Title,
                CreatedAt = post.CreatedAt,
                AssembledProductId = post.AssembledProductId,
                AttachmentUrls = request.AttachmentUrls ?? new List<string>()
            };

            await _postEnricher.EnrichAsync(new[] { response }, cancellationToken);

            return response;
        }

        public async Task<PostFeedResponse> GetPostByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            var post = await _unitOfWork.CommunityPosts.GetByIdAsync(id, cancellationToken);
            if (post == null) throw new KeyNotFoundException("Post not found");

            var responseItem = new PostFeedResponse
            {
                Id = post.Id,
                UserId = post.UserId,
                Title = post.Title,
                CreatedAt = post.CreatedAt,
                AssembledProductId = post.AssembledProductId,
                AttachmentUrls = post.Attachments.Select(a => a.FileUrl).ToList(),
                ReactionCount = post.PostReactions.Count,
                CommentCount = post.PostComments.Count
            };
            
            await _postEnricher.EnrichAsync(new[] { responseItem }, cancellationToken);
            return responseItem;
        }

        public async Task<PostFeedResponse> UpdatePostAsync(int id, Guid userId, UpdatePostDto request, CancellationToken cancellationToken = default)
        {
            var post = await _unitOfWork.CommunityPosts.GetByIdAsync(id, cancellationToken);
            if (post == null) throw new KeyNotFoundException("Post not found");
            if (post.UserId != userId) throw new UnauthorizedAccessException("You are not authorized to update this post");

            if (request.Title != null) post.Title = request.Title;
            if (request.AssembledProductId.HasValue) post.AssembledProductId = request.AssembledProductId;
            
            if (request.AttachmentUrls != null)
            {
                post.Attachments.Clear();
                foreach (var url in request.AttachmentUrls)
                {
                    post.Attachments.Add(new CommunityAttachment { FileUrl = url });
                }
            }

            _unitOfWork.CommunityPosts.Update(post);
            await _unitOfWork.CommitAsync();

            return await GetPostByIdAsync(post.Id, cancellationToken);
        }

        public async Task DeletePostAsync(int id, Guid userId, CancellationToken cancellationToken = default)
        {
            var post = await _unitOfWork.CommunityPosts.GetByIdAsync(id, cancellationToken);
            if (post == null) throw new KeyNotFoundException("Post not found");
            if (post.UserId != userId) throw new UnauthorizedAccessException("You are not authorized to delete this post");

            _unitOfWork.CommunityPosts.Remove(post);
            await _unitOfWork.CommitAsync();
        }

        public async Task ReactToPostAsync(int postId, Guid userId, FPTU.Capstone.AMKCollective.Domain.Enums.ReactionType type, CancellationToken cancellationToken = default)
        {
            var post = await _unitOfWork.CommunityPosts.GetByIdAsync(postId, cancellationToken);
            if (post == null) throw new KeyNotFoundException("Post not found");

            var existingReaction = await _unitOfWork.PostReactions.GetByUserAndPostAsync(postId, userId, cancellationToken);

            if (existingReaction == null)
            {
                // Case 1: No existing reaction -> create new
                var reaction = new PostReaction
                {
                    PostId = postId,
                    UserId = userId,
                    Type = type
                };
                await _unitOfWork.PostReactions.AddAsync(reaction, cancellationToken);

                // Notification to post owner
                if (post.UserId != userId)
                {
                    var notificationItem = new NotificationDispatchItem
                    {
                        ActorId = userId,
                        Type = FPTU.Capstone.AMKCollective.Domain.Enums.NotificationType.Reaction,
                        ReferenceId = postId.ToString(),
                        ReferenceType = NotificationReferenceHelper.TypePost,
                        RedirectUrl = $"/posts/{postId}"
                    };
                    await _notificationQueue.QueueNotificationAsync(notificationItem);
                }
            }
            else if (existingReaction.Type == type)
            {
                // Case 2: Same reaction type clicked again -> remove reaction (toggle off)
                _unitOfWork.PostReactions.Remove(existingReaction);
            }
            else
            {
                // Case 3: Different reaction type -> update existing reaction
                existingReaction.Type = type;
                _unitOfWork.PostReactions.Update(existingReaction);

                // Optional: Update notification or send new one? 
                // Usually just updating the reaction type doesn't need a new notification if one was already sent.
            }

            await _unitOfWork.CommitAsync();
        }

        public async Task<IEnumerable<PostReactionDetailResponse>> GetPostReactionsAsync(int postId, CancellationToken cancellationToken = default)
        {
            var reactions = await _unitOfWork.PostReactions.GetByPostAsync(postId, cancellationToken);
            
            return reactions.Select(r => new PostReactionDetailResponse
            {
                UserId = r.UserId,
                Username = r.User.Username,
                FullName = $"{r.User.FirstName} {r.User.LastName}",
                AvatarUrl = r.User.Image,
                ReactionType = r.Type.ToString(),
                CreatedAt = r.CreatedAt
            });
        }
    }
}
