using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.Projects.Queries.GetByDepartment
{
    public class GetProjectsByDepartmentQueryHandler
        : IRequestHandler<GetProjectsByDepartmentQuery, List<ProjectResponseDto>>
    {
        private readonly IProjectService _projectService;
        private readonly IMapper _mapper;

        public GetProjectsByDepartmentQueryHandler(IProjectService projectService, IMapper mapper)
        {
            _projectService = projectService;
            _mapper = mapper;
        }

        public async Task<List<ProjectResponseDto>> Handle(
            GetProjectsByDepartmentQuery request,
            CancellationToken cancellationToken)
        {
            var projects = await _projectService.GetByDepartmentAsync(request.DepartmentId);
            return _mapper.Map<List<ProjectResponseDto>>(projects);
        }
    }
}
