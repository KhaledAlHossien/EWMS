using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.Projects.Commands.Update
{
    public class UpdateProjectCommandHandler : IRequestHandler<UpdateProjectCommand, ProjectResponseDto>
    {
        private readonly IProjectService _projectService;
        private readonly IProjectAssignmentService _assignmentService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IMapper _mapper;

        public UpdateProjectCommandHandler(
            IProjectService projectService,
            IProjectAssignmentService assignmentService,
            ICurrentUserService currentUserService,
            IMapper mapper)
        {
            _projectService = projectService;
            _assignmentService = assignmentService;
            _currentUserService = currentUserService;
            _mapper = mapper;
        }

        public async Task<ProjectResponseDto> Handle(UpdateProjectCommand request, CancellationToken cancellationToken)
        {
            var project = await _projectService.GetWithDetailsAsync(request.Id)
                ?? throw new KeyNotFoundException("المشروع غير موجود");

            await EnsureCanWorkOnProject(project.Id, project.CurrentDepartmentId);

            _mapper.Map(request.ProjectDto, project);
            await _projectService.UpdateAsync(project);

            var updated = await _projectService.GetWithDetailsAsync(project.Id) ?? project;
            return _mapper.Map<ProjectResponseDto>(updated);
        }

        private async Task EnsureCanWorkOnProject(int projectId, int departmentId)
        {
            if (_currentUserService.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
                return;

            if (_currentUserService.DepartmentId != departmentId)
                throw new UnauthorizedAccessException("المشروع ليس ضمن قسمك الحالي");

            var assignment = await _assignmentService.GetActiveAssignmentAsync(projectId, _currentUserService.UserId);
            if (assignment == null)
                throw new UnauthorizedAccessException("أنت غير معين على هذا المشروع");
        }
    }
}
