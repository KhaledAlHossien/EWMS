using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.Projects;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using MediatR;
using static Application.Features.Schedules.ScheduleChangeSupport;

namespace Application.Features.Schedules
{
    public record DeleteScheduleExtensionCommand(int Id) : IRequest<Unit>;


    public class DeleteScheduleExtensionHandler(ScheduleChangeContext ctx) : IRequestHandler<DeleteScheduleExtensionCommand, Unit>
    {
        public async Task<Unit> Handle(DeleteScheduleExtensionCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var extension = await ctx.LoadExtensionAsync(viewer, request.Id);
            ProjectRules.EnsureOpen(extension.Project);
            EnsureRequesterDraft(viewer, extension);

            await ctx.Changes.DeleteExtensionAsync(extension, new AuditEntry(viewer.Id, AuditActions.Deleted, AuditEntities.Extension, extension.Id,
                $"حذف مسودة طلب تمديد لجدول «{extension.Project.Name}»", $"البنود: {extension.Items.Count}"), extension.Project.BranchId);
            return Unit.Value;
        }
    }
}
