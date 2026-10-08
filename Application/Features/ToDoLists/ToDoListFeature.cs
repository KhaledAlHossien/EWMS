using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using FluentValidation;
using MediatR;

namespace Application.Features.ToDoLists
{
    // قوائم المهام الشخصية: كل صلاحية على قوائم المستخدم نفسه فقط، والسوبر ادمن على الكل (حدّ ثابت، لا نطاقات)
    /// <summary>قوائمي. السوبر ادمن: كل القوائم، أو قوائم مستخدم معيّن بـ OwnerId</summary>
    public record GetAllToDoListsQuery(int? OwnerId) : IRequest<List<ToDoListResponseDto>>;
    public record GetToDoListByIdQuery(int Id) : IRequest<ToDoListResponseDto>;
    public record CreateToDoListCommand(ToDoListRequestDto Dto) : IRequest<ToDoListResponseDto>;
    public record UpdateToDoListCommand(int Id, ToDoListRequestDto Dto) : IRequest<ToDoListResponseDto>;
    public record DeleteToDoListCommand(int Id) : IRequest<Unit>;

    // ════════════════════ التحقق من المدخلات ════════════════════

    public class ToDoListRequestDtoValidator : AbstractValidator<ToDoListRequestDto>
    {
        public ToDoListRequestDtoValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("اسم القائمة مطلوب")
                .MaximumLength(200).WithMessage("اسم القائمة لا يتجاوز 200 حرف");

            RuleFor(x => x.Description)
                .MaximumLength(2000).WithMessage("الوصف لا يتجاوز 2000 حرف");
        }
    }

    public class CreateToDoListCommandValidator : AbstractValidator<CreateToDoListCommand>
    {
        public CreateToDoListCommandValidator() =>
            RuleFor(x => x.Dto).SetValidator(new ToDoListRequestDtoValidator());
    }

    public class UpdateToDoListCommandValidator : AbstractValidator<UpdateToDoListCommand>
    {
        public UpdateToDoListCommandValidator() =>
            RuleFor(x => x.Dto).SetValidator(new ToDoListRequestDtoValidator());
    }

    // ════════════════════ المعالج ════════════════════

    public class ToDoListHandler :
        IRequestHandler<GetAllToDoListsQuery, List<ToDoListResponseDto>>,
        IRequestHandler<GetToDoListByIdQuery, ToDoListResponseDto>,
        IRequestHandler<CreateToDoListCommand, ToDoListResponseDto>,
        IRequestHandler<UpdateToDoListCommand, ToDoListResponseDto>,
        IRequestHandler<DeleteToDoListCommand, Unit>
    {
        private readonly IToDoListService _service;
        private readonly IUserService _userService;
        private readonly IUserPermissionService _permissions;
        private readonly IMapper _mapper;

        public ToDoListHandler(
            IToDoListService service,
            IUserService userService,
            IUserPermissionService permissions,
            IMapper mapper)
        {
            _service = service;
            _userService = userService;
            _permissions = permissions;
            _mapper = mapper;
        }

        private Task<Viewer> ViewerAsync() => Viewer.CurrentAsync(_userService, _permissions);

        // القائمة لصاحبها فقط (والسوبر ادمن)، ويُرفض غيره بـ 403
        private async Task<ToDoList> LoadOwnedAsync(Viewer viewer, int id, string deniedMessage)
        {
            var list = await _service.GetByIdAsync(id) ?? throw new KeyNotFoundException("القائمة غير موجودة");

            if (!viewer.IsSuperAdmin && list.UserId != viewer.Id)
                throw new UnauthorizedAccessException(deniedMessage);

            return list;
        }

        private static void Trim(ToDoList l)
        {
            l.Name = l.Name.Trim();
            l.Description = l.Description.Trim();
        }

        public async Task<List<ToDoListResponseDto>> Handle(GetAllToDoListsQuery request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            var ownerId = viewer.IsSuperAdmin ? request.OwnerId : viewer.Id;

            var lists = _mapper.Map<List<ToDoListResponseDto>>(await _service.GetAllAsync(ownerId));
            foreach (var l in lists) l.Items = [];      // القائمة الرئيسية بالأعداد فقط، والبنود في Get/{id}
            return lists;
        }

        public async Task<ToDoListResponseDto> Handle(GetToDoListByIdQuery request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            return _mapper.Map<ToDoListResponseDto>(
                await LoadOwnedAsync(viewer, request.Id, "لا يمكنك عرض قائمة غيرك"));
        }

        public async Task<ToDoListResponseDto> Handle(CreateToDoListCommand request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();

            var entity = _mapper.Map<ToDoList>(request.Dto);
            Trim(entity);

            // صاحب القائمة = من أنشأها ولا يتغير
            entity.UserId = viewer.Id;

            if (await _service.ExistsByNameAsync(viewer.Id, entity.Name))
                throw new InvalidOperationException("لديك قائمة بنفس الاسم مسبقاً");

            var created = await _service.AddAsync(entity);
            return _mapper.Map<ToDoListResponseDto>(await _service.GetByIdAsync(created.Id) ?? created);
        }

        public async Task<ToDoListResponseDto> Handle(UpdateToDoListCommand request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            var entity = await LoadOwnedAsync(viewer, request.Id, "لا يمكنك تعديل قائمة غيرك");

            _mapper.Map(request.Dto, entity);
            Trim(entity);

            if (await _service.ExistsByNameAsync(entity.UserId, entity.Name, entity.Id))
                throw new InvalidOperationException("توجد قائمة أخرى بنفس الاسم");

            await _service.UpdateAsync(entity);
            return _mapper.Map<ToDoListResponseDto>(entity);
        }

        public async Task<Unit> Handle(DeleteToDoListCommand request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            var entity = await LoadOwnedAsync(viewer, request.Id, "لا يمكنك حذف قائمة غيرك");

            await _service.DeleteAsync(entity);
            return Unit.Value;
        }
    }
}
