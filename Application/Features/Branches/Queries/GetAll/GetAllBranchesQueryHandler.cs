using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.Branches.Queries.GetAll
{
    public class GetAllBranchesQueryHandler : IRequestHandler<GetAllBranchesQuery, List<BranchResponseDto>>
    {
        private readonly IBranchService _branchService;
        private readonly IMapper _mapper;

        public GetAllBranchesQueryHandler(IBranchService branchService, IMapper mapper)
        {
            _branchService = branchService;
            _mapper = mapper;
        }

        public async Task<List<BranchResponseDto>> Handle(GetAllBranchesQuery request, CancellationToken cancellationToken)
        {
            return _mapper.Map<List<BranchResponseDto>>(await _branchService.GetAllAsync());
        }
    }
}
