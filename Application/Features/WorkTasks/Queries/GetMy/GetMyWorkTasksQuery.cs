using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.WorkTasks.Queries.GetMy
{
    /// <summary>بطاقات مهام الموظف الحالي (الفعّالة، من فرعه الحالي)</summary>
    public record GetMyWorkTasksQuery : IRequest<List<WorkTaskCardDto>>;

    public class GetMyWorkTasksQueryHandler : IRequestHandler<GetMyWorkTasksQuery, List<WorkTaskCardDto>>
    {
        private readonly IWorkTaskService _workTaskService;
        private readonly IUserService _userService;
        private readonly IMapper _mapper;

        public GetMyWorkTasksQueryHandler(IWorkTaskService workTaskService, IUserService userService, IMapper mapper)
        {
            _workTaskService = workTaskService;
            _userService = userService;
            _mapper = mapper;
        }

        public async Task<List<WorkTaskCardDto>> Handle(GetMyWorkTasksQuery request, CancellationToken cancellationToken)
        {
            var user = await _userService.GetByIdAsync(_userService.UserId)
                ?? throw new UnauthorizedAccessException("المستخدم غير مصادق");

            // SuperAdmin لا يتبع لفرع → لا مهام شخصية
            if (user.BranchId is not int branchId) return [];

            return _mapper.Map<List<WorkTaskCardDto>>(await _workTaskService.GetForUserAsync(user.Id, branchId));
        }
    }
}
