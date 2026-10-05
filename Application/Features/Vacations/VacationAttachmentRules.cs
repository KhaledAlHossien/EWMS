using Application.DTOs.Request;

namespace Application.Features.Vacations
{
    /// <summary>
    /// قواعد مرفقات الإجازة (قرار المستخدم 2026-10-05) — مصدر واحد للمدقّق والمعالج:
    /// PDF أو JPG أو PNG فقط، يُعرف النوع من بداية محتوى الملف (لا من الامتداد ولا من المتصفح)، حتى 3 ملفات × 5MB.
    /// </summary>
    public static class VacationAttachmentRules
    {
        public const int MaxFiles = 3;
        public const int MaxBytes = 5 * 1024 * 1024;
        public const int MaxFileNameLength = 200;

        /// <summary>نوع الملف من بصمته في أول البايتات، أو null إن لم يكن من الأنواع المسموحة</summary>
        public static string? DetectContentType(byte[] data)
        {
            if (data.Length >= 5 && data[0] == 0x25 && data[1] == 0x50 && data[2] == 0x44 && data[3] == 0x46 && data[4] == 0x2D)
                return "application/pdf";                                   // %PDF-
            if (data.Length >= 8 && data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47
                && data[4] == 0x0D && data[5] == 0x0A && data[6] == 0x1A && data[7] == 0x0A)
                return "image/png";
            if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
                return "image/jpeg";
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
            if (file.Data.Length > MaxBytes) return $"الملف «{file.FileName}» أكبر من 5MB";
            if (DetectContentType(file.Data) == null) return $"الملف «{file.FileName}» ليس PDF أو صورة JPG/PNG";
            return null;
        }
    }
}
