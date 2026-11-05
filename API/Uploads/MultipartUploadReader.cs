using Application.Interfaces;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;

namespace API.Uploads
{
    /// <summary>
    /// يقرأ أول ملف من جسم طلب multipart/form-data تدفقاً ويكتبه في الوجهة مباشرة — لا يُحمَّل في الذاكرة
    /// ولا يُخزَّن مؤقتاً في مجلد ASP.NET (الملفات الكبيرة: مخططات فرع الدراسات قد تبلغ جيجابايت).
    /// الـ action لا يربط أي حقل من النموذج ([FromForm]/IFormFile) كي لا يقرأ ASP.NET الجسم قبله.
    /// </summary>
    public sealed class MultipartUploadReader : IUploadReader
    {
        private readonly HttpRequest _request;

        public MultipartUploadReader(HttpRequest request) => _request = request;

        public async Task<string?> CopyFileToAsync(Stream destination, CancellationToken ct)
        {
            if (!MediaTypeHeaderValue.TryParse(_request.ContentType, out var mediaType)
                || !mediaType.MediaType.Equals("multipart/form-data", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("أرسل الملف بنموذج multipart/form-data");
            var boundary = HeaderUtilities.RemoveQuotes(mediaType.Boundary).Value;
            if (string.IsNullOrWhiteSpace(boundary)) throw new InvalidOperationException("نموذج الرفع غير صالح");

            // BodyLengthLimit = null: لا سقف لحجم الملف
            var reader = new MultipartReader(boundary, _request.Body) { BodyLengthLimit = null };
            for (var section = await reader.ReadNextSectionAsync(ct); section != null; section = await reader.ReadNextSectionAsync(ct))
            {
                if (!ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var disposition)
                    || !disposition.IsFileDisposition())
                    continue;   // حقول نصية: تُتجاوز (معرّفات المالك تأتي في عنوان الطلب)

                var name = disposition.FileNameStar.HasValue ? disposition.FileNameStar.Value : disposition.FileName.Value;
                await section.Body.CopyToAsync(destination, 1 << 20, ct);
                return HeaderUtilities.RemoveQuotes(name).Value ?? "ملف";
            }
            return null;
        }
    }
}
