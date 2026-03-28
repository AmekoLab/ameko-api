using FPTU.Capstone.AMKCollective.Application.DTOs.Chat;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using FPTU.Capstone.AMKCollective.Infrastructure.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Services
{
    public class ChatRealtimePublisherService : IChatRealtimePublisher
    {
        private readonly IHubContext<RealTimeHub> _hubContext;
        private readonly ILogger<ChatRealtimePublisherService> _logger;

        public ChatRealtimePublisherService(IHubContext<RealTimeHub> hubContext, ILogger<ChatRealtimePublisherService> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        public Task PublishMessageReceivedAsync(ChatMessageResponse message, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation(
                "Publishing {EventName} to {GroupName}. ConversationId={ConversationId}, MessageId={MessageId}, SenderId={SenderId}",
                "messageReceived",
                BuildConversationGroupName(message.ConversationId),
                message.ConversationId,
                message.Id,
                message.SenderId);

            return _hubContext.Clients
                .Group(BuildConversationGroupName(message.ConversationId))
                .SendAsync("messageReceived", message, cancellationToken);
        }

        public Task PublishReadReceiptAsync(int conversationId, Guid userId, int upToMessageId, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation(
                "Publishing {EventName} to {GroupName}. ConversationId={ConversationId}, UserId={UserId}, UpToMessageId={UpToMessageId}",
                "readReceipt",
                BuildConversationGroupName(conversationId),
                conversationId,
                userId,
                upToMessageId);

            return _hubContext.Clients
                .Group(BuildConversationGroupName(conversationId))
                .SendAsync("readReceipt", new
                {
                    ConversationId = conversationId,
                    UserId = userId,
                    UpToMessageId = upToMessageId
                }, cancellationToken);
        }

        public Task PublishReactionChangedAsync(MessageReactionResponse reaction, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation(
                "Publishing {EventName} to {GroupName}. ConversationId={ConversationId}, MessageId={MessageId}, UserId={UserId}, Reaction={Reaction}",
                "reactionChanged",
                BuildConversationGroupName(reaction.ConversationId),
                reaction.ConversationId,
                reaction.MessageId,
                reaction.UserId,
                reaction.Reaction);

            return _hubContext.Clients
                .Group(BuildConversationGroupName(reaction.ConversationId))
                .SendAsync("reactionChanged", reaction, cancellationToken);
        }

        private static string BuildConversationGroupName(int conversationId)
        {
            return $"conversation:{conversationId}";
        }
    }
}
