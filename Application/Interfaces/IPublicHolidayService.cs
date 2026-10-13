using Domain.Entities;

namespace Application.Interfaces
{
    public interface IPublicHolidayService
    {
        /// <summary>العطل التي تقع (أو جزء منها) في السنة</summary>
        Task<List<PublicHoliday>> GetAllAsync(int? year = null);
        /// <summary>العطل التي تتقاطع مع الفترة (شاملة)</summary>
        Task<List<PublicHoliday>> GetBetweenAsync(DateTime start, DateTime end);
        Task<PublicHoliday?> GetByIdAsync(int id);
        Task AddAsync(PublicHoliday holiday);
        Task UpdateAsync(PublicHoliday holiday);
        Task DeleteAsync(PublicHoliday holiday);

        /// <summary>أول عطلة أخرى تتداخل مع الفترة (excludeId للتعديل) — العطل لا تتداخل</summary>
        Task<PublicHoliday?> FindOverlapAsync(DateTime start, DateTime end, int? excludeId = null);

        /// <summary>تواريخ أيام العطل الرسمية بين تاريخين (شاملة) — لحساب أيام الإجازة</summary>
        Task<IReadOnlySet<DateTime>> GetDatesAsync(DateTime start, DateTime end);
    }
}
