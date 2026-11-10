using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.Assignments.Queries.GetById
{
    public class GetStudyAssignmentQueryHandler : IRequestHandler<GetStudyAssignmentQuery, StudyAssignmentDto>
    {
        private readonly StudyKit _kit;
        private readonly StudyAssignmentSupport _support;

        public GetStudyAssignmentQueryHandler(StudyKit kit, StudyAssignmentSupport support)
        {
            _kit = kit;
            _support = support;
        }

        public async Task<StudyAssignmentDto> Handle(GetStudyAssignmentQuery request, CancellationToken ct) =>
            await _support.DetailAsync(request.Id, await _kit.ViewerAsync());
    }
}
