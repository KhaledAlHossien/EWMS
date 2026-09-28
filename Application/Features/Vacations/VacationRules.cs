using Domain.Enums;

namespace Application.Features.Vacations
{
    public static class VacationRules
    {
        /// <summary>الحد الشهري للأيام المدفوعة — مشترك بين كل أنواع الإجازات المدفوعة</summary>
        public const int MaxPaidVacationDaysPerMonth = 2;

        /// <summary>اسم الحالة بالعربي — مصدر واحد للـ AutoMapper والداشبورد</summary>
        public static string StatusAr(VacationStatus status) => status switch
        {
            VacationStatus.PendingManager => "بانتظار رئيس القسم",
            VacationStatus.PendingBranchManager => "بانتظار رئيس الفرع",
            VacationStatus.Approved => "معتمدة",
            VacationStatus.Rejected => "مرفوضة",
            VacationStatus.Cancelled => "ملغاة",
            _ => "غير معروفة"
        };
    }
}
