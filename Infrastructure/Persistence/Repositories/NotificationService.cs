using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    public class NotificationService : INotificationService
    {
        private readonly DataContext _context;
        private readonly INotificationPusher _pusher;

        public NotificationService(DataContext context, INotificationPusher pusher)
        {
            _context = context;
            _pusher = pusher;
        }

        public async Task AddAsync(Notification notification)
        {
            await _context.Notifications.AddAsync(notification);
            await SaveChangesAsync();

            // بعد الحفظ (ليكون لدينا Id) نرسله لحظياً لصاحبه إن كان متصلاً
            await _pusher.PushAsync([notification]);
        }

        public async Task AddRangeAsync(IEnumerable<Notification> notifications)
        {
            var list = notifications.ToList();
            if (list.Count == 0) return;

            await _context.Notifications.AddRangeAsync(list);
            await SaveChangesAsync();

            await _pusher.PushAsync(list);
        }

        public async Task<Notification?> GetByIdAsync(int id)
        {
            return await _context.Notifications.FirstOrDefaultAsync(n => n.Id == id);
        }

        public async Task<List<Notification>> GetByUserIdAsync(int userId, bool unreadOnly = false)
        {
            var query = _context.Notifications.Where(n => n.UserId == userId);

            if (unreadOnly)
                query = query.Where(n => !n.IsRead);

            return await query
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();
        }

        public async Task<(List<Notification> Items, int Total)> GetPageByUserIdAsync(int userId, bool unreadOnly, int page, int pageSize)
        {
            var query = _context.Notifications.Where(n => n.UserId == userId);
            if (unreadOnly) query = query.Where(n => !n.IsRead);

            var total = await query.CountAsync();
            var items = await query.AsNoTracking()
                .OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .ToListAsync();
            return (items, total);
        }

        public async Task<int> GetUnreadCountAsync(int userId)
        {
            return await _context.Notifications
                .CountAsync(n => n.UserId == userId && !n.IsRead);
        }

        public async Task<bool> MarkAsReadAsync(Notification notification)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
            _context.Notifications.Update(notification);
            return await SaveChangesAsync();
        }

        public async Task<bool> MarkAllAsReadAsync(int userId)
        {
            var unread = await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ToListAsync();

            if (unread.Count == 0) return false;

            var now = DateTime.UtcNow;
            foreach (var notification in unread)
            {
                notification.IsRead = true;
                notification.ReadAt = now;
            }

            return await SaveChangesAsync();
        }

        public async Task<bool> SaveChangesAsync()
        {
            return (await _context.SaveChangesAsync()) > 0;
        }
    }
}
