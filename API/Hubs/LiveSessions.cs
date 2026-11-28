using Application.Common;
using Application.Interfaces;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Security.Claims;

namespace API.Hubs
{
    /// <summary>
    /// سجل اتصالات قناة الإشعارات المفتوحة على هذا الخادم (singleton) لقطعها عند إبطال الجلسة.
    /// خادم واحد: لو شُغّل أكثر من نسخة فلكل نسخة اتصالاتها.
    /// </summary>
    public class LiveSessions : ILiveSessions
    {
        private readonly ConcurrentDictionary<string, HubCallerContext> _connections = new();

        public void Add(HubCallerContext connection) => _connections[connection.ConnectionId] = connection;

        public void Remove(string connectionId) => _connections.TryRemove(connectionId, out _);

        public void CloseUser(int userId) => Close(ClaimTypes.NameIdentifier, userId.ToString());

        public void CloseToken(string tokenHash) => Close(TokenHash.ClaimType, tokenHash);

        private void Close(string claimType, string value)
        {
            foreach (var connection in _connections.Values)
                if (connection.User?.FindFirst(claimType)?.Value == value)
                    connection.Abort();
        }
    }
}
