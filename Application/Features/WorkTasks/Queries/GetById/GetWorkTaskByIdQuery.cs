using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.WorkTasks.Queries.GetById
{
    public record GetWorkTaskByIdQuery(int Id) : IRequest<WorkTaskResponseDto>;

    public class GetWorkTaskByIdQueryHandler : IRequestHandler<GetWorkTaskByIdQuery, WorkTaskResponseDto>
    {
        private readonly IWorkTaskService _workTaskService;
        private readonly IMapper _mapper;

        public GetWorkTaskByIdQueryHandler(IWorkTaskService workTaskService, IMapper mapper)
        {
            _workTaskService = workTaskService;
            _mapper = mapper;
        }

        public async Task<WorkTaskResponseDto> Handle(GetWorkTaskByIdQuery request, CancellationToken cancellationToken)
        {
            var task = await _workTaskService.GetByIdAsync(request.Id)
                ?? throw new KeyNotFoundException("المهمة غير موجودة");
            return _mapper.Map<WorkTaskResponseDto>(task);
        }
    }
}
