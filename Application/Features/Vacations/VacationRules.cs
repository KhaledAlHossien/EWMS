using Domain.Entities;
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
            VacationStatus.PendingManager => "بانتظار الموافقة الأولى",
            VacationStatus.PendingBranchManager => "بانتظار الاعتماد النهائي",
            VacationStatus.Approved => "معتمدة",
            VacationStatus.Rejected => "مرفوضة",
            VacationStatus.Cancelled => "ملغاة",
            _ => "غير معروفة"
        };

        /// <summary>
        /// تصحيح التاريخ الهجري في نموذج الطباعة بالأيام (تقويم أم القرى + هذا الفرق). نموذج المؤسسة الورقي
        /// كتب 1448/4/12 لتاريخ 2026/9/22 بينما أم القرى تعطي 1448/4/11، فالفرق +1. عدّله إن اختلفت رؤية الهلال.
        /// </summary>
        public const int HijriDayOffset = 1;

        /// <summary>رقم الطلب للعرض والطباعة</summary>
        public static string RequestNumber(int id, DateTime createdAt) => $"VAC-{createdAt.Year}-{id:D5}";

        /// <summary>
        /// حالة الدفع بالعربي: تُحدَّد عند الاعتماد النهائي فقط (قرار المستخدم 2026-10-04).
        /// </summary>
        public static string PaymentAr(VacationStatus status, int paidDays, int unpaidDays) => status switch
        {
            VacationStatus.PendingManager or VacationStatus.PendingBranchManager => "يُحدَّد الدفع عند الاعتماد",
            VacationStatus.Approved when unpaidDays == 0 && paidDays == 0 => "لا أيام عمل",
            VacationStatus.Approved when unpaidDays == 0 => "مدفوعة",
            VacationStatus.Approved when paidDays == 0 => "غير مدفوعة",
            VacationStatus.Approved => $"{paidDays} مدفوع و{unpaidDays} غير مدفوع",
            _ => "—"
        };

        public static string PaymentAr(Vacation v) => PaymentAr(v.Status, v.PaidDays, v.UnpaidDays);
    }
}
