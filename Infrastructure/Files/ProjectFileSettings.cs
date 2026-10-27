using Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Files
{
    /// <summary>
    /// إعدادات رفع وثائق المشاريع (تُقرأ مرة عند الإقلاع):
    /// Projects:MaxFileGigabytes (الافتراضي 50) سقف الملف في وضع المجلد، وProjects:MaxDatabaseFileMegabytes (100) سقفه في وضع Database،
    /// وProjects:ChunkMegabytes (16، حتى 32) حجم الجزء — وNginx يجب أن يقبل طلباً أكبر منه بقليل —
    /// وProjects:TempFolder مجلد الأجزاء المؤقتة في وضع Database (في وضع المجلد: projects/_uploads داخل مجلد المرفقات).
    /// </summary>
    public sealed class ProjectFileSettings : IProjectFileSettings
    {
        private readonly FileStorageSettings _storage;

        public ProjectFileSettings(IConfiguration configuration, FileStorageSettings storage)
        {
            _storage = storage;
            var gigabytes = Math.Clamp(configuration.GetValue("Projects:MaxFileGigabytes", 50), 1, 1000);
            var dbMegabytes = Math.Clamp(configuration.GetValue("Projects:MaxDatabaseFileMegabytes", 100), 1, 1000);
            var chunkMegabytes = Math.Clamp(configuration.GetValue("Projects:ChunkMegabytes", 16), 1, 32);

            MaxFileBytes = gigabytes * 1024L * 1024L * 1024L;
            MaxDatabaseFileBytes = dbMegabytes * 1024L * 1024L;
            ChunkBytes = chunkMegabytes * 1024L * 1024L;

            var temp = configuration["Projects:TempFolder"]?.Trim();
            TempFolder = _storage.UseFileSystem && _storage.Root is string root
                ? Path.Combine(root, FileAreas.Projects, "_uploads")
                : Path.GetFullPath(string.IsNullOrEmpty(temp) ? Path.Combine(Path.GetTempPath(), "ewms-uploads") : temp);
        }

        public bool UsesFileSystem => _storage.UseFileSystem;
        public long MaxFileBytes { get; }
        public long MaxDatabaseFileBytes { get; }
        public long ChunkBytes { get; }
        public string TempFolder { get; }

        /// <summary>أكبر ملف مقبول في الوضع الحالي</summary>
        public long EffectiveMaxBytes => UsesFileSystem ? MaxFileBytes : Math.Min(MaxFileBytes, MaxDatabaseFileBytes);

        /// <summary>مشكلة تمنع الإقلاع (مجلد الأجزاء لا يُنشأ أو لا يُكتب فيه)، أو null</summary>
        public string? Problem()
        {
            try
            {
                Directory.CreateDirectory(TempFolder);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return $"مجلد الأجزاء المؤقتة لرفع الوثائق لا يُنشأ: {TempFolder} ({(UsesFileSystem ? "داخل FileStorage__Root" : "Projects__TempFolder")})";
            }
            return FileStore.CanWrite(TempFolder) ? null : $"مجلد الأجزاء المؤقتة لرفع الوثائق لا يقبل الكتابة: {TempFolder}";
        }
    }
}
