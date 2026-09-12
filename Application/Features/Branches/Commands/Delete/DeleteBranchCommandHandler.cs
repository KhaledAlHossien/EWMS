using Application.Interfaces;
using MediatR;

namespace Application.Features.Branches.Commands.Delete
{
    public class DeleteBranchCommandHandler : IRequestHandler<DeleteBranchCommand, bool>
    {
        private readonly IBranchService _branchService;

        public DeleteBranchCommandHandler(IBranchService branchService)
        {
            _branchService = branchService;
        }

        public async Task<bool> Handle(DeleteBranchCommand request, CancellationToken cancellationToken)
        {
            var branch = await _branchService.GetByIdAsync(request.Id)
                ?? throw new KeyNotFoundException("الفرع غير موجود");

            if (await _branchService.HasDepartmentsAsync(request.Id))
                throw new InvalidOperationException("لا يمكن حذف فرع يحتوي على أقسام");

            if (await _branchService.HasUsersAsync(request.Id))
                throw new InvalidOperationException("لا يمكن حذف فرع يحتوي على مستخدمين");

            return await _branchService.DeleteAsync(branch);
        }
    }
}
