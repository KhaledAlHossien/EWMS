using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.Drawings.Queries.GetById
{
    public class GetStudyDrawingQueryHandler : IRequestHandler<GetStudyDrawingQuery, StudyDrawingDto>
    {
        private readonly StudyKit _kit;
        private readonly StudyDrawingSupport _support;

        public GetStudyDrawingQueryHandler(StudyKit kit, StudyDrawingSupport support)
        {
            _kit = kit;
            _support = support;
        }

        public async Task<StudyDrawingDto> Handle(GetStudyDrawingQuery request, CancellationToken ct) =>
            await _support.DetailAsync(request.Id, await _kit.ViewerAsync());
    }
}
