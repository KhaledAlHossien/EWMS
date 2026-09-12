using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.Projects.Queries.GetById
{
    public class GetProjectByIdQueryHandler : IRequestHandler<GetProjectByIdQuery, ProjectResponseDto>
    {
        private readonly IProjectService _projectService;
        private readonly IProjectAssignmentService _assignmentService;
        private readonly IProjectFileService _fileService;
        private readonly IProjectTransferService _transferService;
        private readonly IMapper _mapper;

        public GetProjectByIdQueryHandler(
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

        public async Task<ProjectResponseDto> Handle(GetProjectByIdQuery request, CancellationToken cancellationToken)
        {
            var project = await _projectService.GetWithDetailsAsync(request.Id)
                ?? throw new KeyNotFoundException("المشروع غير موجود");

            var response = _mapper.Map<ProjectResponseDto>(project);
            response.Assignments = _mapper.Map<List<ProjectAssignmentResponseDto>>(
                await _assignmentService.GetByProjectAsync(project.Id));
            response.Files = _mapper.Map<List<ProjectFileResponseDto>>(
                await _fileService.GetByProjectAsync(project.Id));
            response.Transfers = _mapper.Map<List<ProjectTransferResponseDto>>(
                await _transferService.GetByProjectAsync(project.Id));

            return response;
        }
    }
}
