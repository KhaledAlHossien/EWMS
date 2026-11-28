using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.CentralStudies.Lookups.StageTypes.Commands.Update
{
    public class UpdateStudyStageTypeCommandHandler : IRequestHandler<UpdateStudyStageTypeCommand, StudyStageTypeDto>
    {
        private readonly IStudyLookupService _service;
        private readonly IMapper _mapper;

        public UpdateStudyStageTypeCommandHandler(IStudyLookupService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        public async Task<StudyStageTypeDto> Handle(UpdateStudyStageTypeCommand request, CancellationToken ct)
        {
            var entity = await _service.GetStageTypeAsync(request.Id) ?? throw new KeyNotFoundException("نوع المرحلة غير موجود");
            var oldLink = entity.LinkedDocument;
            var oldTrack = entity.Track;
            _mapper.Map(request.Dto, entity);
            entity.Name = entity.Name.Trim();
            // المراحل المرتبطة بمستند أُنشئت مغلقة من ذلك المستند: تغيير الربط بعد الاستخدام يربك المشاريع القائمة،
            // وكذلك نقل النوع إلى مسار آخر وفي مشاريع المسار الأول مراحل منه
            if ((oldLink != entity.LinkedDocument || oldTrack != entity.Track) && await _service.StageTypeInUseAsync(entity.Id))
                throw new InvalidOperationException("لا يتغيّر المستند المرتبط ولا المسار لنوع مرحلة مستخدم في مشاريع — أنشئ نوعاً جديداً");
            if (await _service.StageTypeNameExistsAsync(entity.Name, entity.Id))
                throw new InvalidOperationException("يوجد نوع مرحلة آخر بهذا الاسم");
            await _service.SaveAsync();
            return _mapper.Map<StudyStageTypeDto>(entity);
        }
    }
}
