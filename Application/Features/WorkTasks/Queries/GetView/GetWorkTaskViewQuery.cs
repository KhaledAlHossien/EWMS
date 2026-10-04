using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.WorkTasks.Queries.GetView
{
    /// <summary>فتح بطاقة مهمة (صفحة المهمة) — للمسنَد إليه، أو من يملك ViewWorkTasks أو لوحة متابعة في نفس الفرع، أو SuperAdmin</summary>
    public record GetWorkTaskViewQuery(int Id) : IRequest<WorkTaskCardDto>;

    public class GetWorkTaskViewQueryHandler : IRequestHandler<GetWorkTaskViewQuery, WorkTaskCardDto>
    {
        private readonly IWorkTaskService _workTaskService;
        private readonly IUserService _userService;
        private readonly IUserPermissionService _permissions;
        private readonly IMapper _mapper;

        public GetWorkTaskViewQueryHandler(IWorkTaskService workTaskService, IUserService userService, IUserPermissionService permissions, IMapper mapper)
        {
            _workTaskService = workTaskService;
            _userService = userService;
            _permissions = permissions;
            _mapper = mapper;
        }

        public async Task<WorkTaskCardDto> Handle(GetWorkTaskViewQuery request, CancellationToken cancellationToken)
        {
            var viewer = await Application.Common.Viewer.CurrentAsync(_userService, _permissions);

            var task = await _workTaskService.GetByIdAsync(request.Id)
                ?? throw new KeyNotFoundException("المهمة غير موجودة");

            await WorkTaskRules.EnsureCanViewAsync(_workTaskService, viewer, task);
            return _mapper.Map<WorkTaskCardDto>(task);
        }
    }
}
