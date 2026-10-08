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
    // بنود القوائم الشخصية (قرار المستخدم 2026-10-08): عنوان + منجز + ترتيب. تتبع حدّ القائمة: صاحبها فقط (والسوبر ادمن).
    // كل العمليات تُرجع القائمة بعد التحديث (بالبنود مرتبة وبالأعداد) ليستبدلها العميل كما هي.
    public record AddToDoItemCommand(int ListId, AddToDoItemRequestDto Dto) : IRequest<ToDoListResponseDto>;
    public record UpdateToDoItemCommand(int ItemId, UpdateToDoItemRequestDto Dto) : IRequest<ToDoListResponseDto>;
    public record DeleteToDoItemCommand(int ItemId) : IRequest<ToDoListResponseDto>;
    public record ReorderToDoItemsCommand(int ListId, List<int> ItemIds) : IRequest<ToDoListResponseDto>;
    public record ClearDoneToDoItemsCommand(int ListId) : IRequest<ToDoListResponseDto>;

    public static class ToDoItemRules
    {
        public const int MaxItemsPerList = 200;
        public const int MaxTitleLength = 200;
    }

    public class AddToDoItemCommandValidator : AbstractValidator<AddToDoItemCommand>
    {
        public AddToDoItemCommandValidator() =>
            RuleFor(x => x.Dto.Title).NotEmpty().WithMessage("عنوان البند مطلوب")
                .MaximumLength(ToDoItemRules.MaxTitleLength).WithMessage($"عنوان البند لا يتجاوز {ToDoItemRules.MaxTitleLength} حرف");
    }

    public class UpdateToDoItemCommandValidator : AbstractValidator<UpdateToDoItemCommand>
    {
        public UpdateToDoItemCommandValidator()
        {
            RuleFor(x => x.Dto).Must(d => d.Title != null || d.IsDone != null).WithMessage("لا يوجد ما يُعدَّل");
            RuleFor(x => x.Dto.Title).NotEmpty().When(x => x.Dto.Title != null).WithMessage("عنوان البند مطلوب")
                .MaximumLength(ToDoItemRules.MaxTitleLength).WithMessage($"عنوان البند لا يتجاوز {ToDoItemRules.MaxTitleLength} حرف");
        }
    }

    public class ToDoItemHandler :
        IRequestHandler<AddToDoItemCommand, ToDoListResponseDto>,
        IRequestHandler<UpdateToDoItemCommand, ToDoListResponseDto>,
        IRequestHandler<DeleteToDoItemCommand, ToDoListResponseDto>,
        IRequestHandler<ReorderToDoItemsCommand, ToDoListResponseDto>,
        IRequestHandler<ClearDoneToDoItemsCommand, ToDoListResponseDto>
    {
        private readonly IToDoListService _service;
        private readonly IUserService _userService;
        private readonly IUserPermissionService _permissions;
        private readonly IMapper _mapper;

        public ToDoItemHandler(IToDoListService service, IUserService userService, IUserPermissionService permissions, IMapper mapper)
        {
            _service = service;
            _userService = userService;
            _permissions = permissions;
            _mapper = mapper;
        }

        private static void EnsureOwner(Viewer viewer, ToDoList list)
        {
            if (!viewer.IsSuperAdmin && list.UserId != viewer.Id)
                throw new UnauthorizedAccessException("لا يمكنك تعديل قائمة غيرك");
        }

        private async Task<(Viewer Viewer, ToDoList List)> LoadListAsync(int listId)
        {
            var viewer = await Viewer.CurrentAsync(_userService, _permissions);
            var list = await _service.GetByIdAsync(listId) ?? throw new KeyNotFoundException("القائمة غير موجودة");
            EnsureOwner(viewer, list);
            return (viewer, list);
        }

        private async Task<ToDoListResponseDto> ResultAsync(int listId) =>
            _mapper.Map<ToDoListResponseDto>(await _service.GetByIdAsync(listId));

        public async Task<ToDoListResponseDto> Handle(AddToDoItemCommand request, CancellationToken ct)
        {
            var (_, list) = await LoadListAsync(request.ListId);
            if (list.Items.Count >= ToDoItemRules.MaxItemsPerList)
                throw new InvalidOperationException($"الحد الأقصى {ToDoItemRules.MaxItemsPerList} بنداً للقائمة");

            await _service.AddItemAsync(new ToDoItem
            {
                ToDoListId = list.Id,
                Title = request.Dto.Title.Trim(),
                SortOrder = await _service.NextSortOrderAsync(list.Id),
                CreatedAt = DateTime.UtcNow
            });
            return await ResultAsync(list.Id);
        }

        public async Task<ToDoListResponseDto> Handle(UpdateToDoItemCommand request, CancellationToken ct)
        {
            var viewer = await Viewer.CurrentAsync(_userService, _permissions);
            var item = await _service.GetItemAsync(request.ItemId) ?? throw new KeyNotFoundException("البند غير موجود");
            EnsureOwner(viewer, item.ToDoList);

            var dto = request.Dto;
            if (dto.Title != null) item.Title = dto.Title.Trim();
            if (dto.IsDone is bool done && done != item.IsDone)
            {
                item.IsDone = done;
                item.DoneAt = done ? DateTime.UtcNow : null;
            }
            await _service.SaveChangesAsync();
            return await ResultAsync(item.ToDoListId);
        }

        public async Task<ToDoListResponseDto> Handle(DeleteToDoItemCommand request, CancellationToken ct)
        {
            var viewer = await Viewer.CurrentAsync(_userService, _permissions);
            var item = await _service.GetItemAsync(request.ItemId) ?? throw new KeyNotFoundException("البند غير موجود");
            EnsureOwner(viewer, item.ToDoList);

            var listId = item.ToDoListId;
            await _service.DeleteItemsAsync([item]);
            return await ResultAsync(listId);
        }

        public async Task<ToDoListResponseDto> Handle(ReorderToDoItemsCommand request, CancellationToken ct)
        {
            var (_, list) = await LoadListAsync(request.ListId);

            // الترتيب الجديد يجب أن يضم كل بنود القائمة مرة واحدة: لا ناقص ولا زائد ولا مكرر ولا من قائمة أخرى
            var ids = request.ItemIds;
            if (ids.Count != list.Items.Count || ids.Distinct().Count() != ids.Count || !list.Items.All(i => ids.Contains(i.Id)))
                throw new InvalidOperationException("الترتيب لا يطابق بنود القائمة — حدّث الصفحة وأعد المحاولة");

            var order = ids.Select((id, index) => (id, index)).ToDictionary(x => x.id, x => x.index);
            foreach (var item in list.Items) item.SortOrder = order[item.Id];
            await _service.SaveChangesAsync();
            return await ResultAsync(list.Id);
        }

        public async Task<ToDoListResponseDto> Handle(ClearDoneToDoItemsCommand request, CancellationToken ct)
        {
            var (_, list) = await LoadListAsync(request.ListId);
            var done = list.Items.Where(i => i.IsDone).ToList();
            if (done.Count > 0) await _service.DeleteItemsAsync(done);
            return await ResultAsync(list.Id);
        }
    }
}
