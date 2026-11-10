using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.Projects.Commands.RemoveDepartment
{
    public class RemoveStudyProjectDepartmentCommandHandler : IRequestHandler<RemoveStudyProjectDepartmentCommand, StudyProjectDto>
    {
        private readonly IStudyProjectService _service;
        private readonly StudyKit _kit;
        private readonly StudyProjectSupport _support;

        public RemoveStudyProjectDepartmentCommandHandler(IStudyProjectService service, StudyKit kit, StudyProjectSupport support)
        {
            _service = service;
            _kit = kit;
            _support = support;
        }

        public async Task<StudyProjectDto> Handle(RemoveStudyProjectDepartmentCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var participation = await _service.GetProjectDepartmentAsync(request.ProjectDepartmentId)
                ?? throw new KeyNotFoundException("القسم غير مشارك في المشروع");
            var project = await _support.LoadAsync(participation.ProjectId, viewer);
            StudyProjectSupport.EnsureOpen(project);
            if (participation.Members.Count > 0)
                throw new InvalidOperationException("للقسم أعضاء في فريق المشروع — أزلهم أولاً");
            if (await _service.HasTasksForDepartmentAsync(project.Id, participation.DepartmentId))
                throw new InvalidOperationException("للقسم أو مكاتبه مهام في المشروع — انقلها أو احذفها أولاً");

            var name = participation.Department.Name;
            await _service.RemoveProjectDepartmentAsync(participation);
            await _kit.LogAsync(await _support.RequestIdOf(project), StudyEntityType.ProjectDepartment, project.Id, StudyLogAction.Deleted,
                $"أزال القسم {name} من المشروع {project.Code}");
            return await _support.DetailAsync(project.Id, viewer);
        }
    }
}
