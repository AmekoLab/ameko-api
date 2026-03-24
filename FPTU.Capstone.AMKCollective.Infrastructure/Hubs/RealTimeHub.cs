using FPTU.Capstone.AMKCollective.Domain.Entities;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Hubs
{
    public class RealTimeHub : Hub
    {
        private readonly ConnectionMapping<Guid> _connections = new();

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
                await base.OnConnectedAsync();
            }
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
                await base.OnDisconnectedAsync(exception);
            }
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

        #endregion
    }
}
