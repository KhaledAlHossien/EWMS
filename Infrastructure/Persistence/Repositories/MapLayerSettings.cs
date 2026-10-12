using Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Persistence.Repositories
{
    /// <summary>إعدادات طبقات الخرائط من إعدادات الخادم (تُقرأ مرة عند الإقلاع)</summary>
    public class MapLayerSettings : IMapLayerSettings
    {
        public MapLayerSettings(IConfiguration configuration)
        {
            // سقف حجم الملف الواحد لكل حقول الملفات — يجب ألا يقلّ client_max_body_size في Nginx عنه
            MaxFileMegabytes = Math.Clamp(configuration.GetValue("MapLayers:MaxFileMegabytes", 100), 1, 2048);
        }

        public int MaxFileMegabytes { get; }
    }
}
