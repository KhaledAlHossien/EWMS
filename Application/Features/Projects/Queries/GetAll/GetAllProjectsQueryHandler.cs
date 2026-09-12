using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.Projects.Queries.GetAll
{
    public class GetAllProjectsQueryHandler : IRequestHandler<GetAllProjectsQuery, List<ProjectResponseDto>>
    {
        private readonly IProjectService _projectService;
        private readonly IMapper _mapper;

        public GetAllProjectsQueryHandler(IProjectService projectService, IMapper mapper)
        {
            _projectService = projectService;
            _mapper = mapper;
        }

        public async Task<List<ProjectResponseDto>> Handle(GetAllProjectsQuery request, CancellationToken cancellationToken)
        {
            var projects = await _projectService.GetAllAsync();
            return _mapper.Map<List<ProjectResponseDto>>(projects);
        }
    }
}
