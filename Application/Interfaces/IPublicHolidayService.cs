using Domain.Entities;

namespace Application.Interfaces
{
    public interface IPublicHolidayService
    {
        Task<List<PublicHoliday>> GetAllAsync(int? year = null);
        Task<List<PublicHoliday>> GetBetweenAsync(DateTime start, DateTime end);
        Task<PublicHoliday?> GetByIdAsync(int id);
        Task AddRangeAsync(IEnumerable<PublicHoliday> holidays);
        Task UpdateAsync(PublicHoliday holiday);
        Task DeleteAsync(PublicHoliday holiday);

        /// <summary>أي من هذه التواريخ مسجّل مسبقاً كعطلة (excludeId للتعديل)</summary>
        Task<List<DateTime>> ExistingDatesAsync(IEnumerable<DateTime> dates, int? excludeId = null);

        /// <summary>تواريخ العطل الرسمية بين تاريخين (شاملة) — لحساب أيام الإجازة</summary>
        Task<IReadOnlySet<DateTime>> GetDatesAsync(DateTime start, DateTime end);
    }
}
