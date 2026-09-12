using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using MediatR;

namespace Application.Features.Projects.Commands.Create
{
    public class CreateProjectCommandHandler : IRequestHandler<CreateProjectCommand, ProjectResponseDto>
    {
        private readonly IProjectService _projectService;
        private readonly IProjectAssignmentService _assignmentService;
        private readonly IDepartmentService _departmentService;
        private readonly IUserService _userService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IMapper _mapper;

        public CreateProjectCommandHandler(
            IProjectService projectService,
            IProjectAssignmentService assignmentService,
            IDepartmentService departmentService,
            IUserService userService,
            ICurrentUserService currentUserService,
            IMapper mapper)
        {
            _projectService = projectService;
            _assignmentService = assignmentService;
            _departmentService = departmentService;
            _userService = userService;
            _currentUserService = currentUserService;
            _mapper = mapper;
        }

        public async Task<ProjectResponseDto> Handle(CreateProjectCommand request, CancellationToken cancellationToken)
        {
            if (!_currentUserService.IsAuthenticated)
                throw new UnauthorizedAccessException("يجب تسجيل الدخول");

            if (!await _departmentService.ExistsAsync(request.ProjectDto.CurrentDepartmentId))
                throw new KeyNotFoundException("القسم المحدد غير موجود");

            var creator = await _userService.GetWithDetailsAsync(_currentUserService.UserId)
                ?? throw new UnauthorizedAccessException("المستخدم الحالي غير موجود");

            if (!IsAdmin(creator) && creator.DepartmentId != request.ProjectDto.CurrentDepartmentId)
                throw new UnauthorizedAccessException("لا يمكنك إنشاء مشروع لقسم آخر");

            var usersToAssign = new List<int>();
            foreach (var userId in request.ProjectDto.AssignedUserIds.Distinct())
            {
                var user = await _userService.GetWithDetailsAsync(userId)
                    ?? throw new KeyNotFoundException($"المستخدم رقم {userId} غير موجود");

                if (!IsAdmin(creator) && user.DepartmentId != request.ProjectDto.CurrentDepartmentId)
                    throw new InvalidOperationException("لا يمكن تعيين مستخدم من قسم آخر عند إنشاء المشروع");

                usersToAssign.Add(userId);
            }

            var project = _mapper.Map<Project>(request.ProjectDto);
            project.CreatedById = _currentUserService.UserId;
            project.Status = ProjectStatus.Active;

            await _projectService.AddAsync(project);

            foreach (var userId in usersToAssign)
            {
                await _assignmentService.AddAsync(new ProjectAssignments
                {
                    ProjectId = project.Id,
                    AssignedUserId = userId,
                    AssignedByUserId = _currentUserService.UserId,
                    Status = AssignmentStatus.Active
                });
            }

            var created = await _projectService.GetWithDetailsAsync(project.Id) ?? project;
            var response = _mapper.Map<ProjectResponseDto>(created);
            response.Assignments = _mapper.Map<List<ProjectAssignmentResponseDto>>(
                await _assignmentService.GetByProjectAsync(project.Id));

            return response;
        }

        private static bool IsAdmin(User user)
        {
            return string.Equals(user.Role?.Name, "Admin", StringComparison.OrdinalIgnoreCase);
        }
    }
}
