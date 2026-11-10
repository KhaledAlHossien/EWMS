using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.Projects.Commands.AddMember
{
    public class AddStudyProjectMemberCommandHandler : IRequestHandler<AddStudyProjectMemberCommand, StudyProjectDto>
    {
        private readonly IStudyProjectService _service;
        private readonly IUserService _users;
        private readonly StudyKit _kit;
        private readonly StudyProjectSupport _support;

        public AddStudyProjectMemberCommandHandler(IStudyProjectService service, IUserService users, StudyKit kit, StudyProjectSupport support)
        {
            _service = service;
            _users = users;
            _kit = kit;
            _support = support;
        }

        public async Task<StudyProjectDto> Handle(AddStudyProjectMemberCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var (participation, project) = await _support.LoadParticipationForMembersAsync(request.ProjectDepartmentId, viewer);
            var user = await _users.GetByIdAsync(request.Dto.UserId);
            if (user is not { IsActive: true }) throw new KeyNotFoundException("الموظف غير موجود أو غير نشط");
            if (user.DepartmentId != participation.DepartmentId)
                throw new InvalidOperationException("الموظف ليس من القسم المشارك");
            if (await _service.IsMemberAsync(project.Id, user.Id))
                throw new InvalidOperationException("الموظف عضو في فريق المشروع مسبقاً");

            await _service.AddMemberAsync(new ProjectMember
            {
                ProjectDepartmentId = participation.Id,
                UserId = user.Id,
                RoleTitle = StudyRules.Clean(request.Dto.RoleTitle),
                AssignedByUserId = viewer.Id,
                AssignedAt = DateTime.Now
            });
            await _kit.LogAsync(await _support.RequestIdOf(project), StudyEntityType.ProjectMember, project.Id, StudyLogAction.Created,
                $"عيّن {user.FullName} في فريق المشروع {project.Code}", request.Dto.RoleTitle);
            return await _support.DetailAsync(project.Id, viewer);
        }
    }
}
