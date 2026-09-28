using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.WorkTasks.Queries.GetView
{
    /// <summary>فتح بطاقة مهمة (صفحة المهمة) — للمسنَد إليه أو رؤساء الفرع نفسه أو SuperAdmin</summary>
    public record GetWorkTaskViewQuery(int Id) : IRequest<WorkTaskCardDto>;

    public class GetWorkTaskViewQueryHandler : IRequestHandler<GetWorkTaskViewQuery, WorkTaskCardDto>
    {
        private readonly IWorkTaskService _workTaskService;
        private readonly IUserService _userService;
        private readonly IMapper _mapper;

        public GetWorkTaskViewQueryHandler(IWorkTaskService workTaskService, IUserService userService, IMapper mapper)
        {
            _workTaskService = workTaskService;
            _userService = userService;
            _mapper = mapper;
        }

        public async Task<WorkTaskCardDto> Handle(GetWorkTaskViewQuery request, CancellationToken cancellationToken)
        {
            var user = await _userService.GetByIdAsync(_userService.UserId)
                ?? throw new UnauthorizedAccessException("المستخدم غير مصادق");

            var task = await _workTaskService.GetByIdAsync(request.Id)
                ?? throw new KeyNotFoundException("المهمة غير موجودة");

            await WorkTaskRules.EnsureCanViewAsync(_workTaskService, user, task);
            return _mapper.Map<WorkTaskCardDto>(task);
        }
    }
}
