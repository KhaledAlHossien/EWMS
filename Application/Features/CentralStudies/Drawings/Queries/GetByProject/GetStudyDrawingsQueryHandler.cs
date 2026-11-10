using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.Drawings.Queries.GetByProject
{
    public class GetStudyDrawingsQueryHandler : IRequestHandler<GetStudyDrawingsQuery, List<StudyDrawingDto>>
    {
        private readonly IStudyDrawingService _service;
        private readonly IStudyProjectService _projects;
        private readonly StudyKit _kit;
        private readonly StudyDrawingSupport _support;

        public GetStudyDrawingsQueryHandler(IStudyDrawingService service, IStudyProjectService projects, StudyKit kit, StudyDrawingSupport support)
        {
            _service = service;
            _projects = projects;
            _kit = kit;
            _support = support;
        }

        public async Task<List<StudyDrawingDto>> Handle(GetStudyDrawingsQuery request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var project = await _projects.GetByIdAsync(request.ProjectId) ?? throw new KeyNotFoundException("المشروع غير موجود");
            StudyRules.EnsureInScope(viewer, project.BranchId);
            var kind = request.Kind is int k && Enum.IsDefined(typeof(DrawingKind), k) ? (DrawingKind?)k : null;
            return (await _service.GetByProjectAsync(project.Id, kind)).Select(d => _support.ToDto(d, viewer)).ToList();
        }
    }
}
