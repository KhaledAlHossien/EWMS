using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.Projects.Queries.GetById
{
    public class GetStudyProjectQueryHandler : IRequestHandler<GetStudyProjectQuery, StudyProjectDto>
    {
        private readonly StudyKit _kit;
        private readonly StudyProjectSupport _support;

        public GetStudyProjectQueryHandler(StudyKit kit, StudyProjectSupport support)
        {
            _kit = kit;
            _support = support;
        }

        public async Task<StudyProjectDto> Handle(GetStudyProjectQuery request, CancellationToken ct) =>
            await _support.DetailAsync(request.Id, await _kit.ViewerAsync());
    }
}
