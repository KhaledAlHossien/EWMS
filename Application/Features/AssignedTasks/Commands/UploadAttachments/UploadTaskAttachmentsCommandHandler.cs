using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using MediatR;

namespace Application.Features.AssignedTasks.Commands.UploadAttachments
{
    public class UploadTaskAttachmentsCommandHandler : IRequestHandler<UploadTaskAttachmentsCommand, AssignedTaskDetailDto>
    {
        private readonly IAssignedTaskService _tasks;
        private readonly IUserService _users;
        private readonly IUserPermissionService _permissions;
        private readonly INotificationService _notifications;
        private readonly IFileStore _files;
        private readonly AssignedTaskExtrasSupport _support;

        public UploadTaskAttachmentsCommandHandler(IAssignedTaskService tasks, IUserService users, IUserPermissionService permissions, INotificationService notifications, IFileStore files, AssignedTaskExtrasSupport support)
        {
            _tasks = tasks;
            _users = users;
            _permissions = permissions;
            _notifications = notifications;
            _files = files;
            _support = support;
        }

        public async Task<AssignedTaskDetailDto> Handle(UploadTaskAttachmentsCommand request, CancellationToken ct)
        {
            var viewer = await CurrentAsync();
            var task = await _support.LoadAsync(request.TaskId);
            if (!AssignedTaskRules.CanView(task, viewer))
                throw new UnauthorizedAccessException("لا تملك صلاحية الإرفاق إلى هذه المهمة");
            AssignedTaskExtrasSupport.EnsureOpen(task, viewer);

            if (request.Files.Count == 0) throw new InvalidOperationException("اختر ملفاً للإرفاق");
            var existing = await _tasks.CountAttachmentsAsync(task.Id);
            if (existing + request.Files.Count > TaskAttachmentRules.MaxFiles)
                throw new InvalidOperationException($"الحد الأقصى {TaskAttachmentRules.MaxFiles} ملفات للمهمة (مرفق الآن {existing})");

            var now = DateTime.UtcNow;
            var created = new List<AssignedTaskAttachment>();
            foreach (var file in request.Files)
            {
                if (TaskAttachmentRules.Problem(file) is string problem) throw new InvalidOperationException(problem);
            }
            foreach (var file in request.Files)
            {
                // في مجلد المرفقات أولاً إن كان مُفعَّلاً، وإلا في القاعدة
                var key = await _files.SaveAsync(FileAreas.Tasks, file.Data, ct);
                created.Add(new AssignedTaskAttachment
                {
                    AssignedTaskId = task.Id,
                    FileName = TaskAttachmentRules.SafeFileName(file.FileName),
                    ContentType = TaskAttachmentRules.DetectContentType(file.Data, file.FileName)!,
                    Size = file.Data.Length,
                    UploadedByUserId = viewer.Id,
                    UploadedAt = now,
                    StorageKey = key,
                    Content = key == null ? new AssignedTaskAttachmentContent { Data = file.Data } : null
                });
            }
            await _tasks.AddAttachmentsAsync(created);

            var names = string.Join("، ", created.Select(a => $"«{a.FileName}»"));
            await _tasks.AddActivityAsync(AssignedTaskRules.Activity(task, viewer.User, AssignedTaskActivityType.Attached, $"أرفق {names}"));

            // المُسنِد والمنفِّذون (عدا الرافع)
            var recipients = (await AssignedTaskRules.HandlerUserIdsAsync(_permissions, task)).Append(task.CreatedByUserId);
            await AssignedTaskRules.NotifyAsync(_notifications, recipients, viewer.Id, task, NotificationType.TaskAttachmentAdded,
                "ملف جديد على مهمة", $"أرفق {viewer.User.FullName} {names} بالمهمة «{task.Title}»");

            return await _support.DetailAsync(task.Id, viewer);
        }

        private Task<Viewer> CurrentAsync() => Viewer.CurrentAsync(_users, _permissions);
    }
}
