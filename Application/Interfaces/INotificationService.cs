using Domain.Entities;

namespace Application.Interfaces
{
    public interface INotificationService
    {
        Task AddAsync(Notification notification);
        Task AddRangeAsync(IEnumerable<Notification> notifications);

        Task<Notification?> GetByIdAsync(int id);
        Task<List<Notification>> GetByUserIdAsync(int userId, bool unreadOnly = false);
        Task<int> GetUnreadCountAsync(int userId);

        Task<bool> MarkAsReadAsync(Notification notification);
        Task<bool> MarkAllAsReadAsync(int userId);

        Task<bool> SaveChangesAsync();
    }
}
