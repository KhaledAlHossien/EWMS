using Application.DTOs.Request;

namespace Application.Features.AssignedTasks
{
    /// <summary>
    /// قواعد مرفقات المهام (قرار المستخدم 2026-10-07): PDF وصور JPG/PNG ومستندات Office (Word وExcel وPowerPoint)،
    /// حتى 10 ملفات × 10MB لكل مهمة. النوع يُعرف من محتوى الملف لا من الامتداد ولا من المتصفح، وتُمنع الملفات التنفيذية
    /// والأرشيفات العامة والمستندات الحاوية على ماكرو (vbaProject).
    /// </summary>
    public static class TaskAttachmentRules
    {
        public const int MaxFiles = 10;
        public const int MaxBytes = 10 * 1024 * 1024;
        public const int MaxFileNameLength = 200;

        public const string Pdf = "application/pdf";
        public const string Png = "image/png";
        public const string Jpeg = "image/jpeg";
        public const string Docx = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
        public const string Xlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        public const string Pptx = "application/vnd.openxmlformats-officedocument.presentationml.presentation";
        public const string Doc = "application/msword";
        public const string Xls = "application/vnd.ms-excel";
        public const string Ppt = "application/vnd.ms-powerpoint";

        /// <summary>تُعرض داخل الصفحة (معاينة)؛ غيرها يُنزَّل فقط</summary>
        public static bool IsPreviewable(string contentType) => contentType is Pdf or Png or Jpeg;

        private static bool Contains(byte[] data, ReadOnlySpan<byte> needle) => data.AsSpan().IndexOf(needle) >= 0;

        /// <summary>نوع الملف من بصمته، أو null إن لم يكن من الأنواع المسموحة</summary>
        public static string? DetectContentType(byte[] data, string? fileName)
        {
            if (data.Length >= 5 && data[0] == 0x25 && data[1] == 0x50 && data[2] == 0x44 && data[3] == 0x46 && data[4] == 0x2D)
                return Pdf;                                                   // %PDF-
            if (data.Length >= 8 && data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47
                && data[4] == 0x0D && data[5] == 0x0A && data[6] == 0x1A && data[7] == 0x0A)
                return Png;
            if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
                return Jpeg;

            // docx / xlsx / pptx: ملف مضغوط (ZIP) بمحتويات Office المعروفة — لا أي ZIP، ولا ما فيه ماكرو
            if (data.Length >= 4 && data[0] == 0x50 && data[1] == 0x4B && data[2] == 0x03 && data[3] == 0x04
                && Contains(data, "[Content_Types].xml"u8) && !Contains(data, "vbaProject.bin"u8))
            {
                if (Contains(data, "word/"u8)) return Docx;
                if (Contains(data, "xl/"u8)) return Xlsx;
                if (Contains(data, "ppt/"u8)) return Pptx;
                return null;
            }

            // doc / xls / ppt القديمة: حاوية OLE — لا تُميَّز أنواعها من البصمة، فنشترط امتداداً مطابقاً معها
            if (data.Length >= 8 && data[0] == 0xD0 && data[1] == 0xCF && data[2] == 0x11 && data[3] == 0xE0
                && data[4] == 0xA1 && data[5] == 0xB1 && data[6] == 0x1A && data[7] == 0xE1)
                return Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant() switch
                {
                    ".doc" => Doc,
                    ".xls" => Xls,
                    ".ppt" => Ppt,
                    _ => null
                };

            return null;
        }

        /// <summary>اسم الملف بلا مسار، ومقصوص إلى الطول المسموح مع الإبقاء على الامتداد</summary>
        public static string SafeFileName(string? name)
        {
            var file = Path.GetFileName((name ?? string.Empty).Replace('\\', '/').Split('/').Last()).Trim();
            if (string.IsNullOrEmpty(file)) file = "مرفق";
            if (file.Length <= MaxFileNameLength) return file;
            var ext = Path.GetExtension(file);
            return file[..(MaxFileNameLength - ext.Length)] + ext;
        }

        /// <summary>رسالة الخطأ لملف غير مقبول، أو null إن كان سليماً</summary>
        public static string? Problem(UploadedFileDto file)
        {
            if (file.Data.Length == 0) return $"الملف «{file.FileName}» فارغ";
            if (file.Data.Length > MaxBytes) return $"الملف «{file.FileName}» أكبر من 10MB";
            if (DetectContentType(file.Data, file.FileName) == null)
                return $"الملف «{file.FileName}» نوعه غير مسموح — المسموح: PDF وصور JPG/PNG ومستندات Word وExcel وPowerPoint (بلا ماكرو)";
            return null;
        }
    }
}
