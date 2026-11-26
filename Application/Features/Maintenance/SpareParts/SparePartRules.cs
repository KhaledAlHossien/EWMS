using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.Maintenance;
using FluentValidation;

namespace Application.Features.Maintenance.SpareParts
{
    /// <summary>
    /// حدود مخزون القطع (قرار المستخدم 2026-10-05): مخزون لكل قسم — يرى الموظف ويدير قطع قسمه فقط، ومدير النظام كل الأقسام.
    /// الصرف على طلب: حدّ IssueSparePart كحدّ التعديل (طلباته، وطلبات قسمه لمن يملك ViewDepartmentMaintenance)،
    /// ومن مخزون قسم الطلب حصراً، والطلب مفتوح.
    /// </summary>
    public static class SparePartRules
    {
        public const int MaxQuantity = 1_000_000_000;

        /// <summary>قسم المخزون للمستخدم: null = كل الأقسام (مدير النظام)</summary>
        public static int? Department(Viewer v) =>
            v.IsSuperAdmin ? null
            : v.User.DepartmentId ?? throw new InvalidOperationException("حسابك لا يتبع لقسم له مخزون قطع غيار");

        public static void EnsureIn(Viewer v, SparePart part)
        {
            if (!v.IsSuperAdmin && part.DepartmentId != v.User.DepartmentId)
                throw new UnauthorizedAccessException("هذه القطعة ليست في مخزون قسمك");
        }

        public static bool TwoDecimals(decimal value) => decimal.Round(value, 2) == value;

        public static bool CrossedBelowMinimum(SparePart part, StockChange change) =>
            part.MinQuantity > 0 && change.Before >= part.MinQuantity && change.After < part.MinQuantity;

        public static string MovementAr(SparePartMovementType type) => type switch
        {
            SparePartMovementType.Receive => "إدخال",
            SparePartMovementType.Issue => "صرف",
            SparePartMovementType.Return => "إرجاع",
            SparePartMovementType.Adjust => "تسوية",
            _ => type.ToString()
        };

        public static string Amount(decimal value) => value.ToString("0.##");

        public static SparePartResponseDto ToDto(SparePart p) => new()
        {
            Id = p.Id,
            DepartmentId = p.DepartmentId,
            DepartmentName = p.Department?.Name ?? string.Empty,
            Name = p.Name,
            PartNumber = p.PartNumber,
            Unit = p.Unit,
            Description = p.Description,
            Quantity = p.Quantity,
            MinQuantity = p.MinQuantity,
            AverageCost = p.AverageCost,
            StockValue = Math.Round(p.Quantity * p.AverageCost, 2),
            IsLow = p.MinQuantity > 0 && p.Quantity < p.MinQuantity,
            DeviceTypes = p.DeviceTypes.Where(t => t.DeviceType != null).Select(t => new NamedRefDto { Id = t.DeviceTypeId, Name = t.DeviceType.Name }).OrderBy(x => x.Name).ToList(),
            DeviceCompanies = p.DeviceCompanies.Where(c => c.DeviceCompany != null).Select(c => new NamedRefDto { Id = c.DeviceCompanyId, Name = c.DeviceCompany.Name }).OrderBy(x => x.Name).ToList(),
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt
        };
    }
}
