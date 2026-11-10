using Application.Interfaces;
using MediatR;

namespace Application.Features.CentralStudies.Requests.Commands.Delete
{
    public class DeleteStudyRequestCommandHandler : IRequestHandler<DeleteStudyRequestCommand, Unit>
    {
        private readonly IStudyRequestService _service;
        private readonly StudyKit _kit;
        private readonly StudyRequestSupport _support;

        public DeleteStudyRequestCommandHandler(IStudyRequestService service, StudyKit kit, StudyRequestSupport support)
        {
            _service = service;
            _kit = kit;
            _support = support;
        }

        public async Task<Unit> Handle(DeleteStudyRequestCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var entity = await _support.LoadAsync(request.Id, viewer);
            StudyRules.EnsureDraftForDelete(entity.Status);
            if (entity.Assignment != null)
                throw new InvalidOperationException("للطلب تكليف داخلي — لا يُحذف");
            await _service.DeleteAsync(entity);   // سجل الدراسة ومرفقات الطلب تُحذف معه
            return Unit.Value;
        }
    }
}
