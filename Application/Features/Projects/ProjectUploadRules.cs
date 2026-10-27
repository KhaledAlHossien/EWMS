using Application.Features.MapLayers;
using Domain.Entities;

namespace Application.Features.Projects
{
    /// <summary>
    /// رفع وثائق المشاريع بأجزاء (2026-10-27، المستخدم: ملفات حتى 10GB فأكثر): قواعد نقية قابلة للاختبار دون قرص أو قاعدة.
    /// الجلسة محفوظة في القاعدة (ما استُلم فعلاً)، فالانقطاع لا يضيّع شيئاً والاستئناف يبدأ من حيث وقف.
    /// </summary>
    public static class ProjectUploadRules
    {
        /// <summary>جلسات الرفع المفتوحة (أجزاء تصل أو قيد الإنهاء) لكل مستخدم</summary>
        public const int MaxOpenPerUser = 3;
        public const int MaxFileNameLength = 260;

        /// <summary>رفع لم يكتمل ولم يُمسّ منذ هذه المدة يُمسح مع ملفه المؤقت</summary>
        public static readonly TimeSpan AbandonedAfter = TimeSpan.FromHours(48);
        /// <summary>جلسات انتهت (نجحت أو فشلت) تبقى مدة ليراها المستخدم، ثم تُمسح</summary>
        public static readonly TimeSpan FinishedKeptFor = TimeSpan.FromHours(24);
        /// <summary>إنهاء تجاوز هذه المدة يُعدّ فاشلاً (توقفت الخدمة في منتصفه ولم تستأنفه)</summary>
        public static readonly TimeSpan ProcessingStuckAfter = TimeSpan.FromHours(6);

        public static string StatusAr(ProjectUploadStatus status) => status switch
        {
            ProjectUploadStatus.Receiving => "جارٍ الرفع",
            ProjectUploadStatus.Processing => "جارٍ فحص الملف وحفظه",
            ProjectUploadStatus.Done => "اكتمل",
            _ => "فشل"
        };

        /// <summary>مشكلة بدء الرفع (للرسالة العربية)، أو null</summary>
        public static string? ValidateStart(string? fileName, long size, long maxBytes, bool usesFileSystem)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return "اسم الملف مطلوب";
            if (fileName.Length > MaxFileNameLength) return $"اسم الملف يتجاوز {MaxFileNameLength} حرفاً";
            if (size <= 0) return "الملف فارغ";
            if (size > maxBytes)
                return usesFileSystem
                    ? $"الملف أكبر من سقف النظام ({Describe(maxBytes)})"
                    : $"الملف أكبر من {Describe(maxBytes)}: الملفات الكبيرة تتطلب تفعيل مجلد المرفقات على الخادم (FileStorage__Mode=FileSystem) — أبلغ مدير النظام";
            return null;
        }

        public static string Describe(long bytes) => bytes switch
        {
            >= 1024L * 1024 * 1024 => $"{bytes / (1024.0 * 1024 * 1024):0.#}GB",
            >= 1024L * 1024 => $"{bytes / (1024.0 * 1024):0.#}MB",
            _ => $"{bytes / 1024.0:0.#}KB"
        };

        public enum ChunkDecision
        {
            /// <summary>الجزء يلي ما استُلم تماماً: يُكتب</summary>
            Write,
            /// <summary>الجزء وصل قبلاً (إعادة بعد ضياع الرد): لا يُكتب، ويُردّ ما استُلم</summary>
            AlreadyReceived,
            /// <summary>خارج الترتيب: يُرفض ويُخبَر العميل بما استُلم ليستأنف منه</summary>
            OutOfOrder,
            /// <summary>طول الجزء صفر أو أكبر من المسموح أو يتجاوز حجم الملف المعلن</summary>
            BadLength
        }

        public static ChunkDecision DecideChunk(long received, long size, long chunkMax, long offset, long length)
        {
            if (length <= 0 || length > chunkMax || offset < 0 || offset + length > size) return ChunkDecision.BadLength;
            if (offset == received) return ChunkDecision.Write;
            if (offset < received && offset + length <= received) return ChunkDecision.AlreadyReceived;
            return ChunkDecision.OutOfOrder;
        }

        /// <summary>الإنهاء يحتاج أن يكتمل الملف كله</summary>
        public static string? CheckComplete(ProjectUpload upload) =>
            upload.Status != ProjectUploadStatus.Receiving
                ? "هذا الرفع أُنهي أو أُلغي"
                : upload.Received != upload.Size
                    ? $"لم يكتمل الرفع: وصل {Describe(upload.Received)} من {Describe(upload.Size)}"
                    : null;

        /// <summary>رفض الملف بعد كشف نوعه (يُستعمل نفس كاشف ملفات الخرائط: الممنوع ممنوع في كل مكان)، أو null مع النوع</summary>
        public static (string? Problem, MapFileRules.Detected? Detected) CheckType(string fileName, Stream content)
        {
            var detected = MapFileRules.Detect(content, fileName);
            return detected == null
                ? ($"الملف «{fileName}» نوعه غير مدعوم أو ممنوع — البرامج والسكربتات وصفحات HTML/SVG والمستندات ذات الماكرو لا تُقبل", null)
                : (null, detected);
        }
    }
}
