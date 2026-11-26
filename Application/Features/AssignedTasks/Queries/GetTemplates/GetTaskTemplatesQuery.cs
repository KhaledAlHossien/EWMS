using Application.DTOs.Response;
using MediatR;

namespace Application.Features.AssignedTasks.Queries.GetTemplates
{
    // قوالب المهام والمهام الدورية والروابط (قرار المستخدم 2026-10-08). كلها لمن يملك صلاحية إسناد (AssignTaskTo*)،
    // والقالب والتكرار خاصان بصاحبهما، والرابط يحدده حق المستخدم في عرض السجل المرتبط (TaskLinkResolver).

    public record GetTaskTemplatesQuery : IRequest<List<TaskTemplateDto>>;
}
