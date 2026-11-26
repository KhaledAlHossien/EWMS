using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using MediatR;

namespace Application.Features.ToDoLists.Commands.Create
{
    public class CreateToDoListCommandHandler : IRequestHandler<CreateToDoListCommand, ToDoListResponseDto>
    {
        private readonly IToDoListService _service;
        private readonly IUserService _userService;
        private readonly IUserPermissionService _permissions;
        private readonly IMapper _mapper;

        public CreateToDoListCommandHandler(IToDoListService service, IUserService userService, IUserPermissionService permissions, IMapper mapper)
        {
            _service = service;
            _userService = userService;
            _permissions = permissions;
            _mapper = mapper;
        }

        public async Task<ToDoListResponseDto> Handle(CreateToDoListCommand request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();

            var entity = _mapper.Map<ToDoList>(request.Dto);
            ToDoListSupport.Trim(entity);

            // صاحب القائمة = من أنشأها ولا يتغير
            entity.UserId = viewer.Id;

            if (await _service.ExistsByNameAsync(viewer.Id, entity.Name))
                throw new InvalidOperationException("لديك قائمة بنفس الاسم مسبقاً");

            var created = await _service.AddAsync(entity);
            return _mapper.Map<ToDoListResponseDto>(await _service.GetByIdAsync(created.Id) ?? created);
        }

        private Task<Viewer> ViewerAsync() => Viewer.CurrentAsync(_userService, _permissions);
    }
}
