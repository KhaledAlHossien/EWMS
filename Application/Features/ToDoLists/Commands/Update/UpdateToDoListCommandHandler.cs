using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.ToDoLists.Commands.Update
{
    public class UpdateToDoListCommandHandler : IRequestHandler<UpdateToDoListCommand, ToDoListResponseDto>
    {
        private readonly IToDoListService _service;
        private readonly IUserService _userService;
        private readonly IUserPermissionService _permissions;
        private readonly IMapper _mapper;
        private readonly ToDoListSupport _support;

        public UpdateToDoListCommandHandler(IToDoListService service, IUserService userService, IUserPermissionService permissions, IMapper mapper, ToDoListSupport support)
        {
            _service = service;
            _userService = userService;
            _permissions = permissions;
            _mapper = mapper;
            _support = support;
        }

        public async Task<ToDoListResponseDto> Handle(UpdateToDoListCommand request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            var entity = await _support.LoadOwnedAsync(viewer, request.Id, "لا يمكنك تعديل قائمة غيرك");

            _mapper.Map(request.Dto, entity);
            ToDoListSupport.Trim(entity);
            if (entity.IsArchived) throw new InvalidOperationException("القائمة مؤرشفة — أعدها من الأرشيف قبل تعديلها");

            if (await _service.ExistsByNameAsync(entity.UserId, entity.Name, entity.Id))
                throw new InvalidOperationException("توجد قائمة أخرى بنفس الاسم");

            await _service.UpdateAsync(entity);
            return _mapper.Map<ToDoListResponseDto>(entity);
        }

        private Task<Viewer> ViewerAsync() => Viewer.CurrentAsync(_userService, _permissions);
    }
}
