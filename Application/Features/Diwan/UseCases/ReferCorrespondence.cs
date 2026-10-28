using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using MediatR;
using static Application.Features.Diwan.DiwanSupport;

namespace Application.Features.Diwan
{
    public record ReferCorrespondenceCommand(int Id, ReferCorrespondenceRequestDto Dto) : IRequest<CorrespondenceResponseDto>;
    public record AcknowledgeCorrespondenceCommand(int Id) : IRequest<CorrespondenceResponseDto>;

    public class ReferCorrespondenceCommandValidator : AbstractValidator<ReferCorrespondenceCommand>
    {
        public ReferCorrespondenceCommandValidator()
        {
            RuleFor(x => x.Dto.ToDepartmentId).GreaterThan(0).WithMessage("اختر القسم المحال إليه");
            RuleFor(x => x.Dto.Note).MaximumLength(1000).WithMessage("ملاحظة الإحالة لا تتجاوز 1000 حرف");
        }
    }

    /// <summary>
    /// إحالة المعاملة التي تحملها وحدتي إلى قسم من فرعها (ومعه مكتب وموظف اختياريان). الجهة الجديدة تصير الحاملة،
    /// وتُشعَر من يعالجها فيها. سجل الإحالات لا يُعدَّل. الإحالة لا تفتح المعاملة لكل موظفي القسم (القاعدة 8).
    /// </summary>
    public class ReferCorrespondenceHandler(DiwanContext ctx) : IRequestHandler<ReferCorrespondenceCommand, CorrespondenceResponseDto>
    {
        public async Task<CorrespondenceResponseDto> Handle(ReferCorrespondenceCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var dto = request.Dto;
            var c = await ctx.LoadAsync(viewer, request.Id);
            DiwanRules.EnsureCanRefer(viewer, c);

            var department = await ctx.Departments.GetByIdAsync(dto.ToDepartmentId) ?? throw new KeyNotFoundException("القسم المحال إليه غير موجود");
            if (department.BranchId != c.BranchId) throw new InvalidOperationException("تُحال المعاملة داخل فرعها فقط");

            Office? office = null;
            if (dto.ToOfficeId is int officeId)
            {
                office = await ctx.Offices.GetByIdAsync(officeId) ?? throw new KeyNotFoundException("المكتب المحال إليه غير موجود");
                if (office.DepartmentId != department.Id) throw new InvalidOperationException("المكتب المختار لا يتبع القسم المحال إليه");
            }

            User? person = null;
            if (dto.ToUserId is int userId)
            {
                person = await ctx.Users.GetByIdAsync(userId) ?? throw new KeyNotFoundException("الموظف المحال إليه غير موجود");
                if (!person.IsActive || person.DepartmentId != department.Id || (office != null && person.OfficeId != office.Id))
                    throw new InvalidOperationException("الموظف المختار لا يتبع الجهة المحال إليها أو حسابه معطّل");
                if (!await ctx.Permissions.HasAsync(person.Id, AppPermissions.HandleCorrespondence))
                    throw new InvalidOperationException("الموظف المختار لا يملك صلاحية معالجة المعاملات");
            }

            if (c.CurrentDepartmentId == department.Id && c.CurrentOfficeId == office?.Id && c.CurrentUserId == person?.Id)
                throw new InvalidOperationException("المعاملة عند هذه الجهة أصلاً");

            var note = (dto.Note ?? string.Empty).Trim();
            var referral = new CorrespondenceReferral
            {
                CorrespondenceId = c.Id,
                FromUserId = viewer.Id,
                FromDepartmentId = viewer.User.DepartmentId,
                ToDepartmentId = department.Id,
                ToOfficeId = office?.Id,
                ToUserId = person?.Id,
                Note = note,
                CreatedAt = DateTime.UtcNow
            };

            // الجهة الحاملة الجديدة (مفاتيح فقط: لا إسناد لكائنات التنقل على كيان متتبَّع)
            c.CurrentDepartmentId = department.Id;
            c.CurrentOfficeId = office?.Id;
            c.CurrentUserId = person?.Id;
            c.DueSoonNotifiedAt = null;      // الجهة الجديدة تُنبَّه إن اقترب الموعد أو مضى (لا يعرف حاملها السابق ما نُبّه عنه)
            c.OverdueNotifiedAt = null;

            var target = person is null ? (office?.Name ?? department.Name) : $"{person.FullName} — {office?.Name ?? department.Name}";
            await ctx.Diwan.AddReferralAsync(c, referral, new AuditEntry(viewer.Id, AuditActions.Referred, AuditEntities.Correspondence, c.Id,
                $"إحالة المعاملة {Label(c)} إلى {target}", note));

            var recipients = await ctx.HandlerIdsAsync(department.Id, office?.Id, person?.Id);
            await ctx.NotifyAsync(recipients, viewer.Id, c, NotificationType.CorrespondenceReferred, "معاملة محالة إليك",
                $"أحال {viewer.User.FullName} المعاملة «{c.Subject}» إلى {target}" + (note.Length > 0 ? $": {note}" : string.Empty));

            return await ctx.DetailAsync(viewer, c.Id);
        }
    }

    /// <summary>تأكيد استلام آخر إحالة بانتظار الاستلام، ممّن تحمل وحدته المعاملة. يُسجَّل مرة واحدة ولا يغيّر الحالة.</summary>
    public class AcknowledgeCorrespondenceHandler(DiwanContext ctx) : IRequestHandler<AcknowledgeCorrespondenceCommand, CorrespondenceResponseDto>
    {
        public async Task<CorrespondenceResponseDto> Handle(AcknowledgeCorrespondenceCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var c = await ctx.LoadAsync(viewer, request.Id);
            DiwanRules.EnsureOpenIncoming(c);
            if (!DiwanRules.CanHandle(viewer, c)) throw new UnauthorizedAccessException("لا تحمل وحدتك هذه المعاملة الآن");

            var pending = DiwanRules.PendingAcknowledgement(c) ?? throw new InvalidOperationException("لا إحالة بانتظار الاستلام");
            await ctx.Diwan.AcknowledgeAsync(c, pending, viewer.Id,
                new AuditEntry(viewer.Id, AuditActions.Acknowledged, AuditEntities.Correspondence, c.Id, $"استلام المعاملة {Label(c)}"));
            return await ctx.DetailAsync(viewer, c.Id);
        }
    }
}
