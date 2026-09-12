using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.Branches.Queries.GetById
{
    public class GetBranchByIdQueryHandler : IRequestHandler<GetBranchByIdQuery, BranchResponseDto>
    {
        private readonly IBranchService _branchService;
        private readonly IMapper _mapper;

        public GetBranchByIdQueryHandler(IBranchService branchService, IMapper mapper)
        {
            _branchService = branchService;
            _mapper = mapper;
        }

        public async Task<BranchResponseDto> Handle(GetBranchByIdQuery request, CancellationToken cancellationToken)
        {
            var branch = await _branchService.GetByIdAsync(request.Id)
                ?? throw new KeyNotFoundException("الفرع غير موجود");

            return _mapper.Map<BranchResponseDto>(branch);
        }
    }
}
