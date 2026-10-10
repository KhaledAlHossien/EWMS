using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using MediatR;

namespace Application.Features.AssignedTasks
{
    // مرفقات المهمة، قائمة التحقق، و«أتولّى هذه المهمة» (قرارات المستخدم 2026-10-07). كلها تُرجع تفصيل المهمة محدَّثاً.
    public record UploadTaskAttachmentsCommand(int TaskId, List<UploadedFileDto> Files) : IRequest<AssignedTaskDetailDto>;
    public record GetTaskAttachmentQuery(int AttachmentId) : IRequest<TaskAttachmentFileDto>;
    public record DeleteTaskAttachmentCommand(int AttachmentId) : IRequest<AssignedTaskDetailDto>;
    public record AddChecklistItemCommand(int TaskId, string Text) : IRequest<AssignedTaskDetailDto>;
    public record UpdateChecklistItemCommand(int ItemId, bool? IsDone, string? Text) : IRequest<AssignedTaskDetailDto>;
    public record DeleteChecklistItemCommand(int ItemId) : IRequest<AssignedTaskDetailDto>;
    public record ClaimAssignedTaskCommand(int TaskId, bool Claim) : IRequest<AssignedTaskDetailDto>;

    public record TaskAttachmentFileDto(string FileName, string ContentType, byte[] Data, bool Previewable);

    public class AddChecklistItemCommandValidator : AbstractValidator<AddChecklistItemCommand>
    {
        public AddChecklistItemCommandValidator() =>
            RuleFor(x => x.Text).Must(t => !string.IsNullOrWhiteSpace(t)).WithMessage("اكتب نص البند")
                .MaximumLength(200).WithMessage("البند لا يتجاوز 200 حرف");
    }

    public class UpdateChecklistItemCommandValidator : AbstractValidator<UpdateChecklistItemCommand>
    {
        public UpdateChecklistItemCommandValidator() =>
            RuleFor(x => x.Text).MaximumLength(200).WithMessage("البند لا يتجاوز 200 حرف")
                .Must(t => t == null || !string.IsNullOrWhiteSpace(t)).WithMessage("نص البند لا يكون فارغاً");
    }

    public class AssignedTaskExtrasHandler :
        IRequestHandler<UploadTaskAttachmentsCommand, AssignedTaskDetailDto>,
        IRequestHandler<GetTaskAttachmentQuery, TaskAttachmentFileDto>,
        IRequestHandler<DeleteTaskAttachmentCommand, AssignedTaskDetailDto>,
        IRequestHandler<AddChecklistItemCommand, AssignedTaskDetailDto>,
        IRequestHandler<UpdateChecklistItemCommand, AssignedTaskDetailDto>,
        IRequestHandler<DeleteChecklistItemCommand, AssignedTaskDetailDto>,
        IRequestHandler<ClaimAssignedTaskCommand, AssignedTaskDetailDto>
    {
        private readonly IAssignedTaskService _tasks;
        private readonly IUserService _users;
        private readonly IUserPermissionService _permissions;
        private readonly INotificationService _notifications;

        public AssignedTaskExtrasHandler(IAssignedTaskService tasks, IUserService users, IUserPermissionService permissions, INotificationService notifications)
        {
            _tasks = tasks;
            _users = users;
            _permissions = permissions;
            _notifications = notifications;
        }

        private Task<Viewer> CurrentAsync() => Viewer.CurrentAsync(_users, _permissions);

        private async Task<AssignedTask> LoadAsync(int id) =>
            await _tasks.GetByIdAsync(id) ?? throw new KeyNotFoundException("المهمة غير موجودة");

        private async Task<AssignedTaskDetailDto> DetailAsync(int id, Viewer viewer) =>
            AssignedTaskRules.ToDetail(await LoadAsync(id), viewer);

        private static void EnsureOpen(AssignedTask t, Viewer viewer)
        {
            if (t.Status == AssignedTaskStatus.Done)
                throw new InvalidOperationException("المهمة منجزة — سجلها ومرفقاتها وقائمتها ثابتة لا تتغيّر");
            AssignedTaskRules.EnsureNotFrozen(t, viewer);
        }

        // ════════════ المرفقات ════════════

        public async Task<AssignedTaskDetailDto> Handle(UploadTaskAttachmentsCommand request, CancellationToken ct)
        {
            var viewer = await CurrentAsync();
            var task = await LoadAsync(request.TaskId);
            if (!AssignedTaskRules.CanView(task, viewer))
                throw new UnauthorizedAccessException("لا تملك صلاحية الإرفاق إلى هذه المهمة");
            EnsureOpen(task, viewer);

            if (request.Files.Count == 0) throw new InvalidOperationException("اختر ملفاً للإرفاق");
            var existing = await _tasks.CountAttachmentsAsync(task.Id);
            if (existing + request.Files.Count > TaskAttachmentRules.MaxFiles)
                throw new InvalidOperationException($"الحد الأقصى {TaskAttachmentRules.MaxFiles} ملفات للمهمة (مرفق الآن {existing})");

            var now = DateTime.UtcNow;
            var created = new List<AssignedTaskAttachment>();
            foreach (var file in request.Files)
            {
                if (TaskAttachmentRules.Problem(file) is string problem) throw new InvalidOperationException(problem);
                created.Add(new AssignedTaskAttachment
                {
                    AssignedTaskId = task.Id,
                    FileName = TaskAttachmentRules.SafeFileName(file.FileName),
                    ContentType = TaskAttachmentRules.DetectContentType(file.Data, file.FileName)!,
                    Size = file.Data.Length,
                    UploadedByUserId = viewer.Id,
                    UploadedAt = now,
                    Content = new AssignedTaskAttachmentContent { Data = file.Data }
                });
            }
            await _tasks.AddAttachmentsAsync(created);

            var names = string.Join("، ", created.Select(a => $"«{a.FileName}»"));
            await _tasks.AddActivityAsync(AssignedTaskRules.Activity(task, viewer.User, AssignedTaskActivityType.Attached, $"أرفق {names}"));

            // المُسنِد والمنفِّذون (عدا الرافع)
            var recipients = (await AssignedTaskRules.HandlerUserIdsAsync(_permissions, task)).Append(task.CreatedByUserId);
            await AssignedTaskRules.NotifyAsync(_notifications, recipients, viewer.Id, task, NotificationType.TaskAttachmentAdded,
                "ملف جديد على مهمة", $"أرفق {viewer.User.FullName} {names} بالمهمة «{task.Title}»");

            return await DetailAsync(task.Id, viewer);
        }

        /// <summary>
        /// لمن يطّلع على المهمة صاحبة المرفق، أو على إحدى مهامها الفرعية المفوَّضة (ترى مرفقات الأصل للقراءة فقط).
        /// </summary>
        public async Task<TaskAttachmentFileDto> Handle(GetTaskAttachmentQuery request, CancellationToken ct)
        {
            var attachment = await _tasks.GetAttachmentAsync(request.AttachmentId)
                ?? throw new KeyNotFoundException("المرفق غير موجود");
            var viewer = await CurrentAsync();

            var allowed = AssignedTaskRules.CanView(attachment.AssignedTask, viewer);
            if (!allowed)
                allowed = (await _tasks.GetChildrenAsync(attachment.AssignedTaskId)).Any(child => AssignedTaskRules.CanView(child, viewer));
            if (!allowed)
                throw new UnauthorizedAccessException("لا تملك صلاحية عرض مرفقات هذه المهمة");

            return new TaskAttachmentFileDto(attachment.FileName, attachment.ContentType, attachment.Content.Data,
                TaskAttachmentRules.IsPreviewable(attachment.ContentType));
        }

        public async Task<AssignedTaskDetailDto> Handle(DeleteTaskAttachmentCommand request, CancellationToken ct)
        {
            var attachment = await _tasks.GetAttachmentAsync(request.AttachmentId)
                ?? throw new KeyNotFoundException("المرفق غير موجود");
            var viewer = await CurrentAsync();
            var task = await LoadAsync(attachment.AssignedTaskId);

            if (attachment.UploadedByUserId != viewer.Id && !AssignedTaskRules.IsReviewer(task, viewer))
                throw new UnauthorizedAccessException("يحذف المرفق من رفعه أو من أسند المهمة");
            EnsureOpen(task, viewer);

            var name = attachment.FileName;
            await _tasks.DeleteAttachmentAsync(attachment);
            await _tasks.AddActivityAsync(AssignedTaskRules.Activity(task, viewer.User, AssignedTaskActivityType.AttachmentRemoved, $"حذف المرفق «{name}»"));
            return await DetailAsync(task.Id, viewer);
        }

        // ════════════ قائمة التحقق ════════════

        private static void EnsureCanManageChecklist(AssignedTask task, Viewer viewer)
        {
            if (!(task.CreatedByUserId == viewer.Id || AssignedTaskRules.CanHandle(task, viewer) || viewer.IsSuperAdmin))
                throw new UnauthorizedAccessException("قائمة التحقق للمُسنِد والجهة المنفِّذة فقط");
            EnsureOpen(task, viewer);
        }

        public async Task<AssignedTaskDetailDto> Handle(AddChecklistItemCommand request, CancellationToken ct)
        {
            var viewer = await CurrentAsync();
            var task = await LoadAsync(request.TaskId);
            EnsureCanManageChecklist(task, viewer);

            if (await _tasks.CountChecklistAsync(task.Id) >= AssignedTaskRules.MaxChecklistItems)
                throw new InvalidOperationException($"الحد الأقصى {AssignedTaskRules.MaxChecklistItems} بنداً في القائمة");

            await _tasks.AddChecklistItemAsync(new AssignedTaskChecklistItem
            {
                AssignedTaskId = task.Id,
                Text = request.Text.Trim(),
                CreatedAt = DateTime.UtcNow
            });
            return await DetailAsync(task.Id, viewer);
        }

        public async Task<AssignedTaskDetailDto> Handle(UpdateChecklistItemCommand request, CancellationToken ct)
        {
            var viewer = await CurrentAsync();
            var item = await _tasks.GetChecklistItemAsync(request.ItemId) ?? throw new KeyNotFoundException("البند غير موجود");
            var task = await LoadAsync(item.AssignedTaskId);
            EnsureCanManageChecklist(task, viewer);

            if (request.Text != null)
            {
                // تعديل النص للمُسنِد وحده؛ المنفِّذ يعلّم فقط
                if (!AssignedTaskRules.IsReviewer(task, viewer))
                    throw new UnauthorizedAccessException("يعدّل نص البند من أسند المهمة");
                item.Text = request.Text.Trim();
            }
            if (request.IsDone is bool done && done != item.IsDone)
            {
                item.IsDone = done;
                item.DoneAt = done ? DateTime.UtcNow : null;
            }
            await _tasks.SaveChangesAsync();
            return await DetailAsync(task.Id, viewer);
        }

        public async Task<AssignedTaskDetailDto> Handle(DeleteChecklistItemCommand request, CancellationToken ct)
        {
            var viewer = await CurrentAsync();
            var item = await _tasks.GetChecklistItemAsync(request.ItemId) ?? throw new KeyNotFoundException("البند غير موجود");
            var task = await LoadAsync(item.AssignedTaskId);
            EnsureCanManageChecklist(task, viewer);

            await _tasks.DeleteChecklistItemAsync(item);
            return await DetailAsync(task.Id, viewer);
        }

        // ════════════ «أتولّى هذه المهمة» ════════════

        public async Task<AssignedTaskDetailDto> Handle(ClaimAssignedTaskCommand request, CancellationToken ct)
        {
            var viewer = await CurrentAsync();
            var user = viewer.User;
            var task = await LoadAsync(request.TaskId);
            EnsureOpen(task, viewer);

            var now = DateTime.UtcNow;
            if (request.Claim)
            {
                if (task.TargetType == AssignedTaskTargetType.User)
                    throw new InvalidOperationException("مهمة الموظف له وحده — لا حاجة لتوليها");
                if (!AssignedTaskRules.CanHandle(task, viewer))
                    throw new UnauthorizedAccessException("تتولّى المهمة الجهة المنفِّذة فقط");
                if (task.ClaimedByUserId == user.Id) return await DetailAsync(task.Id, viewer);

                var previous = task.ClaimedByUser?.FullName;
                task.ClaimedByUserId = user.Id;
                task.ClaimedAt = now;
                task.UpdatedAt = now;
                await _tasks.SaveChangesAsync();
                await _tasks.AddActivityAsync(AssignedTaskRules.Activity(task, user, AssignedTaskActivityType.Claimed,
                    previous == null ? "تولّى العمل على المهمة" : $"تولّى العمل على المهمة (بدل {previous})"));
                await AssignedTaskRules.NotifyAsync(_notifications, [task.CreatedByUserId], user.Id, task,
                    NotificationType.TaskStatusChanged, "تولّى موظف مهمة", $"«{task.Title}»: يتولاها {user.FullName}");
            }
            else
            {
                if (task.ClaimedByUserId == null) return await DetailAsync(task.Id, viewer);
                if (task.ClaimedByUserId != user.Id && !AssignedTaskRules.IsReviewer(task, viewer))
                    throw new UnauthorizedAccessException("يتخلّى عن المهمة من تولّاها أو من أسندها");

                task.ClaimedByUserId = null;
                task.ClaimedAt = null;
                task.UpdatedAt = now;
                await _tasks.SaveChangesAsync();
                await _tasks.AddActivityAsync(AssignedTaskRules.Activity(task, user, AssignedTaskActivityType.Released, "أخلى المهمة (لم يعد يتولاها أحد)"));
            }

            return await DetailAsync(task.Id, viewer);
        }
    }
}
