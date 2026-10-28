using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using FluentValidation;
using MediatR;
using static Application.Features.Diwan.DiwanSupport;

namespace Application.Features.Diwan
{
    public record RegisterIncomingCommand(RegisterIncomingRequestDto Dto) : IRequest<CorrespondenceResponseDto>;

    public class RegisterIncomingCommandValidator : AbstractValidator<RegisterIncomingCommand>
    {
        public RegisterIncomingCommandValidator()
        {
            RuleFor(x => x.Dto.TypeId).GreaterThan(0).WithMessage("نوع المعاملة مطلوب");
            RuleFor(x => x.Dto).Custom((dto, context) =>
            {
                if (DiwanRules.ValidateRegistration(dto.Party, dto.Subject, dto.Summary, dto.ExternalNumber) is string problem) context.AddFailure(problem);
            });
        }
    }

    /// <summary>
    /// تسجيل وارد: يأخذ رقمه الداخلي، وحالته الابتدائية أول حالة فعّالة بمرحلة «مسجّلة»، وموعده من مهلة نوعه،
    /// وتحمله وحدة المسجِّل (الديوان) إلى أن يحيله. الأثر يُحفظ مع التسجيل في المعاملة نفسها.
    /// </summary>
    public class RegisterIncomingHandler(DiwanContext ctx) : IRequestHandler<RegisterIncomingCommand, CorrespondenceResponseDto>
    {
        public async Task<CorrespondenceResponseDto> Handle(RegisterIncomingCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var dto = request.Dto;
            var branch = DiwanContext.ResolveBranch(viewer, dto.BranchId);
            DiwanRules.EnsureCanRegister(viewer, branch);

            var type = await ctx.ResolveTypeAsync(branch, dto.TypeId);
            var initial = (await ctx.Diwan.GetStatusesAsync(branch, false)).FirstOrDefault(s => s.Stage == CorrespondenceStage.Registered)
                ?? throw new InvalidOperationException("عرّف حالة بمرحلة «مسجّلة» لهذا الفرع من إعدادات الديوان قبل تسجيل المعاملات");
            var project = await ctx.ResolveProjectAsync(viewer, dto.ProjectId, branch);

            // الوحدة الحاملة = وحدة المسجِّل نفسه إن كان من هذا الفرع
            var inBranch = OrganizationRole.InBranch(viewer.User, branch);
            var now = DateTime.UtcNow;
            var external = (dto.ExternalNumber ?? string.Empty).Trim();
            var c = new Correspondence
            {
                Direction = CorrespondenceDirection.Incoming,
                BranchId = branch,
                DepartmentId = inBranch ? viewer.User.DepartmentId : null,
                Year = DateTime.Today.Year,
                ExternalNumber = external.Length == 0 ? null : external,
                ExternalDate = dto.ExternalDate?.Date,
                Party = dto.Party.Trim(),
                Subject = dto.Subject.Trim(),
                Summary = (dto.Summary ?? string.Empty).Trim(),
                TypeId = type.Id,
                StatusId = initial.Id,
                ProjectId = project?.Id,
                CurrentDepartmentId = inBranch ? viewer.User.DepartmentId : null,
                CurrentOfficeId = inBranch ? viewer.User.OfficeId : null,
                DueDate = DiwanRules.DueDate(DateTime.Today, type.DeadlineDays),
                RegisteredByUserId = viewer.Id,
                RegisteredAt = now,
                UpdatedAt = now
            };

            var saved = await ctx.Diwan.AddAsync(c, new AuditEntry(viewer.Id, AuditActions.Created, AuditEntities.Correspondence, null,
                $"تسجيل وارد «{c.Subject}»", Lines($"الجهة: {c.Party}", $"النوع: {type.Name}", project is null ? null : $"المشروع: {project.Name}")));
            return await ctx.DetailAsync(viewer, saved.Id);
        }
    }
}
