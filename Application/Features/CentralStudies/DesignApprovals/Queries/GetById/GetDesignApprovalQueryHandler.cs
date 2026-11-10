using Application.DTOs.Response;
using Application.Features.CentralStudies.Drawings;
using MediatR;

namespace Application.Features.CentralStudies.DesignApprovals.Queries.GetById
{
    public class GetDesignApprovalQueryHandler : IRequestHandler<GetDesignApprovalQuery, DesignApprovalDto>
    {
        private readonly StudyKit _kit;
        private readonly StudyDrawingSupport _support;

        public GetDesignApprovalQueryHandler(StudyKit kit, StudyDrawingSupport support)
        {
            _kit = kit;
            _support = support;
        }

        public async Task<DesignApprovalDto> Handle(GetDesignApprovalQuery request, CancellationToken ct) =>
            await _support.CdaDetailAsync(request.Id, await _kit.ViewerAsync());
    }
}
