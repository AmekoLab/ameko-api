using FPTU.Capstone.AMKCollective.Application.DTOs.Chat;
using FPTU.Capstone.AMKCollective.Application.Helpers;
using FPTU.Capstone.AMKCollective.Domain.Enums;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Services
{
    public interface IChatService
    {
        Task<ConversationResponse> GetOrCreateDirectConversationAsync(Guid currentUserId, Guid targetUserId, CancellationToken cancellationToken = default);
        Task<ChatMessageResponse> SendMessageAsync(Guid senderId, SendMessageRequest request, CancellationToken cancellationToken = default);
        Task<CursorPagedResult<ConversationResponse>> GetUserConversationsAsync(Guid userId, string? cursor, int pageSize, CancellationToken cancellationToken = default);
        Task<CursorPagedResult<ChatMessageResponse>> GetConversationMessagesAsync(Guid userId, int conversationId, string? cursor, int pageSize, CancellationToken cancellationToken = default);
        Task MarkMessagesAsReadAsync(Guid userId, int conversationId, int upToMessageId, CancellationToken cancellationToken = default);
        Task<MessageReactionResponse> SetMessageReactionAsync(Guid userId, int conversationId, int messageId, MessageReaction? reaction, CancellationToken cancellationToken = default);
        Task<bool> IsUserInConversationAsync(Guid userId, int conversationId, CancellationToken cancellationToken = default);
    }
}
