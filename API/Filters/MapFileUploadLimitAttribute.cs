using Application.Interfaces;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace API.Filters
{
    /// <summary>
    /// حد حجم طلب رفع ملفات حقول الطبقات من إعدادات الخادم (MapLayers:MaxFileMegabytes) بدل رقم ثابت:
    /// يُضبط قبل قراءة النموذج، ويُرفض الطلب الأكبر منه مباشرة برسالة عربية (413).
    /// </summary>
    public class MapFileUploadLimitAttribute : TypeFilterAttribute
    {
        public MapFileUploadLimitAttribute() : base(typeof(MapFileUploadLimitFilter)) { }
    }

    public class MapFileUploadLimitFilter : IAsyncResourceFilter
    {
        /// <summary>هامش لحدود النموذج وبياناته فوق حجم الملف</summary>
        private const long Overhead = 1024 * 1024;
        private readonly IMapLayerSettings _settings;

        public MapFileUploadLimitFilter(IMapLayerSettings settings) => _settings = settings;

        public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
        {
            var limit = _settings.MaxFileMegabytes * 1024L * 1024L + Overhead;
            var http = context.HttpContext;
            if (http.Request.ContentLength > limit)
            {
                context.Result = new ObjectResult(new { statusCode = StatusCodes.Status413PayloadTooLarge, message = $"الملف أكبر من سقف النظام {_settings.MaxFileMegabytes}MB" })
                { StatusCode = StatusCodes.Status413PayloadTooLarge };
                return;
            }

            var bodySize = http.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (bodySize is { IsReadOnly: false }) bodySize.MaxRequestBodySize = limit;
            // حد النموذج أكبر قليلاً من حد الطلب، فيكون حد الطلب (413) هو الذي يُطبَّق
            if (http.Features.Get<IFormFeature>() is null)
                http.Features.Set<IFormFeature>(new FormFeature(http.Request, new FormOptions { MultipartBodyLengthLimit = limit + Overhead }));
            await next();
        }
    }
}
