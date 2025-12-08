using inflan_api.Models;

namespace inflan_api.Interfaces
{
    public interface INotificationRepository
    {
        Task<Notification> CreateAsync(Notification notification);
        Task<Notification?> GetByIdAsync(int id);
        Task<IEnumerable<Notification>> GetByUserIdAsync(int userId, int page = 1, int pageSize = 20);
        Task<int> GetUnreadCountAsync(int userId);
        Task<int> GetTotalCountAsync(int userId);
        Task<bool> MarkAsReadAsync(int notificationId, int userId);
        Task<bool> MarkAllAsReadAsync(int userId);
        Task<bool> MarkMultipleAsReadAsync(List<int> notificationIds, int userId);
        Task<bool> DeleteAsync(int notificationId, int userId);
        Task<bool> DeleteAllAsync(int userId);
    }
}
