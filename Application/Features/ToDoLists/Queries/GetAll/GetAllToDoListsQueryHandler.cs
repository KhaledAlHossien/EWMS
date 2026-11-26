using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.ToDoLists.Queries.GetAll
{
    public class GetAllToDoListsQueryHandler : IRequestHandler<GetAllToDoListsQuery, List<ToDoListResponseDto>>
    {
        private readonly IToDoListService _service;
        private readonly IUserService _userService;
        private readonly IUserPermissionService _permissions;
        private readonly IMapper _mapper;

        public GetAllToDoListsQueryHandler(IToDoListService service, IUserService userService, IUserPermissionService permissions, IMapper mapper)
        {
            _service = service;
            _userService = userService;
            _permissions = permissions;
            _mapper = mapper;
        }

        public async Task<List<ToDoListResponseDto>> Handle(GetAllToDoListsQuery request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            var ownerId = viewer.IsSuperAdmin ? request.OwnerId : viewer.Id;

            var lists = _mapper.Map<List<ToDoListResponseDto>>(await _service.GetAllAsync(ownerId));
            foreach (var l in lists) l.Items = [];      // القائمة الرئيسية بالأعداد فقط، والبنود في Get/{id}
            return lists;
        }

        private Task<Viewer> ViewerAsync() => Viewer.CurrentAsync(_userService, _permissions);
    }
}
