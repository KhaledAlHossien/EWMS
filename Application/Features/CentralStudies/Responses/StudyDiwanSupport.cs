using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using Domain.Entities.CentralStudiesBranch;
using Domain.Enums;

namespace Application.Features.CentralStudies.Responses
{
    /// <summary>الدوال المشتركة بين عمليات هذه الميزة</summary>
    public class StudyDiwanSupport(IStudyResponseService responses, StudyResponseSupport support, StudyKit kit, INotificationPusher pusher)
    {
        public async Task<(StudyAssignmentResponse Response, Viewer Viewer, byte[] Version)> LoadForActionAsync(int id, string permission, string rowVersion)
        {
            var viewer = await kit.ViewerAsync();
            var response = await support.LoadAsync(id, viewer);
            if (!StudyDiwanRules.CanAct(viewer, permission, response.Assignment.Request.BranchId))
                throw new UnauthorizedAccessException("لا تملك صلاحية تنفيذ هذا الإجراء في فرع الدراسة");
            StudyRules.EnsureApproved(response.Status, "إشعار الرد");
            if (response.StartOrder != null) throw new InvalidOperationException("صدر أمر بدء لهذه الدراسة مسبقاً");

            var version = new byte[8];
            if (string.IsNullOrWhiteSpace(rowVersion) || !Convert.TryFromBase64String(rowVersion, version, out var count) || count != version.Length)
                throw new InvalidOperationException("أعد تحميل إشعار الرد قبل تسجيل الإجراء");
            if (!version.SequenceEqual(response.RowVersion))
                throw new InvalidOperationException("تغيّرت المعاملة بعد فتحها — أعد تحميل الصفحة ثم أعد المحاولة");
            return (response, viewer, version);
        }

        public static string ValidateReference(string value, DateOnly date)
        {
            var number = StudyRules.Clean(value);
            if (number == null || number.Length > 50 || date == default)
                throw new InvalidOperationException("رقم الكتاب (حتى 50 حرفاً) وتاريخه مطلوبان");
            return number;
        }

        public async Task<StudyResponseDto> SaveStepAsync(StudyAssignmentResponse response, Viewer viewer, byte[] version,
            StudyLogAction action, string title, string? details, bool notifyNext = false, IStagedFile? file = null)
        {
            var study = response.Assignment.Request;
            var now = DateTime.Now;
            var stage = response.DiwanCase?.Stage;
            var activity = new StudyActivityLog
            {
                RequestId = study.Id,
                UserId = viewer.Id,
                EntityType = StudyEntityType.Response,
                EntityId = response.Id,
                Action = action,
                Title = title,
                Details = details,
                CreatedAt = now
            };
            var notifications = new List<Notification>();
            (IEnumerable<User> Users, string Title)? next = null;
            if (notifyNext && stage == StudyDiwanStage.Rejected)
                next = (await kit.DepartmentHoldersAsync(StudyPermissions.EditResponse, response.Assignment.FollowingDepartmentId),
                    "رفض الديوان المركزي الدراسة — عدّل إشعار الرد وأعد إرساله");
            else if (notifyNext && StudyDiwanRules.NextAction(stage) is { } nextAction)
                next = (await kit.DiwanRecipientsAsync(nextAction.Permission, study.BranchId), nextAction.Title);
            if (next is { } target)
            {
                notifications.AddRange(target.Users.Where(user => user.Id != viewer.Id).Select(user => new Notification
                {
                    UserId = user.Id,
                    Title = target.Title,
                    Message = $"{response.ResponseNumber} — {study.MainBeneficiary}",
                    Type = NotificationType.StudyNextStepReady,
                    RelatedEntityType = "StudyResponse",
                    RelatedEntityId = response.Id,
                    CreatedAt = now
                }));
            }

            // تحديث نسخة إشعار الرد يسلسل انتقالات المعاملة مع الإلغاء وأي تعديل متزامن.
            response.UpdatedAt = now;
            try
            {
                await responses.SaveDiwanStepAsync(response, version, activity, notifications);
            }
            catch
            {
                // لم يُحفظ السجل: يُحذف ملف الكتاب المعتمد كي لا يبقى ملف بلا سجل
                if (file != null) await file.RevertAsync();
                throw;
            }
            if (notifications.Count > 0) await pusher.PushAsync(notifications);
            return await support.DetailAsync(response.Id, viewer);
        }
    }
}
