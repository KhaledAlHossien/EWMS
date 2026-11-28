using Application.Interfaces;
using MediatR;

namespace Application.Features.CentralStudies.Lookups.StudyBranches.Commands.Delete
{
    public class DeleteStudyBranchCommandHandler : IRequestHandler<DeleteStudyBranchCommand, Unit>
    {
        private readonly IStudyLookupService _service;
        private readonly StudyKit _kit;

        public DeleteStudyBranchCommandHandler(IStudyLookupService service, StudyKit kit)
        {
            _service = service;
            _kit = kit;
        }

        public async Task<Unit> Handle(DeleteStudyBranchCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var entity = await _service.GetStudyBranchAsync(request.Id) ?? throw new KeyNotFoundException("فرع الدراسة غير موجود");
            StudyRules.EnsureInScope(viewer, entity.BranchId);
            if ((await _service.StudyBranchesInUseAsync([entity.Id])).Count > 0)
                throw new InvalidOperationException("لا يُحذف فرع دراسة مستعمل في طلبات — عطّله بدلاً من ذلك");
            await _service.RemoveAsync(entity);
            return Unit.Value;
        }
    }
}
