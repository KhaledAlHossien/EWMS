using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.WorkTasks.Queries.GetAll
{
    public record GetAllWorkTasksQuery(int? BranchId) : IRequest<List<WorkTaskResponseDto>>;

    public class GetAllWorkTasksQueryHandler : IRequestHandler<GetAllWorkTasksQuery, List<WorkTaskResponseDto>>
    {
        private readonly IWorkTaskService _workTaskService;
        private readonly IMapper _mapper;

        public GetAllWorkTasksQueryHandler(IWorkTaskService workTaskService, IMapper mapper)
        {
            _workTaskService = workTaskService;
            _mapper = mapper;
        }

        public async Task<List<WorkTaskResponseDto>> Handle(GetAllWorkTasksQuery request, CancellationToken cancellationToken)
            => _mapper.Map<List<WorkTaskResponseDto>>(await _workTaskService.GetAllAsync(request.BranchId));
    }
}
