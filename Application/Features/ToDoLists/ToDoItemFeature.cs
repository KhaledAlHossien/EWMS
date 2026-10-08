using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.AssignedTasks;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using FluentValidation;
using MediatR;

namespace Application.Features.ToDoLists
{
    // مفكرتي (قرار المستخدم 2026-10-08): بنود القوائم الشخصية وما حولها. كلها على قوائمي فقط (والسوبر ادمن).
    // كل عملية على البنود تُرجع القائمة بعد التحديث (البنود مرتبة والأعداد) ليستبدلها العميل كما هي.
    public record AddToDoItemCommand(int ListId, AddToDoItemRequestDto Dto) : IRequest<ToDoListResponseDto>;
    public record BulkAddToDoItemsCommand(int ListId, List<string> Titles) : IRequest<ToDoListResponseDto>;
    public record AddTaskToToDoListCommand(int ListId, int TaskId) : IRequest<ToDoListResponseDto>;
    public record UpdateToDoItemCommand(int ItemId, UpdateToDoItemRequestDto Dto) : IRequest<ToDoListResponseDto>;
    public record DeleteToDoItemCommand(int ItemId) : IRequest<ToDoListResponseDto>;
    public record ReorderToDoItemsCommand(int ListId, List<int> ItemIds) : IRequest<ToDoListResponseDto>;
    public record ClearDoneToDoItemsCommand(int ListId) : IRequest<ToDoListResponseDto>;

    public record SetToDoListPinnedCommand(int ListId, bool Value) : IRequest<ToDoListResponseDto>;
    public record SetToDoListArchivedCommand(int ListId, bool Value) : IRequest<ToDoListResponseDto>;
    public record DuplicateToDoListCommand(int ListId) : IRequest<ToDoListResponseDto>;
    /// <summary>«مهامي اليوم»: المتأخرة والمستحقة اليوم من كل قوائمي</summary>
    public record GetToDoTodayQuery : IRequest<ToDoTodayDto>;

    // ════════════════════ التحقق ════════════════════

    public class AddToDoItemCommandValidator : AbstractValidator<AddToDoItemCommand>
    {
        public AddToDoItemCommandValidator()
        {
            RuleFor(x => x.Dto.Title).NotEmpty().WithMessage("عنوان البند مطلوب")
                .MaximumLength(ToDoRules.MaxTitleLength).WithMessage($"عنوان البند لا يتجاوز {ToDoRules.MaxTitleLength} حرف");
            RuleFor(x => x.Dto.Note).MaximumLength(ToDoRules.MaxNoteLength).WithMessage($"الملاحظة لا تتجاوز {ToDoRules.MaxNoteLength} حرف");
            RuleFor(x => x.Dto).Must(d => ToDoRules.ParseRepeat(d.Repeat) == null || d.DueDate != null).WithMessage("البند المتكرر يحتاج موعداً");
        }
    }

    public class BulkAddToDoItemsCommandValidator : AbstractValidator<BulkAddToDoItemsCommand>
    {
        public BulkAddToDoItemsCommandValidator()
        {
            RuleFor(x => x.Titles).Must(t => t.Any(s => !string.IsNullOrWhiteSpace(s))).WithMessage("لا توجد بنود للإضافة")
                .Must(t => t.Count <= ToDoRules.MaxBulkItems).WithMessage($"الحد الأقصى {ToDoRules.MaxBulkItems} بنداً في المرة الواحدة");
            RuleForEach(x => x.Titles).MaximumLength(ToDoRules.MaxTitleLength).WithMessage($"عنوان البند لا يتجاوز {ToDoRules.MaxTitleLength} حرف");
        }
    }

    public class UpdateToDoItemCommandValidator : AbstractValidator<UpdateToDoItemCommand>
    {
        public UpdateToDoItemCommandValidator()
        {
            RuleFor(x => x.Dto).Must(d => d.Title != null || d.IsDone != null || d.Note != null || d.IsImportant != null
                    || d.DueDate != null || d.ClearDueDate || d.Repeat != null).WithMessage("لا يوجد ما يُعدَّل");
            RuleFor(x => x.Dto.Title).NotEmpty().When(x => x.Dto.Title != null).WithMessage("عنوان البند مطلوب")
                .MaximumLength(ToDoRules.MaxTitleLength).WithMessage($"عنوان البند لا يتجاوز {ToDoRules.MaxTitleLength} حرف");
            RuleFor(x => x.Dto.Note).MaximumLength(ToDoRules.MaxNoteLength).WithMessage($"الملاحظة لا تتجاوز {ToDoRules.MaxNoteLength} حرف");
        }
    }

    public class ToDoItemHandler :
        IRequestHandler<AddToDoItemCommand, ToDoListResponseDto>,
        IRequestHandler<BulkAddToDoItemsCommand, ToDoListResponseDto>,
        IRequestHandler<AddTaskToToDoListCommand, ToDoListResponseDto>,
        IRequestHandler<UpdateToDoItemCommand, ToDoListResponseDto>,
        IRequestHandler<DeleteToDoItemCommand, ToDoListResponseDto>,
        IRequestHandler<ReorderToDoItemsCommand, ToDoListResponseDto>,
        IRequestHandler<ClearDoneToDoItemsCommand, ToDoListResponseDto>,
        IRequestHandler<SetToDoListPinnedCommand, ToDoListResponseDto>,
        IRequestHandler<SetToDoListArchivedCommand, ToDoListResponseDto>,
        IRequestHandler<DuplicateToDoListCommand, ToDoListResponseDto>,
        IRequestHandler<GetToDoTodayQuery, ToDoTodayDto>
    {
        private readonly IToDoListService _service;
        private readonly IAssignedTaskService _tasks;
        private readonly IUserService _userService;
        private readonly IUserPermissionService _permissions;
        private readonly IMapper _mapper;

        public ToDoItemHandler(IToDoListService service, IAssignedTaskService tasks, IUserService userService,
            IUserPermissionService permissions, IMapper mapper)
        {
            _service = service;
            _tasks = tasks;
            _userService = userService;
            _permissions = permissions;
            _mapper = mapper;
        }

        private Task<Viewer> ViewerAsync() => Viewer.CurrentAsync(_userService, _permissions);

        private static void EnsureOwner(Viewer viewer, ToDoList list)
        {
            if (!viewer.IsSuperAdmin && list.UserId != viewer.Id)
                throw new UnauthorizedAccessException("لا يمكنك تعديل قائمة غيرك");
        }

        private static void EnsureNotArchived(ToDoList list)
        {
            if (list.IsArchived) throw new InvalidOperationException("القائمة مؤرشفة — أعدها من الأرشيف قبل تعديل بنودها");
        }

        private async Task<(Viewer Viewer, ToDoList List)> LoadListAsync(int listId, bool forEdit = true)
        {
            var viewer = await ViewerAsync();
            var list = await _service.GetByIdAsync(listId) ?? throw new KeyNotFoundException("القائمة غير موجودة");
            EnsureOwner(viewer, list);
            if (forEdit) EnsureNotArchived(list);
            return (viewer, list);
        }

        /// <summary>القائمة بعد التحديث (تُعاد قراءتها من القاعدة) مع المهام المرتبطة</summary>
        private async Task<ToDoListResponseDto> ResultAsync(int listId, Viewer viewer)
        {
            var entity = await _service.GetByIdAsync(listId) ?? throw new KeyNotFoundException("القائمة غير موجودة");
            var dto = _mapper.Map<ToDoListResponseDto>(entity);
            ToDoRules.FillLinks(entity, dto, viewer);
            return dto;
        }

        private static string Clean(string? text) => (text ?? string.Empty).Trim();

        // ─────────── إضافة ───────────
        public async Task<ToDoListResponseDto> Handle(AddToDoItemCommand request, CancellationToken ct)
        {
            var (viewer, list) = await LoadListAsync(request.ListId);
            if (list.Items.Count >= ToDoRules.MaxItemsPerList)
                throw new InvalidOperationException($"الحد الأقصى {ToDoRules.MaxItemsPerList} بنداً للقائمة");

            var dto = request.Dto;
            await _service.AddItemAsync(new ToDoItem
            {
                ToDoListId = list.Id,
                Title = dto.Title.Trim(),
                Note = Clean(dto.Note),
                IsImportant = dto.IsImportant,
                DueDate = dto.DueDate?.Date,
                Repeat = ToDoRules.ParseRepeat(dto.Repeat),
                SortOrder = await _service.NextSortOrderAsync(list.Id),
                CreatedAt = DateTime.UtcNow
            });
            return await ResultAsync(list.Id, viewer);
        }

        public async Task<ToDoListResponseDto> Handle(BulkAddToDoItemsCommand request, CancellationToken ct)
        {
            var (viewer, list) = await LoadListAsync(request.ListId);
            var titles = request.Titles.Select(Clean).Where(t => t.Length > 0).ToList();
            if (list.Items.Count + titles.Count > ToDoRules.MaxItemsPerList)
                throw new InvalidOperationException($"لا تتسع القائمة: الحد الأقصى {ToDoRules.MaxItemsPerList} بنداً وفيها {list.Items.Count}");

            var order = await _service.NextSortOrderAsync(list.Id);
            var now = DateTime.UtcNow;
            await _service.AddItemsAsync(titles.Select((t, i) => new ToDoItem
            {
                ToDoListId = list.Id, Title = t, SortOrder = order + i, CreatedAt = now
            }));
            return await ResultAsync(list.Id, viewer);
        }

        public async Task<ToDoListResponseDto> Handle(AddTaskToToDoListCommand request, CancellationToken ct)
        {
            var (viewer, list) = await LoadListAsync(request.ListId);
            if (!viewer.IsSuperAdmin && !viewer.Has(AppPermissions.ViewTaskBoard))
                throw new UnauthorizedAccessException("لا تملك صلاحية لوحة المهام");

            var task = await _tasks.GetByIdAsync(request.TaskId) ?? throw new KeyNotFoundException("المهمة غير موجودة");
            if (!AssignedTaskRules.CanView(task, viewer)) throw new UnauthorizedAccessException("لا تملك صلاحية عرض هذه المهمة");
            if (list.Items.Any(i => i.LinkedTaskId == task.Id)) throw new InvalidOperationException("هذه المهمة مضافة إلى القائمة بالفعل");
            if (list.Items.Count >= ToDoRules.MaxItemsPerList)
                throw new InvalidOperationException($"الحد الأقصى {ToDoRules.MaxItemsPerList} بنداً للقائمة");

            await _service.AddItemAsync(new ToDoItem
            {
                ToDoListId = list.Id,
                Title = task.Title.Length > ToDoRules.MaxTitleLength ? task.Title[..ToDoRules.MaxTitleLength] : task.Title,
                DueDate = task.DueDate?.Date,
                LinkedTaskId = task.Id,
                SortOrder = await _service.NextSortOrderAsync(list.Id),
                CreatedAt = DateTime.UtcNow
            });
            return await ResultAsync(list.Id, viewer);
        }

        // ─────────── تعديل ───────────
        public async Task<ToDoListResponseDto> Handle(UpdateToDoItemCommand request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            var item = await _service.GetItemAsync(request.ItemId) ?? throw new KeyNotFoundException("البند غير موجود");
            EnsureOwner(viewer, item.ToDoList);
            EnsureNotArchived(item.ToDoList);

            var dto = request.Dto;
            var today = DateTime.Today;
            if (dto.Title != null) item.Title = dto.Title.Trim();
            if (dto.Note != null) item.Note = Clean(dto.Note);
            if (dto.IsImportant is bool important) item.IsImportant = important;

            var dueBefore = item.DueDate;
            if (dto.ClearDueDate) { item.DueDate = null; item.Repeat = null; }
            else if (dto.DueDate != null) item.DueDate = dto.DueDate.Value.Date;
            if (dto.Repeat != null) item.Repeat = ToDoRules.ParseRepeat(dto.Repeat);
            if (item.Repeat != null && item.DueDate == null) throw new InvalidOperationException("البند المتكرر يحتاج موعداً");
            if (item.DueDate != dueBefore) { item.DueSoonNotifiedAt = null; item.OverdueNotifiedAt = null; }

            if (dto.IsDone is bool done && done != item.IsDone)
            {
                if (done && item.Repeat is { } repeat && item.DueDate is { } due)
                {
                    // المتكرر لا يبقى منجزاً: يعود مفتوحاً بموعده التالي
                    item.DueDate = ToDoRules.NextDue(repeat, due, today);
                    item.LastCompletedAt = DateTime.UtcNow;
                    item.DueSoonNotifiedAt = null; item.OverdueNotifiedAt = null;
                }
                else
                {
                    item.IsDone = done;
                    item.DoneAt = done ? DateTime.UtcNow : null;
                    if (done) item.LastCompletedAt = item.DoneAt;
                }
            }
            await _service.SaveChangesAsync();
            return await ResultAsync(item.ToDoListId, viewer);
        }

        public async Task<ToDoListResponseDto> Handle(DeleteToDoItemCommand request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            var item = await _service.GetItemAsync(request.ItemId) ?? throw new KeyNotFoundException("البند غير موجود");
            EnsureOwner(viewer, item.ToDoList);
            EnsureNotArchived(item.ToDoList);

            var listId = item.ToDoListId;
            await _service.DeleteItemsAsync([item]);
            return await ResultAsync(listId, viewer);
        }

        public async Task<ToDoListResponseDto> Handle(ReorderToDoItemsCommand request, CancellationToken ct)
        {
            var (viewer, list) = await LoadListAsync(request.ListId);

            // الترتيب الجديد يجب أن يضم كل بنود القائمة مرة واحدة: لا ناقص ولا زائد ولا مكرر ولا من قائمة أخرى
            var ids = request.ItemIds;
            if (ids.Count != list.Items.Count || ids.Distinct().Count() != ids.Count || !list.Items.All(i => ids.Contains(i.Id)))
                throw new InvalidOperationException("الترتيب لا يطابق بنود القائمة — حدّث الصفحة وأعد المحاولة");

            var order = ids.Select((id, index) => (id, index)).ToDictionary(x => x.id, x => x.index);
            foreach (var item in list.Items) item.SortOrder = order[item.Id];
            await _service.SaveChangesAsync();
            return await ResultAsync(list.Id, viewer);
        }

        public async Task<ToDoListResponseDto> Handle(ClearDoneToDoItemsCommand request, CancellationToken ct)
        {
            var (viewer, list) = await LoadListAsync(request.ListId);
            var done = list.Items.Where(i => i.IsDone).ToList();
            if (done.Count > 0) await _service.DeleteItemsAsync(done);
            return await ResultAsync(list.Id, viewer);
        }

        // ─────────── تثبيت وأرشفة ونسخ ───────────
        public async Task<ToDoListResponseDto> Handle(SetToDoListPinnedCommand request, CancellationToken ct)
        {
            var (viewer, list) = await LoadListAsync(request.ListId, forEdit: false);
            list.IsPinned = request.Value;
            await _service.SaveChangesAsync();
            return await ResultAsync(list.Id, viewer);
        }

        public async Task<ToDoListResponseDto> Handle(SetToDoListArchivedCommand request, CancellationToken ct)
        {
            var (viewer, list) = await LoadListAsync(request.ListId, forEdit: false);
            list.IsArchived = request.Value;
            if (request.Value) list.IsPinned = false;            // المؤرشفة لا تُثبَّت
            await _service.SaveChangesAsync();
            return await ResultAsync(list.Id, viewer);
        }

        /// <summary>نسخة من القائمة ببنودها غير منجزة وبلا مواعيد ولا تكرار ولا روابط (قالب جاهز لدورة جديدة)</summary>
        public async Task<ToDoListResponseDto> Handle(DuplicateToDoListCommand request, CancellationToken ct)
        {
            var (viewer, source) = await LoadListAsync(request.ListId, forEdit: false);

            var baseName = source.Name.Length > 180 ? source.Name[..180] : source.Name;
            var name = $"{baseName} (نسخة)";
            for (var n = 2; await _service.ExistsByNameAsync(source.UserId, name); n++) name = $"{baseName} (نسخة {n})";

            var now = DateTime.UtcNow;
            var copy = new ToDoList
            {
                UserId = source.UserId, Name = name, Description = source.Description, Color = source.Color, Icon = source.Icon,
                Items = source.Items.OrderBy(i => i.SortOrder).ThenBy(i => i.Id).Select((i, index) => new ToDoItem
                {
                    Title = i.Title, Note = i.Note, IsImportant = i.IsImportant, SortOrder = index, CreatedAt = now
                }).ToList()
            };
            var created = await _service.AddAsync(copy);
            return await ResultAsync(created.Id, viewer);
        }

        // ─────────── مهامي اليوم ───────────
        public async Task<ToDoTodayDto> Handle(GetToDoTodayQuery request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            var today = DateTime.Today;
            var items = await _service.GetDueItemsAsync(viewer.Id, today);

            var dtos = items
                .OrderBy(i => i.DueDate).ThenByDescending(i => i.IsImportant).ThenBy(i => i.ToDoList.Name).ThenBy(i => i.SortOrder)
                .Select(i =>
                {
                    var dto = _mapper.Map<ToDoTodayItemDto>(i);
                    dto.ListName = i.ToDoList.Name; dto.ListColor = i.ToDoList.Color; dto.ListIcon = i.ToDoList.Icon;
                    if (i.LinkedTask != null) dto.LinkedTask = ToDoRules.LinkInfo(i.LinkedTask, viewer, today);
                    return dto;
                }).ToList();

            return new ToDoTodayDto
            {
                Items = dtos,
                OverdueCount = dtos.Count(d => d.IsOverdue),
                TodayCount = dtos.Count(d => !d.IsOverdue)
            };
        }
    }
}
