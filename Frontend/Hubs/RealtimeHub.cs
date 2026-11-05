using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace ServerCSharp.Hubs
{
    public class RealtimeHub : Hub
    {
        // Count active concurrent readers per story room
        private static readonly ConcurrentDictionary<string, int> _roomViewers = new();

        public async Task JoinStoryRoom(string storyId)
        {
            var roomKey = $"story_{storyId}";
            await Groups.AddToGroupAsync(Context.ConnectionId, roomKey);
            
            _roomViewers.AddOrUpdate(roomKey, 1, (_, count) => count + 1);
            int currentViewers = _roomViewers.GetValueOrDefault(roomKey, 1);

            await Clients.Group(roomKey).SendAsync("UpdateActiveReaders", currentViewers);
        }

        public async Task LeaveStoryRoom(string storyId)
        {
            var roomKey = $"story_{storyId}";
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomKey);

            _roomViewers.AddOrUpdate(roomKey, 0, (_, count) => Math.Max(0, count - 1));
            int currentViewers = _roomViewers.GetValueOrDefault(roomKey, 0);

            await Clients.Group(roomKey).SendAsync("UpdateActiveReaders", currentViewers);
        }

        public async Task SendCommentRealtime(string storyId, string userName, string userAvatar, string userRole, string content)
        {
            var roomKey = $"story_{storyId}";
            await Clients.Group(roomKey).SendAsync("ReceiveComment", new
            {
                userName,
                userAvatar,
                userRole,
                content,
                createdAt = DateTime.Now.ToString("HH:mm - dd/MM/yyyy")
            });
        }

        public override async Task OnConnectedAsync()
        {
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            await base.OnDisconnectedAsync(exception);
        }
    }
}
