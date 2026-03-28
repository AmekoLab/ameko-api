using AutoMapper;
using FPTU.Capstone.AMKCollective.Application.DTOs.Chat;
using FPTU.Capstone.AMKCollective.Application.Helpers;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Domain.Enums;

namespace FPTU.Capstone.AMKCollective.Application.Services
{
    public class ChatService : IChatService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IChatRealtimePublisher _chatRealtimePublisher;

        public ChatService(IUnitOfWork unitOfWork, IMapper mapper, IChatRealtimePublisher chatRealtimePublisher)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _chatRealtimePublisher = chatRealtimePublisher;
        }

        public async Task<ConversationResponse> GetOrCreateDirectConversationAsync(Guid currentUserId, Guid targetUserId, CancellationToken cancellationToken = default)
        {
            if (currentUserId == targetUserId)
            {
                throw new InvalidOperationException("You cannot chat with yourself.");
            }

            var targetUser = await _unitOfWork.Users.GetByIdAsync(targetUserId);
            if (targetUser == null)
            {
                throw new KeyNotFoundException("Target user not found.");
            }

            var conversation = await _unitOfWork.Conversations.GetConversationByUserIdsAsync(currentUserId, targetUserId);
            if (conversation == null)
            {
                conversation = new Conversation
                {
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    IsRestricted = true
                };

                await _unitOfWork.Conversations.AddConversationAsync(conversation);
                await _unitOfWork.CommitAsync();

                await _unitOfWork.Conversations.AddUserConversationAsync(new UserConversation
                {
                    UserId = currentUserId,
                    ConversationId = conversation.Id,
                    IsOwner = true,
                    CreatedAt = DateTime.UtcNow
                });

                await _unitOfWork.Conversations.AddUserConversationAsync(new UserConversation
                {
                    UserId = targetUserId,
                    ConversationId = conversation.Id,
                    IsOwner = false,
                    CreatedAt = DateTime.UtcNow
                });

                await _unitOfWork.CommitAsync();
            }

            return await BuildConversationResponseAsync(currentUserId, conversation.Id);
        }

        public async Task<ChatMessageResponse> SendMessageAsync(Guid senderId, SendMessageRequest request, CancellationToken cancellationToken = default)
        {
            var trimmedContent = request.Content?.Trim();
            if (string.IsNullOrWhiteSpace(trimmedContent))
            {
                throw new InvalidOperationException("Message content is required.");
            }

            var conversationId = await ResolveConversationIdAsync(senderId, request, cancellationToken);
            var isMember = await IsUserInConversationAsync(senderId, conversationId, cancellationToken);
            if (!isMember)
            {
                throw new UnauthorizedAccessException("You do not have access to this conversation.");
            }

            if (request.ParentMessageId.HasValue)
            {
                var parentMessage = await _unitOfWork.Conversations.GetMessageByIdAsync(request.ParentMessageId.Value);
                if (parentMessage == null)
                {
                    throw new KeyNotFoundException("Parent message not found.");
                }

                var isParentInConversation = await _unitOfWork.Conversations
                    .IsMessageInConversationAsync(request.ParentMessageId.Value, conversationId);
                if (!isParentInConversation)
                {
                    throw new InvalidOperationException("Parent message must belong to this conversation.");
                }
            }

            var message = new Message
            {
                Content = trimmedContent,
                SenderId = senderId,
                MessageType = request.MessageType,
                ParentMessageId = request.ParentMessageId,
                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.Conversations.AddMessageAsync(message);
            await _unitOfWork.CommitAsync();

            var participants = await _unitOfWork.Conversations.GetConversationParticipantsAsync(conversationId);
            var now = DateTime.UtcNow;
            var recipients = participants.Select(participant => new MessageRecipient
            {
                UserId = participant.UserId,
                UserConversationId = participant.Id,
                MessageId = message.Id,
                IsRead = participant.UserId == senderId,
                CreatedAt = now
            }).ToList();

            await _unitOfWork.Conversations.AddMessageRecipientsAsync(recipients);

            var conversation = await _unitOfWork.Conversations.GetConversationByIdAsync(conversationId);
            if (conversation != null)
            {
                conversation.UpdatedAt = now;
                _unitOfWork.Conversations.UpdateConversation(conversation);
            }

            await _unitOfWork.CommitAsync();

            var response = _mapper.Map<ChatMessageResponse>(message);
            response.ConversationId = conversationId;

            await _chatRealtimePublisher.PublishMessageReceivedAsync(response, cancellationToken);
            return response;
        }

        public async Task<CursorPagedResult<ConversationResponse>> GetUserConversationsAsync(Guid userId, string? cursor, int pageSize, CancellationToken cancellationToken = default)
        {
            pageSize = NormalizePageSize(pageSize, 20, 50);

            var memberships = await _unitOfWork.Conversations.GetUserConversationsAsync(userId);
            var ordered = memberships
                .OrderByDescending(uc => uc.Conversation.UpdatedAt ?? uc.Conversation.CreatedAt)
                .ThenByDescending(uc => uc.ConversationId)
                .ToList();

            var decodedCursor = CursorHelper.DecodeCursor(cursor);
            if (decodedCursor.HasValue)
            {
                ordered = ordered
                    .Where(uc =>
                    {
                        var key = uc.Conversation.UpdatedAt ?? uc.Conversation.CreatedAt;
                        return key < decodedCursor.Value.CreatedAt
                               || (key == decodedCursor.Value.CreatedAt && uc.ConversationId < decodedCursor.Value.Id);
                    })
                    .ToList();
            }

            var selected = ordered.Take(pageSize + 1).ToList();
            var hasMore = selected.Count > pageSize;
            var pageItems = selected.Take(pageSize).ToList();

            var dtos = new List<ConversationResponse>(pageItems.Count);
            foreach (var membership in pageItems)
            {
                dtos.Add(await BuildConversationResponseAsync(userId, membership.ConversationId));
            }

            string? nextCursor = null;
            if (hasMore && pageItems.Count > 0)
            {
                var last = pageItems.Last();
                var lastKey = last.Conversation.UpdatedAt ?? last.Conversation.CreatedAt;
                nextCursor = CursorHelper.EncodeCursor(lastKey, last.ConversationId);
            }

            return new CursorPagedResult<ConversationResponse>
            {
                Items = dtos,
                HasMore = hasMore,
                NextCursor = nextCursor
            };
        }

        public async Task<CursorPagedResult<ChatMessageResponse>> GetConversationMessagesAsync(Guid userId, int conversationId, string? cursor, int pageSize, CancellationToken cancellationToken = default)
        {
            pageSize = NormalizePageSize(pageSize, 30, 100);

            var isMember = await IsUserInConversationAsync(userId, conversationId, cancellationToken);
            if (!isMember)
            {
                throw new UnauthorizedAccessException("You do not have access to this conversation.");
            }

            var messages = await _unitOfWork.Conversations.GetMessagesByConversationIdAsync(conversationId);
            var orderedDesc = messages
                .OrderByDescending(m => m.CreatedAt)
                .ThenByDescending(m => m.Id)
                .ToList();

            var decodedCursor = CursorHelper.DecodeCursor(cursor);
            if (decodedCursor.HasValue)
            {
                orderedDesc = orderedDesc
                    .Where(m =>
                        m.CreatedAt < decodedCursor.Value.CreatedAt
                        || (m.CreatedAt == decodedCursor.Value.CreatedAt && m.Id < decodedCursor.Value.Id))
                    .ToList();
            }

            var selected = orderedDesc.Take(pageSize + 1).ToList();
            var hasMore = selected.Count > pageSize;
            var pageItems = selected.Take(pageSize).OrderBy(m => m.CreatedAt).ThenBy(m => m.Id).ToList();

            var dtos = pageItems.Select(m =>
            {
                var mapped = _mapper.Map<ChatMessageResponse>(m);
                mapped.ConversationId = conversationId;
                return mapped;
            }).ToList();

            // Attach current user's reaction from recipient rows.
            foreach (var dto in dtos)
            {
                var recipient = await _unitOfWork.Conversations.GetMessageRecipientAsync(userId, conversationId, dto.Id);
                dto.Reaction = recipient?.MessageReaction;
            }

            string? nextCursor = null;
            if (hasMore && pageItems.Count > 0)
            {
                var last = pageItems.First();
                nextCursor = CursorHelper.EncodeCursor(last.CreatedAt, last.Id);
            }

            return new CursorPagedResult<ChatMessageResponse>
            {
                Items = dtos,
                HasMore = hasMore,
                NextCursor = nextCursor
            };
        }

        public async Task MarkMessagesAsReadAsync(Guid userId, int conversationId, int upToMessageId, CancellationToken cancellationToken = default)
        {
            var isMember = await IsUserInConversationAsync(userId, conversationId, cancellationToken);
            if (!isMember)
            {
                throw new UnauthorizedAccessException("You do not have access to this conversation.");
            }

            var unreadRecipients = await _unitOfWork.Conversations.GetUnreadRecipientsAsync(userId, conversationId, upToMessageId);
            if (unreadRecipients.Count == 0)
            {
                await _chatRealtimePublisher.PublishReadReceiptAsync(userId: userId, conversationId: conversationId, upToMessageId: upToMessageId, cancellationToken: cancellationToken);
                return;
            }

            var now = DateTime.UtcNow;
            foreach (var recipient in unreadRecipients)
            {
                recipient.IsRead = true;
                recipient.UpdatedAt = now;
            }

            _unitOfWork.Conversations.UpdateMessageRecipients(unreadRecipients);
            await _unitOfWork.CommitAsync();

            await _chatRealtimePublisher.PublishReadReceiptAsync(userId: userId, conversationId: conversationId, upToMessageId: upToMessageId, cancellationToken: cancellationToken);
        }

        public async Task<MessageReactionResponse> SetMessageReactionAsync(Guid userId, int conversationId, int messageId, MessageReaction? reaction, CancellationToken cancellationToken = default)
        {
            var result = await UpdateReactionAsync(userId, conversationId, messageId, reaction, cancellationToken);
            await _chatRealtimePublisher.PublishReactionChangedAsync(result, cancellationToken);
            return result;
        }

        public async Task<bool> IsUserInConversationAsync(Guid userId, int conversationId, CancellationToken cancellationToken = default)
        {
            var userConversation = await _unitOfWork.Conversations.GetUserConversationAsync(userId, conversationId);
            return userConversation != null;
        }

        private async Task<int> ResolveConversationIdAsync(Guid senderId, SendMessageRequest request, CancellationToken cancellationToken)
        {
            if (request.ConversationId.HasValue)
            {
                return request.ConversationId.Value;
            }

            if (!request.TargetUserId.HasValue)
            {
                throw new InvalidOperationException("TargetUserId is required when ConversationId is not provided.");
            }

            var conversation = await GetOrCreateDirectConversationAsync(senderId, request.TargetUserId.Value, cancellationToken);
            return conversation.ConversationId;
        }

        private async Task<ConversationResponse> BuildConversationResponseAsync(Guid currentUserId, int conversationId)
        {
            var participants = await _unitOfWork.Conversations.GetConversationParticipantsAsync(conversationId);
            var otherParticipant = participants.FirstOrDefault(p => p.UserId != currentUserId);
            if (otherParticipant == null)
            {
                throw new InvalidOperationException("Invalid direct conversation participants.");
            }

            var otherUser = await _unitOfWork.Users.GetByIdAsync(otherParticipant.UserId);
            var messages = await _unitOfWork.Conversations.GetMessagesByConversationIdAsync(conversationId);
            var lastMessage = messages.OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id).FirstOrDefault();
            var unreadCount = await _unitOfWork.Conversations.GetUnreadCountAsync(currentUserId, conversationId);

            return new ConversationResponse
            {
                ConversationId = conversationId,
                OtherUserId = otherParticipant.UserId,
                OtherUserName = otherUser == null ? "Unknown" : $"{otherUser.FirstName} {otherUser.LastName}".Trim(),
                OtherUserAvatarUrl = otherUser?.Image,
                LastMessage = lastMessage?.Content ?? string.Empty,
                LastMessageAt = lastMessage?.CreatedAt,
                UnreadCount = unreadCount
            };
        }

        private static int NormalizePageSize(int requested, int defaultSize, int maxSize)
        {
            if (requested <= 0)
            {
                return defaultSize;
            }

            return Math.Min(requested, maxSize);
        }

        private async Task<MessageReactionResponse> UpdateReactionAsync(Guid userId, int conversationId, int messageId, MessageReaction? reaction, CancellationToken cancellationToken)
        {
            var isMember = await IsUserInConversationAsync(userId, conversationId, cancellationToken);
            if (!isMember)
            {
                throw new UnauthorizedAccessException("You do not have access to this conversation.");
            }

            var messageRecipient = await _unitOfWork.Conversations.GetMessageRecipientAsync(userId, conversationId, messageId);
            if (messageRecipient == null)
            {
                throw new KeyNotFoundException("Message not found in this conversation.");
            }

            var now = DateTime.UtcNow;
            messageRecipient.MessageReaction = reaction;
            messageRecipient.UpdatedAt = now;

            _unitOfWork.Conversations.UpdateMessageRecipient(messageRecipient);
            await _unitOfWork.CommitAsync();

            return new MessageReactionResponse
            {
                ConversationId = conversationId,
                MessageId = messageId,
                UserId = userId,
                Reaction = reaction,
                UpdatedAt = now
            };
        }
    }
}
