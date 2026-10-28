using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.Projects;
using Application.Interfaces;
using Domain.Entities;
using FluentValidation;
using MediatR;
using static Application.Features.Schedules.ScheduleSupport;

namespace Application.Features.Schedules
{
    public record SaveStartOrderCommand(int ProjectId, StartOrderRequestDto Dto) : IRequest<StartOrderResponseDto>;

    public class SaveStartOrderCommandValidator : AbstractValidator<SaveStartOrderCommand>
    {
        public SaveStartOrderCommandValidator()
        {
            RuleFor(x => x.Dto.OrderNumber).NotEmpty().WithMessage("رقم أمر المباشرة مطلوب").MaximumLength(50).WithMessage("رقم الأمر لا يتجاوز 50 حرفاً");
            RuleFor(x => x.Dto.Notes).MaximumLength(2000).WithMessage("الملاحظات لا تتجاوز 2000 حرف");
        }
    }

    public class SaveStartOrderHandler(ScheduleContext ctx) : IRequestHandler<SaveStartOrderCommand, StartOrderResponseDto>
    {
        public async Task<StartOrderResponseDto> Handle(SaveStartOrderCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var project = await ctx.LoadProjectAsync(viewer, request.ProjectId);
            ProjectRules.EnsureOpen(project);
            var dto = request.Dto;

            if (ScheduleRules.ValidateStartOrder(dto.OrderNumber, dto.OrderDate, dto.StartDate, dto.CompletionDays, dto.ExecutionDays, dto.StudyCost) is string problem)
                throw new InvalidOperationException(problem);

            // مهندس الموقع ومشرف القطاع من مشاركي المشروع
            var engineer = MemberOrNull(project, dto.SiteEngineerUserId, "مهندس الموقع");
            var supervisor = MemberOrNull(project, dto.SectorSupervisorUserId, "مشرف القطاع");

            var existing = await ctx.Schedules.GetStartOrderAsync(project.Id);
            var isNew = existing == null;
            var order = existing ?? new ProjectStartOrder { ProjectId = project.Id, CreatedByUserId = viewer.Id, CreatedAt = DateTime.UtcNow };

            var changes = new List<string>();
            void Track(string label, string? from, string? to)
            {
                if (!string.Equals(from ?? string.Empty, to ?? string.Empty, StringComparison.Ordinal))
                    changes.Add($"{label}: {(string.IsNullOrEmpty(from) ? "—" : from)} ← {(string.IsNullOrEmpty(to) ? "—" : to)}");
            }

            var number = dto.OrderNumber.Trim();
            var notes = (dto.Notes ?? string.Empty).Trim();
            Track("رقم الأمر", order.OrderNumber, number);
            Track("تاريخ الأمر", isNew ? null : Format(order.OrderDate), Format(dto.OrderDate));
            Track("تاريخ المباشرة", isNew ? null : Format(order.StartDate), Format(dto.StartDate));
            Track("مدة الإنجاز (يوم)", isNew ? null : order.CompletionDays.ToString(), dto.CompletionDays.ToString());
            Track("زمن التنفيذ (يوم)", isNew ? null : order.ExecutionDays.ToString(), dto.ExecutionDays.ToString());
            Track("تكلفة الدراسة", order.StudyCost?.ToString("0.##"), dto.StudyCost?.ToString("0.##"));
            Track("مهندس الموقع", order.SiteEngineer?.FullName, engineer?.FullName);
            Track("مشرف القطاع", order.SectorSupervisor?.FullName, supervisor?.FullName);
            if (order.Notes != notes) changes.Add("الملاحظات: تغيّرت");

            if (!isNew && changes.Count == 0) return ToStartOrderDto(order);

            order.OrderNumber = number;
            order.OrderDate = dto.OrderDate.Date;
            order.StartDate = dto.StartDate.Date;
            order.CompletionDays = dto.CompletionDays;
            order.ExecutionDays = dto.ExecutionDays;
            order.StudyCost = dto.StudyCost;
            order.SiteEngineerUserId = engineer?.Id;
            order.SiteEngineer = engineer;
            order.SectorSupervisorUserId = supervisor?.Id;
            order.SectorSupervisor = supervisor;
            order.Notes = notes;

            await ctx.Schedules.SaveStartOrderAsync(order, isNew, dto.RowVersion, new AuditEntry(viewer.Id,
                isNew ? AuditActions.Created : AuditActions.Updated, AuditEntities.StartOrder, null,
                $"{(isNew ? "تسجيل" : "تعديل")} أمر المباشرة للمشروع «{project.Name}»", string.Join("\n", changes)), project.BranchId);

            return ToStartOrderDto(await ctx.Schedules.GetStartOrderAsync(project.Id) ?? order);
        }

        private static User? MemberOrNull(Project project, int? userId, string role)
        {
            if (userId is not int id) return null;
            return project.Members.FirstOrDefault(m => m.UserId == id)?.User
                ?? throw new InvalidOperationException($"{role} يُختار من مشاركي المشروع — أضفه مشاركاً أولاً");
        }
    }
}
