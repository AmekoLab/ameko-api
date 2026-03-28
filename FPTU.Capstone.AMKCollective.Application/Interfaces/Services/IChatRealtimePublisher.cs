using FPTU.Capstone.AMKCollective.Application.DTOs.Chat;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Services
{
    public interface IChatRealtimePublisher
    {
        Task PublishMessageReceivedAsync(ChatMessageResponse message, CancellationToken cancellationToken = default);
        Task PublishReadReceiptAsync(int conversationId, Guid userId, int upToMessageId, CancellationToken cancellationToken = default);
        Task PublishReactionChangedAsync(MessageReactionResponse reaction, CancellationToken cancellationToken = default);
    }
}
