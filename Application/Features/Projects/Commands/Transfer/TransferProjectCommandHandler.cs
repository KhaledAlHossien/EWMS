using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using MediatR;

namespace Application.Features.Projects.Commands.Transfer
{
    public class TransferProjectCommandHandler : IRequestHandler<TransferProjectCommand, ProjectTransferResponseDto>
    {
        private readonly IProjectService _projectService;
        private readonly IProjectTransferService _transferService;
        private readonly IDepartmentService _departmentService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IMapper _mapper;

        public TransferProjectCommandHandler(
            IProjectService projectService,
            IProjectTransferService transferService,
            IDepartmentService departmentService,
            ICurrentUserService currentUserService,
            IMapper mapper)
        {
            _projectService = projectService;
            _transferService = transferService;
            _departmentService = departmentService;
            _currentUserService = currentUserService;
            _mapper = mapper;
        }

        public async Task<ProjectTransferResponseDto> Handle(
            TransferProjectCommand request,
            CancellationToken cancellationToken)
        {
            var project = await _projectService.GetByIdAsync(request.ProjectId)
                ?? throw new KeyNotFoundException("المشروع غير موجود");

            if (!_currentUserService.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase)
                && _currentUserService.DepartmentId != project.CurrentDepartmentId)
                throw new UnauthorizedAccessException("لا يمكنك تحويل مشروع ليس ضمن قسمك الحالي");

            if (!await _departmentService.ExistsAsync(request.TransferDto.ToDepartmentId))
                throw new KeyNotFoundException("القسم الهدف غير موجود");

            if (request.TransferDto.ToDepartmentId == project.CurrentDepartmentId)
                throw new InvalidOperationException("القسم الهدف هو القسم الحالي للمشروع");

            await _transferService.DeactivateAllAsync(project.Id);

            var transfer = new ProjectTransfers
            {
                ProjectId = project.Id,
                FromDepartmentId = project.CurrentDepartmentId,
                ToDepartmentId = request.TransferDto.ToDepartmentId,
                TransferredById = _currentUserService.UserId,
                TransferType = request.TransferDto.TransferType,
                Notes = request.TransferDto.Notes,
                IsActive = true
            };

            await _transferService.AddAsync(transfer);

            project.CurrentDepartmentId = request.TransferDto.ToDepartmentId;
            project.Status = ProjectStatus.Active;
            await _projectService.UpdateAsync(project);

            var created = await _transferService.GetByIdAsync(transfer.Id) ?? transfer;
            return _mapper.Map<ProjectTransferResponseDto>(created);
        }
    }
}
