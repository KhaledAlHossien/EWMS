using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.Branches.Commands.Update
{
    public class UpdateBranchCommandHandler : IRequestHandler<UpdateBranchCommand, BranchResponseDto>
    {
        private readonly IBranchService _branchService;
        private readonly IMapper _mapper;

        public UpdateBranchCommandHandler(IBranchService branchService, IMapper mapper)
        {
            _branchService = branchService;
            _mapper = mapper;
        }

        public async Task<BranchResponseDto> Handle(UpdateBranchCommand request, CancellationToken cancellationToken)
        {
            var branch = await _branchService.GetByIdAsync(request.Id)
                ?? throw new KeyNotFoundException("الفرع غير موجود");

            if (await _branchService.ExistsByNameAsync(request.BranchDto.Name, request.Id))
                throw new InvalidOperationException("يوجد فرع آخر بنفس الاسم");

            _mapper.Map(request.BranchDto, branch);
            await _branchService.UpdateAsync(branch);
            return _mapper.Map<BranchResponseDto>(branch);
        }
    }
}
