using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.Requests.Commands.Create
{
    public class CreateStudyRequestCommandHandler : IRequestHandler<CreateStudyRequestCommand, StudyRequestDto>
    {
        private readonly IStudyRequestService _service;
        private readonly StudyKit _kit;
        private readonly StudyRequestSupport _support;

        public CreateStudyRequestCommandHandler(IStudyRequestService service, StudyKit kit, StudyRequestSupport support)
        {
            _service = service;
            _kit = kit;
            _support = support;
        }

        public async Task<StudyRequestDto> Handle(CreateStudyRequestCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            // فرع الدراسة المختار (المركزية أو فرع جغرافي) يحدد المسار، وفرعه في النظام هو فرع الطلب (حدّ الصلاحيات)
            var studyBranch = await _support.StudyBranchAsync(request.Dto.StudyBranchId, viewer);
            await _support.EnsureSiteAsync(request.Dto.SiteId);

            var entity = _kit.Mapper.Map<StudyRequest>(request.Dto);
            var beneficiary = await _support.BeneficiaryAsync(request.Dto.MainBeneficiaryId);
            entity.MainBeneficiary = beneficiary.Name;
            entity.EntityCode = beneficiary.Code;
            StudyRequestSupport.Normalize(entity);
            if (await _service.NumberExistsAsync(entity.RequestNumber))
                throw new InvalidOperationException($"رقم طلب الدراسة «{entity.RequestNumber}» مستخدم في طلب آخر — تحقق من الرقم الوارد من الديوان المركزي");
            entity.StudyBranchId = studyBranch.Id;
            entity.BranchId = studyBranch.BranchId;
            entity.CreatedByUserId = viewer.Id;
            entity.Status = StudyDocumentStatus.Draft;
            entity.CreatedAt = DateTime.Now;

            await _service.AddAsync(entity);
            await _kit.LogAsync(entity.Id, StudyEntityType.Request, entity.Id, StudyLogAction.Created,
                $"سجّل طلب تجهيز الدراسة {entity.RequestNumber}", $"{entity.MainBeneficiary} — فرع الدراسة: {studyBranch.Name}");
            return await _support.DetailAsync(entity.Id, viewer);
        }
    }
}
