using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.Requests.Commands.Update
{
    public class UpdateStudyRequestCommandHandler : IRequestHandler<UpdateStudyRequestCommand, StudyRequestDto>
    {
        private readonly IStudyRequestService _service;
        private readonly StudyKit _kit;
        private readonly StudyRequestSupport _support;

        public UpdateStudyRequestCommandHandler(IStudyRequestService service, StudyKit kit, StudyRequestSupport support)
        {
            _service = service;
            _kit = kit;
            _support = support;
        }

        public async Task<StudyRequestDto> Handle(UpdateStudyRequestCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var entity = await _support.LoadAsync(request.Id, viewer);
            StudyRules.EnsureEditable(entity.Status);
            await _support.EnsureSiteAsync(request.Dto.SiteId);
            if (await _service.NumberExistsAsync(request.Dto.RequestNumber.Trim(), entity.Id))
                throw new InvalidOperationException($"رقم طلب الدراسة «{request.Dto.RequestNumber.Trim()}» مستخدم في طلب آخر — تحقق من الرقم الوارد من الديوان المركزي");

            var beneficiary = await _support.BeneficiaryAsync(request.Dto.MainBeneficiaryId, entity);
            _kit.Mapper.Map(request.Dto, entity);
            entity.MainBeneficiary = beneficiary.Name;
            entity.EntityCode = beneficiary.Code;
            StudyRequestSupport.Normalize(entity);
            entity.Status = StudyDocumentStatus.Draft;
            entity.UpdatedAt = DateTime.Now;
            await _service.UpdateAsync(entity, request.Dto.RowVersion);
            await _kit.LogAsync(entity.Id, StudyEntityType.Request, entity.Id, StudyLogAction.Edited,
                $"عدّل الطلب {entity.RequestNumber}");
            return await _support.DetailAsync(entity.Id, viewer);
        }
    }
}
