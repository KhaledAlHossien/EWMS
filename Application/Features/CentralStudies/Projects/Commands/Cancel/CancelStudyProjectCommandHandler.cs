using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.Projects.Commands.Cancel
{
    public class CancelStudyProjectCommandHandler : IRequestHandler<CancelStudyProjectCommand, StudyProjectDto>
    {
        private readonly IStudyProjectService _service;
        private readonly StudyKit _kit;
        private readonly StudyProjectSupport _support;

        public CancelStudyProjectCommandHandler(IStudyProjectService service, StudyKit kit, StudyProjectSupport support)
        {
            _service = service;
            _kit = kit;
            _support = support;
        }

        public async Task<StudyProjectDto> Handle(CancelStudyProjectCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var entity = await _support.LoadAsync(request.Id, viewer);
            StudyProjectSupport.EnsureOpen(entity);
            entity.Status = StudyProjectStatus.Cancelled;
            entity.UpdatedAt = DateTime.Now;
            await _service.UpdateAsync(entity);
            await _kit.LogAsync(await _support.RequestIdOf(entity), StudyEntityType.Project, entity.Id, StudyLogAction.Cancelled,
                $"ألغى المشروع {entity.Code}", request.Reason.Trim());
            return await _support.DetailAsync(entity.Id, viewer);
        }
    }
}
