using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.Assignments.Commands.Update
{
    public class UpdateStudyAssignmentCommandHandler : IRequestHandler<UpdateStudyAssignmentCommand, StudyAssignmentDto>
    {
        private readonly IStudyAssignmentService _service;
        private readonly StudyKit _kit;
        private readonly StudyAssignmentSupport _support;

        public UpdateStudyAssignmentCommandHandler(IStudyAssignmentService service, StudyKit kit, StudyAssignmentSupport support)
        {
            _service = service;
            _kit = kit;
            _support = support;
        }

        public async Task<StudyAssignmentDto> Handle(UpdateStudyAssignmentCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var entity = await _support.LoadAsync(request.Id, viewer);
            StudyRules.EnsureEditable(entity.Status);
            await _support.EnsureDepartmentAsync(request.Dto.FollowingDepartmentId, entity.Request.BranchId);
            // قبل Apply: الكود الحالي يُقرأ بتاريخ التكليف المحفوظ، ثم يُولَّد بالتاريخ الجديد.
            var projectCode = await _support.RefreshProjectCodeAsync(entity, request.Dto.AssignmentDate, request.Dto.AdminArchiveNumber);

            StudyAssignmentSupport.Apply(request.Dto, entity);
            entity.ProjectCode = projectCode;
            entity.Status = StudyDocumentStatus.Draft;
            entity.UpdatedAt = DateTime.Now;
            await _service.UpdateAsync(entity, request.Dto.RowVersion);
            await _kit.LogAsync(entity.RequestId, StudyEntityType.Assignment, entity.Id, StudyLogAction.Edited,
                $"عدّل التكليف {entity.AssignmentNumber}");
            return await _support.DetailAsync(entity.Id, viewer);
        }
    }
}
