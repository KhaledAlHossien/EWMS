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
    public record AddScheduleDependencyCommand(int ScheduleId, ScheduleDependencyRequestDto Dto) : IRequest<ScheduleDependencyResponseDto>;


    public class AddScheduleDependencyHandler(ScheduleContext ctx) : IRequestHandler<AddScheduleDependencyCommand, ScheduleDependencyResponseDto>
    {
        public async Task<ScheduleDependencyResponseDto> Handle(AddScheduleDependencyCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var schedule = await ctx.LoadScheduleAsync(viewer, request.ScheduleId);
            EnsureDraftEditable(schedule);
            var dto = request.Dto;

            var byId = schedule.Tasks.ToDictionary(t => t.Id);
            if (!byId.TryGetValue(dto.PredecessorId, out var predecessor) || !byId.TryGetValue(dto.SuccessorId, out var successor))
                throw new InvalidOperationException("البندان يجب أن يتبعا هذه المسودة");
            if (dto.PredecessorId == dto.SuccessorId)
                throw new InvalidOperationException("لا يعتمد البند على نفسه");

            var existing = await ctx.Schedules.GetDependenciesAsync(schedule.Id);
            if (existing.Any(d => d.PredecessorId == dto.PredecessorId && d.SuccessorId == dto.SuccessorId))
                throw new InvalidOperationException("هذه التبعية مضافة مسبقاً");
            var edges = existing.Select(d => new ScheduleRules.Edge(d.PredecessorId, d.SuccessorId)).ToList();
            if (ScheduleRules.WouldCreateCycle(edges, dto.PredecessorId, dto.SuccessorId))
                throw new InvalidOperationException("هذه التبعية تُنشئ حلقة: يصير البند معتمداً على نفسه بشكل غير مباشر");

            var dependency = new ScheduleDependency { ScheduleId = schedule.Id, PredecessorId = dto.PredecessorId, SuccessorId = dto.SuccessorId };
            await ctx.Schedules.AddDependencyAsync(dependency, new AuditEntry(viewer.Id, AuditActions.Created, AuditEntities.ScheduleDependency, null,
                $"إضافة تبعية في جدول «{schedule.Project.Name}»", $"«{predecessor.Name}» ← «{successor.Name}»"), schedule.Project.BranchId);

            return new ScheduleDependencyResponseDto
            {
                Id = dependency.Id, PredecessorId = dependency.PredecessorId, SuccessorId = dependency.SuccessorId,
                HasConflict = successor.PlannedStart.Date <= predecessor.PlannedEnd.Date
            };
        }
    }
}
