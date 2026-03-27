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
using FPTU.Capstone.AMKCollective.Application.Exceptions;
using Microsoft.Extensions.Configuration;

namespace FPTU.Capstone.AMKCollective.Application.Services
{
    public class CommunityService : ICommunityService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IPostEnricher _postEnricher;
        private readonly INotificationQueue _notificationQueue;
        private readonly IContentModerationService _moderationService;
        private readonly IConfiguration _configuration;

        public CommunityService(
            IUnitOfWork unitOfWork,
            IPostEnricher postEnricher,
            INotificationQueue notificationQueue,
            IContentModerationService moderationService,
            IConfiguration configuration)
        {
            _unitOfWork = unitOfWork;
            _postEnricher = postEnricher;
            _notificationQueue = notificationQueue;
            _moderationService = moderationService;
            _configuration = configuration;
        }

        public async Task<CursorPagedResult<PostFeedResponse>> GetFeedAsync(string? cursor, int pageSize, CancellationToken cancellationToken)
        {
            var decodedCursor = CursorHelper.DecodeCursor(cursor);
            var posts = await _unitOfWork.CommunityPosts.GetFeedCursorPagedAsync(decodedCursor?.CreatedAt, decodedCursor?.Id, pageSize, cancellationToken);
            
            return await MapPostsToFeedResponse(posts, pageSize, cancellationToken);
        }

        public async Task<CursorPagedResult<PostFeedResponse>> GetPostsByUserIdAsync(Guid userId, string? cursor, int pageSize, CancellationToken cancellationToken = default)
        {
            var decodedCursor = CursorHelper.DecodeCursor(cursor);
            var posts = await _unitOfWork.CommunityPosts.GetByUserIdCursorPagedAsync(userId, decodedCursor?.CreatedAt, decodedCursor?.Id, pageSize, cancellationToken);
            
            return await MapPostsToFeedResponse(posts, pageSize, cancellationToken);
        }

        private async Task<CursorPagedResult<CommentResponse>> MapCommentsToResponse(List<PostComment> comments, int pageSize)
        {
            var responseItems = comments.Select(c => new CommentResponse
            {
                Id = c.Id,
                UserId = c.UserId,
                Username = c.User?.Username ?? "Unknown",
                FullName = c.User != null ? $"{c.User.FirstName} {c.User.LastName}" : "Unknown",
                AvatarUrl = c.User?.Image,
                Content = c.Content,
                CreatedAt = c.CreatedAt
            }).ToList();

            bool hasMore = responseItems.Count > pageSize;
            var itemsToReturn = responseItems.Take(pageSize).ToList();

            string? nextCursor = null;
            if (itemsToReturn.Any() && hasMore)
            {
                var lastItem = itemsToReturn.Last();
                nextCursor = CursorHelper.EncodeCursor(lastItem.CreatedAt, lastItem.Id);
            }

            return new CursorPagedResult<CommentResponse>
            {
                Items = itemsToReturn,
                HasMore = hasMore,
                NextCursor = nextCursor
            };
        }


        private async Task<CursorPagedResult<PostFeedResponse>> MapPostsToFeedResponse(List<CommunityPost> posts, int pageSize, CancellationToken cancellationToken)
        {
            var responseItems = posts.Take(pageSize + 1).Select(p => new PostFeedResponse
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
            var sanitizedTitle = await _moderationService.ProcessContentAsync(userId, request.Title, "Post", 0, cancellationToken);
            
            var post = new CommunityPost
            {
                UserId = userId,
                Title = sanitizedTitle,
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

            if (request.Title != null)
            {
                post.Title = await _moderationService.ProcessContentAsync(userId, request.Title, "Post", 0, cancellationToken);
            }

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
                var reaction = new PostReaction
                {
                    PostId = postId,
                    UserId = userId,
                    Type = type
                };
                await _unitOfWork.PostReactions.AddAsync(reaction, cancellationToken);

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
                _unitOfWork.PostReactions.Remove(existingReaction);
            }
            else
            {
                existingReaction.Type = type;
                _unitOfWork.PostReactions.Update(existingReaction);
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

        public async Task<CommentResponse> AddCommentAsync(int postId, Guid userId, CreateCommentDto request, CancellationToken cancellationToken = default)
        {
            // Rate limit check (using the unified service)
            int limit = _configuration.GetValue<int>("SecuritySettings:MaxCommentsPerMinute", 20);
            var sanitizedContent = await _moderationService.ProcessContentAsync(userId, request.Content, "Comment", limit, cancellationToken);

            var post = await _unitOfWork.CommunityPosts.GetByIdAsync(postId, cancellationToken);
            if (post == null) throw new KeyNotFoundException("Post not found");

            var comment = new PostComment
            {
                PostId = postId,
                UserId = userId,
                Content = sanitizedContent
            };

            await _unitOfWork.PostComments.AddAsync(comment, cancellationToken);
            await _unitOfWork.CommitAsync();

            if (post.UserId != userId)
            {
                await _notificationQueue.QueueNotificationAsync(new NotificationDispatchItem
                {
                    ActorId = userId,
                    Type = FPTU.Capstone.AMKCollective.Domain.Enums.NotificationType.Comment,
                    ReferenceId = postId.ToString(),
                    ReferenceType = NotificationReferenceHelper.TypePost,
                    RedirectUrl = $"/posts/{postId}"
                });
            }

            var user = await _unitOfWork.Users.GetByIdAsync(userId);

            return new CommentResponse
            {
                Id = comment.Id,
                UserId = userId,
                Username = user?.Username ?? "Unknown",
                FullName = user != null ? $"{user.FirstName} {user.LastName}" : "Unknown",
                AvatarUrl = user?.Image,
                Content = comment.Content,
                CreatedAt = comment.CreatedAt
            };
        }

        public async Task<CursorPagedResult<CommentResponse>> GetPostCommentsAsync(int postId, string? cursor, int pageSize, CancellationToken cancellationToken = default)
        {
            var decodedCursor = CursorHelper.DecodeCursor(cursor);
            var comments = await _unitOfWork.PostComments.GetByPostIdCursorPagedAsync(postId, decodedCursor?.CreatedAt, decodedCursor?.Id, pageSize, cancellationToken);
            return await MapCommentsToResponse(comments, pageSize);
        }
    }
}
