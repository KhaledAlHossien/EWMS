using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.Projects.Queries.GetAll
{
    public class GetAllProjectsQueryHandler : IRequestHandler<GetAllProjectsQuery, List<ProjectResponseDto>>
    {
        private readonly IProjectService _projectService;
        private readonly IProjectAssignmentService _assignmentService;
        private readonly IProjectFileService _fileService;
        private readonly IProjectTransferService _transferService;
        private readonly IMapper _mapper;

        public GetAllProjectsQueryHandler(
            IProjectService projectService,
            IProjectAssignmentService assignmentService,
            IProjectFileService fileService,
            IProjectTransferService transferService,
            IMapper mapper)
        {
            _projectService = projectService;
            _assignmentService = assignmentService;
            _fileService = fileService;
            _transferService = transferService;
            _mapper = mapper;
        }

        public async Task<List<ProjectResponseDto>> Handle(
            GetAllProjectsQuery request,
            CancellationToken cancellationToken)
        {
            var projects = await _projectService.GetAllAsync();

            var result = new List<ProjectResponseDto>(projects.Count);

            foreach (var project in projects)
            {
                var dto = _mapper.Map<ProjectResponseDto>(project);

                dto.Assignments = _mapper.Map<List<ProjectAssignmentResponseDto>>(
                    await _assignmentService.GetByProjectAsync(project.Id));

                dto.Files = _mapper.Map<List<ProjectFileResponseDto>>(
                    await _fileService.GetByProjectAsync(project.Id));

                dto.Transfers = _mapper.Map<List<ProjectTransferResponseDto>>(
                    await _transferService.GetByProjectAsync(project.Id));

                result.Add(dto);
            }

            return result;
        }
    }
}