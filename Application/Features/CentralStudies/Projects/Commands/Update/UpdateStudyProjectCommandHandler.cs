using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.Projects.Commands.Update
{
    public class UpdateStudyProjectCommandHandler : IRequestHandler<UpdateStudyProjectCommand, StudyProjectDto>
    {
        private readonly IStudyProjectService _service;
        private readonly IUserService _users;
        private readonly StudyKit _kit;
        private readonly StudyProjectSupport _support;

        public UpdateStudyProjectCommandHandler(IStudyProjectService service, IUserService users, StudyKit kit, StudyProjectSupport support)
        {
            _service = service;
            _users = users;
            _kit = kit;
            _support = support;
        }

        public async Task<StudyProjectDto> Handle(UpdateStudyProjectCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var entity = await _support.LoadAsync(request.Id, viewer);
            StudyProjectSupport.EnsureOpen(entity);
            if (request.Dto.ManagerUserId is int managerId && managerId != entity.ManagerUserId)
            {
                StudyRules.EnsureBranchEmployee(await _users.GetByIdAsync(managerId), entity.BranchId, "مدير المشروع");
                entity.ManagerUserId = managerId;
            }
            entity.Name = request.Dto.Name.Trim();
            entity.PlannedStartDate = request.Dto.PlannedStartDate.Date;
            entity.PlannedEndDate = request.Dto.PlannedEndDate.Date;
            entity.ActualStartDate = request.Dto.ActualStartDate?.Date ?? entity.ActualStartDate;
            if (request.Dto.Status is int status) entity.Status = (StudyProjectStatus)status;
            entity.UpdatedAt = DateTime.Now;
            await _service.UpdateAsync(entity, request.Dto.RowVersion);
            await _kit.LogAsync(await _support.RequestIdOf(entity), StudyEntityType.Project, entity.Id, StudyLogAction.Edited,
                $"عدّل المشروع {entity.Code}", $"الحالة: {StudyRules.Name(entity.Status)}");
            return await _support.DetailAsync(entity.Id, viewer);
        }
    }
}
