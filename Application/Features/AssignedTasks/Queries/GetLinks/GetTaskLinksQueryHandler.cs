using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;

namespace Application.Features.AssignedTasks.Queries.GetLinks
{
    public class GetTaskLinksQueryHandler : IRequestHandler<GetTaskLinksQuery, List<TaskLinkDto>>
    {
        private readonly IUserService _userService;
        private readonly IUserPermissionService _permissions;
        private readonly TaskLinksSupport _support;

        public GetTaskLinksQueryHandler(IUserService userService, IUserPermissionService permissions, TaskLinksSupport support)
        {
            _userService = userService;
            _permissions = permissions;
            _support = support;
        }

        public async Task<List<TaskLinkDto>> Handle(GetTaskLinksQuery request, CancellationToken ct)
        {
            var viewer = await CurrentAsync();
            return await _support.ListAsync(await _support.ViewableTaskAsync(request.TaskId, viewer), viewer);
        }

        private Task<Viewer> CurrentAsync() => Viewer.CurrentAsync(_userService, _permissions);
    }
}
