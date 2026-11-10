using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.Requests.Commands.Create
{
    public class CreateStudyRequestCommandHandler : IRequestHandler<CreateStudyRequestCommand, StudyRequestDto>
    {
        private readonly IStudyRequestService _service;
        private readonly IStudyNumberService _numbers;
        private readonly IBranchService _branches;
        private readonly StudyKit _kit;
        private readonly StudyRequestSupport _support;

        public CreateStudyRequestCommandHandler(IStudyRequestService service, IStudyNumberService numbers, IBranchService branches, StudyKit kit, StudyRequestSupport support)
        {
            _service = service;
            _numbers = numbers;
            _branches = branches;
            _kit = kit;
            _support = support;
        }

        public async Task<StudyRequestDto> Handle(CreateStudyRequestCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            // الطلب يتبع فرع منشئه؛ نظرة المؤسسة تختار الفرع
            var branchId = viewer.IsOrganizationWide && request.Dto.BranchId is int chosen ? chosen : viewer.User.BranchId
                ?? throw new InvalidOperationException("حسابك غير مرتبط بفرع — لا يمكنك تسجيل طلبات دراسة");
            if (viewer.IsOrganizationWide && !await _branches.ExistsAsync(branchId))
                throw new KeyNotFoundException("الفرع المحدد غير موجود");
            await _support.EnsureSiteAsync(request.Dto.SiteId);

            var entity = _kit.Mapper.Map<StudyRequest>(request.Dto);
            StudyRequestSupport.Normalize(entity);
            var number = await _numbers.NextAsync(StudyDocumentType.Request, DateTime.Today.Year);
            entity.RequestNumber = StudyRules.FormatNumber(StudyDocumentType.Request, DateTime.Today.Year, number);
            entity.BranchId = branchId;
            entity.CreatedByUserId = viewer.Id;
            entity.Status = StudyDocumentStatus.Draft;
            entity.CreatedAt = DateTime.Now;

            await _service.AddAsync(entity);
            await _kit.LogAsync(entity.Id, StudyEntityType.Request, entity.Id, StudyLogAction.Created,
                $"سجّل طلب تجهيز الدراسة {entity.RequestNumber}", entity.MainBeneficiary);
            return await _support.DetailAsync(entity.Id, viewer);
        }
    }
}
