using Application.Common;
using Domain.Entities;

namespace Application.Features.Projects
{
    /// <summary>
    /// وثائق إضبارة المشروع (القواعد 7 و9 من تحليل الجنوبية): الإصدار الجديد يُضاف ولا يستبدل شيئاً، والاعتماد على غير من رفع،
    /// والإصدار المعتمد لا يُعدَّل ولا يُحذف، وتنزيله يُسجَّل. الوصول إلى أي وثيقة يمرّ من حدّ رؤية مشروعها (ProjectRules.Visible).
    /// </summary>
    public static class ProjectDocumentRules
    {
        /// <summary>آخر إصدار (أعلى رقم)، أو null لوثيقة بلا إصدارات</summary>
        public static ProjectDocumentVersion? Latest(ProjectDocument document) =>
            document.Versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault();

        /// <summary>أحدث إصدار معتمد، أو null إن لم يُعتمد شيء</summary>
        public static ProjectDocumentVersion? Approved(ProjectDocument document) =>
            document.Versions.Where(v => v.IsApproved).OrderByDescending(v => v.VersionNumber).FirstOrDefault();

        /// <summary>
        /// الاعتماد صلاحية مستقلة، ومن رفع الإصدار لا يعتمده (فصل الإدخال عن الاعتماد) — حتى لمدير النظام،
        /// فالفصل غرضه أن يراجع شخص ثانٍ ما رُفع. الإصدار المعتمد مرة لا يُعتمد ثانية.
        /// </summary>
        public static void EnsureCanApprove(Viewer viewer, ProjectDocumentVersion version)
        {
            if (version.IsApproved)
                throw new InvalidOperationException("هذا الإصدار معتمد مسبقاً");
            if (version.UploadedByUserId == viewer.Id)
                throw new InvalidOperationException("من رفع الإصدار لا يعتمده — يعتمده موظف آخر يملك صلاحية الاعتماد");
        }

        /// <summary>الوثيقة التي فيها إصدار معتمد تبقى بإصداراتها (للمراجعة)؛ غيرها يجوز حذفها بصلاحية الحذف</summary>
        public static void EnsureCanDelete(ProjectDocument document)
        {
            if (document.Versions.Any(v => v.IsApproved))
                throw new InvalidOperationException("لا تُحذف وثيقة فيها إصدار معتمد — تبقى في الإضبارة للمراجعة، وأضف إصداراً جديداً لتصحيحها");
        }

        /// <summary>
        /// يُسجَّل في التدقيق فتح الإصدار المعتمد وتنزيله، مرة لكل فتح (أول طلب)، لا لكل طلب نطاق (Range)
        /// الذي يرسله المتصفح أثناء التنقل في PDF كبير. الطلب الأول: بلا ترويسة Range أو يبدأ من البايت 0.
        /// </summary>
        public static bool ShouldAuditDownload(ProjectDocumentVersion version, bool firstRequest) => version.IsApproved && firstRequest;

        /// <summary>بداية النطاق المطلوب من ترويسة Range («bytes=0-99»)، أو 0 بلا ترويسة؛ غير المفهوم يُعدّ طلباً أول (الأحوط تسجيلاً)</summary>
        public static bool IsFirstRequest(string? rangeHeader)
        {
            if (string.IsNullOrWhiteSpace(rangeHeader)) return true;
            var value = rangeHeader.Trim();
            if (!value.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase)) return true;
            var first = value[6..].Split(',')[0].Split('-')[0].Trim();
            return first.Length == 0 || !long.TryParse(first, out var start) || start == 0;
        }
    }
}
