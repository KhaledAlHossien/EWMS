using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.StartOrders.Queries.GetById
{
    public class GetStudyStartOrderQueryHandler : IRequestHandler<GetStudyStartOrderQuery, StudyStartOrderDto>
    {
        private readonly StudyKit _kit;
        private readonly StudyStartOrderSupport _support;

        public GetStudyStartOrderQueryHandler(StudyKit kit, StudyStartOrderSupport support)
        {
            _kit = kit;
            _support = support;
        }

        public async Task<StudyStartOrderDto> Handle(GetStudyStartOrderQuery request, CancellationToken ct) =>
            await _support.DetailAsync(request.Id, await _kit.ViewerAsync());
    }
}
