using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.Responses.Queries.GetById
{
    public class GetStudyResponseQueryHandler : IRequestHandler<GetStudyResponseQuery, StudyResponseDto>
    {
        private readonly StudyKit _kit;
        private readonly StudyResponseSupport _support;

        public GetStudyResponseQueryHandler(StudyKit kit, StudyResponseSupport support)
        {
            _kit = kit;
            _support = support;
        }

        public async Task<StudyResponseDto> Handle(GetStudyResponseQuery request, CancellationToken ct) =>
            await _support.DetailAsync(request.Id, await _kit.ViewerAsync());
    }
}
