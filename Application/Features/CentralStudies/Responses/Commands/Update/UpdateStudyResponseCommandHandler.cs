using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.Responses.Commands.Update
{
    public class UpdateStudyResponseCommandHandler : IRequestHandler<UpdateStudyResponseCommand, StudyResponseDto>
    {
        private readonly IStudyResponseService _service;
        private readonly StudyKit _kit;
        private readonly StudyResponseSupport _support;

        public UpdateStudyResponseCommandHandler(IStudyResponseService service, StudyKit kit, StudyResponseSupport support)
        {
            _service = service;
            _kit = kit;
            _support = support;
        }

        public async Task<StudyResponseDto> Handle(UpdateStudyResponseCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var entity = await _support.LoadAsync(request.Id, viewer);
            StudyRules.EnsureEditable(entity.Status);
            StudyResponseSupport.EnsureFollowingDepartment(viewer, entity);
            await _support.ValidateAsync(request.Dto, entity.Assignment.Request.BranchId);

            StudyResponseSupport.Apply(request.Dto, entity);
            entity.Status = StudyDocumentStatus.Draft;
            entity.UpdatedAt = DateTime.Now;
            await _service.UpdateAsync(entity, request.Dto.RowVersion);
            await _kit.LogAsync(entity.Assignment.RequestId, StudyEntityType.Response, entity.Id, StudyLogAction.Edited,
                $"عدّل جزء القسم المتابع من إشعار الرد {entity.ResponseNumber}");
            return await _support.DetailAsync(entity.Id, viewer);
        }
    }
}
