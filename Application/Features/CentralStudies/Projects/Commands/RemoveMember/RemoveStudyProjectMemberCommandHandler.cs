using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.Projects.Commands.RemoveMember
{
    public class RemoveStudyProjectMemberCommandHandler : IRequestHandler<RemoveStudyProjectMemberCommand, StudyProjectDto>
    {
        private readonly IStudyProjectService _service;
        private readonly StudyKit _kit;
        private readonly StudyProjectSupport _support;

        public RemoveStudyProjectMemberCommandHandler(IStudyProjectService service, StudyKit kit, StudyProjectSupport support)
        {
            _service = service;
            _kit = kit;
            _support = support;
        }

        public async Task<StudyProjectDto> Handle(RemoveStudyProjectMemberCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var (participation, project) = await _support.LoadParticipationForMembersAsync(request.ProjectDepartmentId, viewer);
            var member = participation.Members.FirstOrDefault(m => m.UserId == request.UserId)
                ?? throw new KeyNotFoundException("الموظف ليس عضواً في فريق القسم");
            if (await _service.HasTasksForUserAsync(project.Id, request.UserId))
                throw new InvalidOperationException("للعضو مهام في المشروع — انقلها أولاً");

            await _service.RemoveMemberAsync(member);
            await _kit.LogAsync(await _support.RequestIdOf(project), StudyEntityType.ProjectMember, project.Id, StudyLogAction.Deleted,
                $"أزال عضواً من فريق المشروع {project.Code}", $"المستخدم رقم {request.UserId}");
            return await _support.DetailAsync(project.Id, viewer);
        }
    }
}
