using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;

namespace Application.Features.AssignedTasks.Queries.GetAttachment
{
    public class GetTaskAttachmentQueryHandler : IRequestHandler<GetTaskAttachmentQuery, TaskAttachmentFileDto>
    {
        private readonly IAssignedTaskService _tasks;
        private readonly IUserService _users;
        private readonly IUserPermissionService _permissions;
        private readonly IFileStore _files;

        public GetTaskAttachmentQueryHandler(IAssignedTaskService tasks, IUserService users, IUserPermissionService permissions, IFileStore files)
        {
            _tasks = tasks;
            _users = users;
            _permissions = permissions;
            _files = files;
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

            var content = await StoredFiles.OpenAsync(_files, attachment.StorageKey, () => _tasks.GetAttachmentDataAsync(attachment.Id));
            return new TaskAttachmentFileDto(attachment.FileName, attachment.ContentType, content,
                TaskAttachmentRules.IsPreviewable(attachment.ContentType));
        }

        private Task<Viewer> CurrentAsync() => Viewer.CurrentAsync(_users, _permissions);
    }
}
