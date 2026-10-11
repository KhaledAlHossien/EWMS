using Application.Interfaces;
using MediatR;

namespace Application.Features.Branches.Commands.Delete
{
    public class DeleteBranchCommandHandler : IRequestHandler<DeleteBranchCommand, bool>
    {
        private readonly IBranchService _branchService;
        private readonly IDeviceSiteService _installations;

        public DeleteBranchCommandHandler(IBranchService branchService, IDeviceSiteService installations)
        {
            _branchService = branchService;
            _installations = installations;
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

            return await _branchService.DeleteAsync(branch);
        }
    }
}
