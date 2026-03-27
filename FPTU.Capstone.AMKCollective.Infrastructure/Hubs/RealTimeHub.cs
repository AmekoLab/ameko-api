using FPTU.Capstone.AMKCollective.Application.DTOs.Chat;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Hubs
{
    [Authorize]
    public class RealTimeHub : Hub
    {
        private readonly IChatService _chatService;
        private readonly ConnectionMapping<Guid> _connections = new();

        public RealTimeHub(IChatService chatService)
        {
            _chatService = chatService;
        }

        /// <summary>
        /// When a client connects to the hub, this method is called. It retrieves the current user's ID from the claims and adds the connection ID to the mapping for that user. This allows the server to keep track of which connections belong to which users, enabling targeted real-time communication in the future.
        /// </summary>
        /// <returns></returns>
        public override async Task OnConnectedAsync()
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId.HasValue)
            {
                var connectionId = Context.ConnectionId;
                _connections.Add(currentUserId.Value, connectionId);
            }

            await base.OnConnectedAsync();
        }

        /// <summary>
        /// If a client disconnects from the hub, this method is called. It retrieves the current user's ID from the claims and removes the connection ID from the mapping for that user. This ensures that the server's tracking of active connections remains accurate, preventing attempts to send messages to connections that are no longer active.
        /// </summary>
        /// <param name="exception"></param>
        /// <returns></returns>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId.HasValue)
            {
                var connectionId = Context.ConnectionId;
                _connections.Remove(currentUserId.Value, connectionId);
            }

            await base.OnDisconnectedAsync(exception);
        }

        public async Task JoinConversation(int conversationId)
        {
            var currentUserId = GetCurrentUserId();
            if (!currentUserId.HasValue)
            {
                throw new HubException("Unauthorized.");
            }

            var canAccess = await _chatService.IsUserInConversationAsync(currentUserId.Value, conversationId);
            if (!canAccess)
            {
                throw new HubException("You do not have access to this conversation.");
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, BuildConversationGroupName(conversationId));
        }

        public async Task LeaveConversation(int conversationId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, BuildConversationGroupName(conversationId));
        }

        public async Task SendMessage(SendMessageRequest request)
        {
            var currentUserId = GetCurrentUserId();
            if (!currentUserId.HasValue)
            {
                throw new HubException("Unauthorized.");
            }

            var message = await _chatService.SendMessageAsync(currentUserId.Value, request);
            await Clients.Group(BuildConversationGroupName(message.ConversationId)).SendAsync("messageReceived", message);
        }

        public async Task Typing(int conversationId, bool isTyping)
        {
            var currentUserId = GetCurrentUserId();
            if (!currentUserId.HasValue)
            {
                throw new HubException("Unauthorized.");
            }

            var canAccess = await _chatService.IsUserInConversationAsync(currentUserId.Value, conversationId);
            if (!canAccess)
            {
                throw new HubException("You do not have access to this conversation.");
            }

            await Clients.OthersInGroup(BuildConversationGroupName(conversationId))
                .SendAsync("typingChanged", new
                {
                    ConversationId = conversationId,
                    UserId = currentUserId.Value,
                    IsTyping = isTyping
                });
        }

        public async Task MarkRead(int conversationId, int messageId)
        {
            var currentUserId = GetCurrentUserId();
            if (!currentUserId.HasValue)
            {
                throw new HubException("Unauthorized.");
            }

            await _chatService.MarkMessagesAsReadAsync(currentUserId.Value, conversationId, messageId);
            await Clients.Group(BuildConversationGroupName(conversationId)).SendAsync("readReceipt", new
            {
                ConversationId = conversationId,
                UserId = currentUserId.Value,
                UpToMessageId = messageId
            });
        }

        public async Task SetReaction(int conversationId, int messageId, MessageReaction? reaction)
        {
            var currentUserId = GetCurrentUserId();
            if (!currentUserId.HasValue)
            {
                throw new HubException("Unauthorized.");
            }

            var result = await _chatService.SetMessageReactionAsync(currentUserId.Value, conversationId, messageId, reaction);
            await Clients.Group(BuildConversationGroupName(conversationId)).SendAsync("reactionChanged", result);
        }

        #region Helper

        private Guid? GetCurrentUserId()
        {
            var identity = Context.User?.Identity as ClaimsIdentity;
            var accountIdClaim = identity?.FindFirst("accountId")
                                    ?? identity.FindFirst("nameid")
                                    ?? identity.FindFirst("sub")
                                    ?? identity.FindFirst("id");
            if (accountIdClaim != null && Guid.TryParse(accountIdClaim.Value, out var currentUserId)) return currentUserId;

            return null;
        }

        private static string BuildConversationGroupName(int conversationId)
        {
            return $"conversation:{conversationId}";
        }

        #endregion
    }
}
