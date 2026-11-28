using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.Lookups.StudyBranches.Commands.Update
{
    public class UpdateStudyBranchCommandHandler : IRequestHandler<UpdateStudyBranchCommand, StudyBranchDto>
    {
        private readonly IStudyLookupService _service;
        private readonly StudyKit _kit;

        public UpdateStudyBranchCommandHandler(IStudyLookupService service, StudyKit kit)
        {
            _service = service;
            _kit = kit;
        }

        public async Task<StudyBranchDto> Handle(UpdateStudyBranchCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var entity = await _service.GetStudyBranchAsync(request.Id) ?? throw new KeyNotFoundException("فرع الدراسة غير موجود");
            StudyRules.EnsureInScope(viewer, entity.BranchId);
            var dto = request.Dto;
            var track = (StudyTrack)dto.Track;
            var inUse = (await _service.StudyBranchesInUseAsync([entity.Id])).Count > 0;
            // الرمز في أكواد المشاريع الصادرة، والمسار يحدد دورة الطلبات القائمة: لا يتغيران بعد الاستعمال
            if (inUse && (dto.Code != entity.Code || track != entity.Track))
                throw new InvalidOperationException("لا يتغير رمز فرع الدراسة ولا مساره بعد استعماله في طلبات — أضف فرعاً جديداً وعطّل هذا");

            var name = dto.Name.Trim();
            if (await _service.StudyBranchCodeExistsAsync(entity.BranchId, dto.Code, entity.Id))
                throw new InvalidOperationException($"الرمز {dto.Code} مستخدم لفرع دراسة آخر في هذا الفرع");
            if (await _service.StudyBranchNameExistsAsync(entity.BranchId, name, entity.Id))
                throw new InvalidOperationException("يوجد فرع دراسة آخر بهذا الاسم في هذا الفرع");

            entity.Code = dto.Code;
            entity.Name = name;
            entity.Track = track;
            entity.IsActive = dto.IsActive;
            await _service.SaveAsync();

            var result = _kit.Mapper.Map<StudyBranchDto>(entity);
            result.InUse = inUse;
            return result;
        }
    }
}
