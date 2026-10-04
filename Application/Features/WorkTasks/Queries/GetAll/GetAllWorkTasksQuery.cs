using Application.DTOs.Response;
using Application.Common;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.WorkTasks.Queries.GetAll
{
    public record GetAllWorkTasksQuery(int? BranchId) : IRequest<List<WorkTaskResponseDto>>;

    public class GetAllWorkTasksQueryHandler : IRequestHandler<GetAllWorkTasksQuery, List<WorkTaskResponseDto>>
    {
        private readonly IWorkTaskService _workTaskService;
        private readonly IUserService _users;
        private readonly IUserPermissionService _permissions;
        private readonly IMapper _mapper;

        public GetAllWorkTasksQueryHandler(IWorkTaskService workTaskService, IUserService users, IUserPermissionService permissions, IMapper mapper)
        {
            _workTaskService = workTaskService;
            _mapper = mapper;
            _users = users;
            _permissions = permissions;
        }

        public async Task<List<WorkTaskResponseDto>> Handle(GetAllWorkTasksQuery request, CancellationToken cancellationToken)
        {
            // حدّ ViewWorkTasks: مهام فرعي، أو أي فرع لمن يتصفح المؤسسة (StructureScope)
            var viewer = await Viewer.CurrentAsync(_users, _permissions);
            var limit = StructureScope.BranchOf(viewer);
            if (limit != null && request.BranchId != null && request.BranchId != limit)
                throw new UnauthorizedAccessException("لا يمكنك عرض مهام فرع غير فرعك");
            return _mapper.Map<List<WorkTaskResponseDto>>(await _workTaskService.GetAllAsync(limit ?? request.BranchId));
        }
    }
}
