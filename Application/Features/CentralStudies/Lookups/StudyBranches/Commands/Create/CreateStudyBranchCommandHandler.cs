using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.Lookups.StudyBranches.Commands.Create
{
    public class CreateStudyBranchCommandHandler : IRequestHandler<CreateStudyBranchCommand, StudyBranchDto>
    {
        private readonly IStudyLookupService _service;
        private readonly IBranchService _branches;
        private readonly StudyKit _kit;

        public CreateStudyBranchCommandHandler(IStudyLookupService service, IBranchService branches, StudyKit kit)
        {
            _service = service;
            _branches = branches;
            _kit = kit;
        }

        public async Task<StudyBranchDto> Handle(CreateStudyBranchCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var dto = request.Dto;
            // فرع الدراسة يتبع فرعاً في النظام (فرع الدراسات): فرع المستخدم، أو ما تختاره نظرة المؤسسة
            var branchId = viewer.IsOrganizationWide && dto.BranchId is int chosen ? chosen
                : viewer.User.BranchId ?? throw new InvalidOperationException("حسابك غير مرتبط بفرع — لا يمكنك إضافة أفرع دراسات");
            if (!await _branches.ExistsAsync(branchId)) throw new KeyNotFoundException("الفرع المحدد غير موجود");
            StudyRules.EnsureInScope(viewer, branchId);

            var name = dto.Name.Trim();
            if (await _service.StudyBranchCodeExistsAsync(branchId, dto.Code))
                throw new InvalidOperationException($"الرمز {dto.Code} مستخدم لفرع دراسة آخر في هذا الفرع");
            if (await _service.StudyBranchNameExistsAsync(branchId, name))
                throw new InvalidOperationException("يوجد فرع دراسة بهذا الاسم في هذا الفرع");

            var entity = new StudyBranch
            {
                BranchId = branchId,
                Code = dto.Code,
                Name = name,
                Track = (StudyTrack)dto.Track,
                IsActive = dto.IsActive
            };
            await _service.AddAsync(entity);
            var saved = await _service.GetStudyBranchAsync(entity.Id) ?? entity;
            return _kit.Mapper.Map<StudyBranchDto>(saved);
        }
    }
}
