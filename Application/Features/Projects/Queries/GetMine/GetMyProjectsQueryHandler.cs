using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.Projects.Queries.GetMine
{
    public class GetMyProjectsQueryHandler : IRequestHandler<GetMyProjectsQuery, List<ProjectResponseDto>>
    {
        private readonly IProjectAssignmentService _assignmentService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IMapper _mapper;

        public GetMyProjectsQueryHandler(
            IProjectAssignmentService assignmentService,
            ICurrentUserService currentUserService,
            IMapper mapper)
        {
            _assignmentService = assignmentService;
            _currentUserService = currentUserService;
            _mapper = mapper;
        }

        public async Task<List<ProjectResponseDto>> Handle(GetMyProjectsQuery request, CancellationToken cancellationToken)
        {
            var assignments = await _assignmentService.GetByUserAsync(_currentUserService.UserId);
            var projects = assignments.Select(a => a.Project).DistinctBy(p => p.Id).ToList();
            return _mapper.Map<List<ProjectResponseDto>>(projects);
        }
    }
}
