using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using MediatR;

namespace Application.Features.Projects.Commands.UploadFile
{
    public class UploadProjectFileCommandHandler : IRequestHandler<UploadProjectFileCommand, ProjectFileResponseDto>
    {
        private readonly IProjectService _projectService;
        private readonly IProjectAssignmentService _assignmentService;
        private readonly IProjectFileService _fileService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IMapper _mapper;

        public UploadProjectFileCommandHandler(
            IProjectService projectService,
            IProjectAssignmentService assignmentService,
            IProjectFileService fileService,
            ICurrentUserService currentUserService,
            IMapper mapper)
        {
            _projectService = projectService;
            _assignmentService = assignmentService;
            _fileService = fileService;
            _currentUserService = currentUserService;
            _mapper = mapper;
        }

        public async Task<ProjectFileResponseDto> Handle(
            UploadProjectFileCommand request,
            CancellationToken cancellationToken)
        {
            var project = await _projectService.GetByIdAsync(request.ProjectId)
                ?? throw new KeyNotFoundException("المشروع غير موجود");

            if (!_currentUserService.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            {
                if (_currentUserService.DepartmentId != project.CurrentDepartmentId)
                    throw new UnauthorizedAccessException("لا يمكنك رفع ملف لمشروع خارج قسمك الحالي");

                var assignment = await _assignmentService.GetActiveAssignmentAsync(project.Id, _currentUserService.UserId);
                if (assignment == null)
                    throw new UnauthorizedAccessException("أنت غير معين على هذا المشروع");
            }

            var file = new ProjectFile
            {
                ProjectId = project.Id,
                UploadedById = _currentUserService.UserId,
                FileName = request.FileDto.FileName,
                FilePath = request.FileDto.FilePath,
                Notes = request.FileDto.Notes
            };

            await _fileService.AddAsync(file);

            var created = await _fileService.GetByIdAsync(file.Id) ?? file;
            return _mapper.Map<ProjectFileResponseDto>(created);
        }
    }
}
