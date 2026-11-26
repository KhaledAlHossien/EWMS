using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.ToDoLists.Queries.GetById
{
    public class GetToDoListByIdQueryHandler : IRequestHandler<GetToDoListByIdQuery, ToDoListResponseDto>
    {
        private readonly IUserService _userService;
        private readonly IUserPermissionService _permissions;
        private readonly IMapper _mapper;
        private readonly ToDoListSupport _support;

        public GetToDoListByIdQueryHandler(IUserService userService, IUserPermissionService permissions, IMapper mapper, ToDoListSupport support)
        {
            _userService = userService;
            _permissions = permissions;
            _mapper = mapper;
            _support = support;
        }

        public async Task<ToDoListResponseDto> Handle(GetToDoListByIdQuery request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            var entity = await _support.LoadOwnedAsync(viewer, request.Id, "لا يمكنك عرض قائمة غيرك");
            var dto = _mapper.Map<ToDoListResponseDto>(entity);
            ToDoRules.FillLinks(entity, dto, viewer);
            return dto;
        }

        private Task<Viewer> ViewerAsync() => Viewer.CurrentAsync(_userService, _permissions);
    }
}
