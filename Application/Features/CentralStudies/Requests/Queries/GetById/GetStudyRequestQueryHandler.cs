using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.Requests.Queries.GetById
{
    public class GetStudyRequestQueryHandler : IRequestHandler<GetStudyRequestQuery, StudyRequestDto>
    {
        private readonly StudyKit _kit;
        private readonly StudyRequestSupport _support;

        public GetStudyRequestQueryHandler(StudyKit kit, StudyRequestSupport support)
        {
            _kit = kit;
            _support = support;
        }

        public async Task<StudyRequestDto> Handle(GetStudyRequestQuery request, CancellationToken ct) =>
            await _support.DetailAsync(request.Id, await _kit.ViewerAsync());
    }
}
