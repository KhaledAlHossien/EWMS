using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Enums;
using MediatR;

namespace Application.Features.AssignedTasks.Commands.DeleteAttachment
{
    public class DeleteTaskAttachmentCommandHandler : IRequestHandler<DeleteTaskAttachmentCommand, AssignedTaskDetailDto>
    {
        private readonly IAssignedTaskService _tasks;
        private readonly IUserService _users;
        private readonly IUserPermissionService _permissions;
        private readonly AssignedTaskExtrasSupport _support;

        public DeleteTaskAttachmentCommandHandler(IAssignedTaskService tasks, IUserService users, IUserPermissionService permissions, AssignedTaskExtrasSupport support)
        {
            _tasks = tasks;
            _users = users;
            _permissions = permissions;
            _support = support;
        }

        public async Task<AssignedTaskDetailDto> Handle(DeleteTaskAttachmentCommand request, CancellationToken ct)
        {
            var attachment = await _tasks.GetAttachmentAsync(request.AttachmentId)
                ?? throw new KeyNotFoundException("المرفق غير موجود");
            var viewer = await CurrentAsync();
            var task = await _support.LoadAsync(attachment.AssignedTaskId);

            if (attachment.UploadedByUserId != viewer.Id && !AssignedTaskRules.IsReviewer(task, viewer))
                throw new UnauthorizedAccessException("يحذف المرفق من رفعه أو من أسند المهمة");
            AssignedTaskExtrasSupport.EnsureOpen(task, viewer);

            var name = attachment.FileName;
            await _tasks.DeleteAttachmentAsync(attachment);
            await _tasks.AddActivityAsync(AssignedTaskRules.Activity(task, viewer.User, AssignedTaskActivityType.AttachmentRemoved, $"حذف المرفق «{name}»"));
            return await _support.DetailAsync(task.Id, viewer);
        }

        private Task<Viewer> CurrentAsync() => Viewer.CurrentAsync(_users, _permissions);
    }
}
