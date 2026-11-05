using Application.Interfaces;
using MediatR;

namespace Application.Features.Branches.Commands.Delete
{
    public class DeleteBranchCommandHandler : IRequestHandler<DeleteBranchCommand, bool>
    {
        private readonly IBranchService _branchService;
        private readonly IDeviceSiteService _installations;
        private readonly IMapLayerService _mapLayers;
        private readonly IStudyReferenceService _studies;

        public DeleteBranchCommandHandler(IBranchService branchService, IDeviceSiteService installations, IMapLayerService mapLayers,
            IStudyReferenceService studies)
        {
            _studies = studies;
            _branchService = branchService;
            _installations = installations;
            _mapLayers = mapLayers;
        }

        public async Task<bool> Handle(DeleteBranchCommand request, CancellationToken cancellationToken)
        {
            var branch = await _branchService.GetByIdAsync(request.Id)
                ?? throw new KeyNotFoundException("الفرع غير موجود");

            if (await _branchService.HasDepartmentsAsync(request.Id))
                throw new InvalidOperationException("لا يمكن حذف فرع يحتوي على أقسام");

            if (await _branchService.HasUsersAsync(request.Id))
                throw new InvalidOperationException("لا يمكن حذف فرع يحتوي على مستخدمين");

            if (await _branchService.HasWorkTasksAsync(request.Id))
                throw new InvalidOperationException("لا يمكن حذف فرع له مهام عمل، احذف مهامه أولاً");

            if (await _installations.ExistsForBranchAsync(request.Id))
                throw new InvalidOperationException("لا يمكن حذف فرع له تركيبات أجهزة مسجّلة باسمه — انقلها إلى فرع آخر أو احذفها أولاً");

            if (await _mapLayers.ExistsForBranchAsync(request.Id))
                throw new InvalidOperationException("لا يمكن حذف فرع له طبقات على خريطته — احذف طبقاته (وسجلاتها) أولاً");

            if (await _studies.ExistsForBranchAsync(request.Id))
                throw new InvalidOperationException("لا يمكن حذف فرع له طلبات دراسة أو مشاريع دراسات");

            return await _branchService.DeleteAsync(branch);
        }
    }
}
