using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Map.Queries.GetBranchMap
{
    /// <summary>
    /// خريطة الفرع في لوحة المتابعة (Role-Permission، 2026-10-03):
    /// - SuperAdmin يتصفح خريطة أي فرع.
    /// - من يملك ViewBranchMap يرى خريطة فرعه فقط ببيانات فرعه (ولا يرى فروعاً أخرى).
    /// </summary>
    public record GetBranchMapQuery(int BranchId) : IRequest<BranchMapDto>;
}
