using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using MediatR;

namespace Application.Features.Projects.Commands.AssignUsers
{
    public class AssignProjectUsersCommandHandler
        : IRequestHandler<AssignProjectUsersCommand, List<ProjectAssignmentResponseDto>>
    {
        private readonly IProjectService _projectService;
        private readonly IProjectAssignmentService _assignmentService;
        private readonly IUserService _userService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IMapper _mapper;

        public AssignProjectUsersCommandHandler(
            IProjectService projectService,
            IProjectAssignmentService assignmentService,
            IUserService userService,
            ICurrentUserService currentUserService,
            IMapper mapper)
        {
            _projectService = projectService;
            _assignmentService = assignmentService;
            _userService = userService;
            _currentUserService = currentUserService;
            _mapper = mapper;
        }

        public async Task<List<ProjectAssignmentResponseDto>> Handle(
            AssignProjectUsersCommand request,
            CancellationToken cancellationToken)
        {
            var project = await _projectService.GetByIdAsync(request.ProjectId)
                ?? throw new KeyNotFoundException("المشروع غير موجود");

            if (!_currentUserService.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase)
                && _currentUserService.DepartmentId != project.CurrentDepartmentId)
                throw new UnauthorizedAccessException("لا يمكنك تعيين موظفين لمشروع خارج قسمك");

            var usersToAssign = new List<int>();
            foreach (var userId in request.AssignmentDto.UserIds.Distinct())
            {
                var user = await _userService.GetWithDetailsAsync(userId)
                    ?? throw new KeyNotFoundException($"المستخدم رقم {userId} غير موجود");

                if (!_currentUserService.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase)
                    && user.DepartmentId != project.CurrentDepartmentId)
                    throw new InvalidOperationException("لا يمكن تعيين مستخدم من قسم آخر على المرحلة الحالية");

                usersToAssign.Add(userId);
            }

            foreach (var userId in usersToAssign)
            {
                var existing = await _assignmentService.GetActiveAssignmentAsync(project.Id, userId);
                if (existing != null)
                    continue;

                await _assignmentService.AddAsync(new ProjectAssignments
                {
                    ProjectId = project.Id,
                    AssignedUserId = userId,
                    AssignedByUserId = _currentUserService.UserId,
                    Status = AssignmentStatus.Active
                });
            }

            var assignments = await _assignmentService.GetByProjectAsync(project.Id);
            return _mapper.Map<List<ProjectAssignmentResponseDto>>(assignments);
        }
    }
}
