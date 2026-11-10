using Application.Interfaces;
using MediatR;

namespace Application.Features.CentralStudies.Lookups.Catalogs.Commands.Delete
{
    public class DeleteStudyLookupCommandHandler : IRequestHandler<DeleteStudyLookupCommand, Unit>
    {
        private readonly IStudyLookupService _service;

        public DeleteStudyLookupCommandHandler(IStudyLookupService service)
        {
            _service = service;
        }

        public async Task<Unit> Handle(DeleteStudyLookupCommand request, CancellationToken ct)
        {
            if (request.Kind == StudyLookupKind.AttachmentType)
            {
                var entity = await _service.GetAttachmentTypeAsync(request.Id) ?? throw new KeyNotFoundException("نوع المرفق غير موجود");
                if (await _service.AttachmentTypeInUseAsync(entity.Id))
                    throw new InvalidOperationException("لا يُحذف نوع مرفق مستخدم — عطّله بدلاً من ذلك");
                await _service.RemoveAsync(entity);
            }
            else
            {
                var entity = await _service.GetDisciplineAsync(request.Id) ?? throw new KeyNotFoundException("الاختصاص غير موجود");
                if (await _service.DisciplineInUseAsync(entity.Id))
                    throw new InvalidOperationException("لا يُحذف اختصاص مستخدم في ردود أو أوامر بدء — عطّله بدلاً من ذلك");
                await _service.RemoveAsync(entity);
            }
            return Unit.Value;
        }
    }
}
