using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using MediatR;

namespace Application.Features.Branches.Commands.Create
{
    public class CreateBranchCommandHandler : IRequestHandler<CreateBranchCommand, BranchResponseDto>
    {
        private readonly IBranchService _branchService;
        private readonly IMapper _mapper;

        public CreateBranchCommandHandler(IBranchService branchService, IMapper mapper)
        {
            _branchService = branchService;
            _mapper = mapper;
        }

        public async Task<BranchResponseDto> Handle(CreateBranchCommand request, CancellationToken cancellationToken)
        {
            if (await _branchService.ExistsByNameAsync(request.BranchDto.Name))
                throw new InvalidOperationException("يوجد فرع بنفس الاسم مسبقاً");

            var branch = _mapper.Map<Branch>(request.BranchDto);
            var created = await _branchService.AddAsync(branch);
            return _mapper.Map<BranchResponseDto>(created);
        }
    }
}
