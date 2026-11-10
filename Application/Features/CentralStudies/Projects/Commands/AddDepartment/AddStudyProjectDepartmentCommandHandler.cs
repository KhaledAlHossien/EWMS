using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.Projects.Commands.AddDepartment
{
    public class AddStudyProjectDepartmentCommandHandler : IRequestHandler<AddStudyProjectDepartmentCommand, StudyProjectDto>
    {
        private readonly IStudyProjectService _service;
        private readonly IDepartmentService _departments;
        private readonly StudyKit _kit;
        private readonly StudyProjectSupport _support;

        public AddStudyProjectDepartmentCommandHandler(IStudyProjectService service, IDepartmentService departments, StudyKit kit, StudyProjectSupport support)
        {
            _service = service;
            _departments = departments;
            _kit = kit;
            _support = support;
        }

        public async Task<StudyProjectDto> Handle(AddStudyProjectDepartmentCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var project = await _support.LoadAsync(request.ProjectId, viewer);
            StudyProjectSupport.EnsureOpen(project);
            var department = await _departments.GetByIdAsync(request.DepartmentId) ?? throw new KeyNotFoundException("القسم غير موجود");
            if (department.BranchId != project.BranchId)
                throw new InvalidOperationException("القسم المشارك يجب أن يكون من أقسام فرع المشروع");
            if (await _service.DepartmentParticipatesAsync(project.Id, department.Id))
                throw new InvalidOperationException("القسم مشارك في المشروع مسبقاً");

            await _service.AddProjectDepartmentAsync(new ProjectDepartment
            {
                ProjectId = project.Id,
                DepartmentId = department.Id,
                AddedByUserId = viewer.Id,
                AddedAt = DateTime.Now
            });
            await _kit.LogAsync(await _support.RequestIdOf(project), StudyEntityType.ProjectDepartment, project.Id, StudyLogAction.Created,
                $"أضاف القسم {department.Name} إلى المشروع {project.Code}");
            return await _support.DetailAsync(project.Id, viewer);
        }
    }
}
