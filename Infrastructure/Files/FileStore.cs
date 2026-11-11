using System.Text.RegularExpressions;
using Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Files
{
    /// <summary>إعدادات مخزن المرفقات (FileStorage:Mode و FileStorage:Root) — تُقرأ مرة عند الإقلاع</summary>
    public sealed class FileStorageSettings
    {
        public FileStorageSettings(IConfiguration configuration)
        {
            var mode = configuration["FileStorage:Mode"]?.Trim();
            UseFileSystem = string.Equals(mode, "FileSystem", StringComparison.OrdinalIgnoreCase);
            ModeValid = string.IsNullOrEmpty(mode) || UseFileSystem || string.Equals(mode, "Database", StringComparison.OrdinalIgnoreCase);
            var root = configuration["FileStorage:Root"]?.Trim();
            // بلا مجلد مضبوط: مجلد wwwroot/upload داخل مجلد مشروع الـ API (يضبطه Program.cs من مجلد المحتوى) — حلّ مؤقت للتطوير بقرار المستخدم
            if (string.IsNullOrEmpty(root))
            {
                root = configuration["FileStorage:DefaultRoot"]?.Trim();
                IsDefaultRoot = !string.IsNullOrEmpty(root);
            }
            Root = string.IsNullOrEmpty(root) ? null : Path.GetFullPath(root);
        }

        /// <summary>true = المجلد هو الافتراضي (wwwroot/upload داخل مشروع الـ API) لا مجلداً ضُبط صراحةً</summary>
        public bool IsDefaultRoot { get; }

        /// <summary>true = المرفقات الجديدة تُحفظ في المجلد؛ false = في القاعدة (الافتراضي)</summary>
        public bool UseFileSystem { get; }
        public bool ModeValid { get; }
        /// <summary>مسار المجلد (قد يُضبط في وضع Database أيضاً لقراءة ملفات حُفظت فيه قبل الرجوع إلى القاعدة)</summary>
        public string? Root { get; }

        /// <summary>مشكلة تمنع الإقلاع (للرسالة العربية في StartupSettings)، أو null</summary>
        public string? Problem()
        {
            // المجلد الافتراضي يُنشأ عند الإقلاع (فحص /health يكتب فيه ملف اختبار). تعذّر إنشائه (مجلد التطبيق للقراءة فقط مثلاً)
            // لا يوقف الإقلاع في وضع Database: بقية النظام لا تحتاجه، فيظهر /health «Degraded» ويفشل رفع مرفقات الدراسات برسالة واضحة.
            if (IsDefaultRoot && Root != null)
            {
                try { Directory.CreateDirectory(Root); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
                {
                    if (UseFileSystem) return $"تعذّر إنشاء مجلد المرفقات الافتراضي: {Root} — اضبط FileStorage__Root على مجلد يملك مستخدم الخدمة الكتابة فيه";
                }
            }
            if (!ModeValid) return "طريقة حفظ المرفقات (FileStorage__Mode): Database أو FileSystem";
            if (!UseFileSystem) return null;
            if (Root == null) return "مجلد المرفقات (FileStorage__Root): مطلوب عند FileStorage__Mode=FileSystem";
            if (!Directory.Exists(Root)) return $"مجلد المرفقات (FileStorage__Root) غير موجود: {Root} — هل رُكّبت المشاركة؟";
            if (!FileStore.CanWrite(Root)) return $"مجلد المرفقات (FileStorage__Root) لا يقبل الكتابة: {Root} — تحقق من صلاحيات مستخدم الخدمة على المشاركة";
            return null;
        }
    }

    /// <summary>
    /// المرفقات على القرص (عادة مشاركة SMB مركّبة على خادم التطبيق). المفتاح «مجلد/سنة/شهر/معرّف عشوائي» بلا اسم الملف الأصلي
    /// (الاسم في القاعدة فقط)، والملف لا يُعدَّل بعد كتابته أبداً. الكتابة إلى ملف مؤقت ثم إعادة تسمية، فلا يُقرأ ملف ناقص.
    /// </summary>
    public sealed partial class FileStore : IFileStore
    {
        internal const string TempSuffix = ".tmp";
        private readonly FileStorageSettings _settings;

        public FileStore(FileStorageSettings settings) => _settings = settings;

        // «منطقة/سنة/شهر/معرّف» لكل المناطق، ولمرفقات الدراسات أيضاً «studies/projects|documents/رقم/معرّف» (مجلد لكل مشروع، ومجلد لكل دراسة قبل مشروعها)
        [GeneratedRegex("^(?:(?:vacations|tasks|map-files|studies)/[0-9]{4}/[0-9]{2}|studies/(?:projects|documents)/[0-9]{1,10})/[0-9a-f]{32}\\z")]
        private static partial Regex KeyPattern();

        public static bool IsValidKey(string key) => KeyPattern().IsMatch(key);

        public async Task<string?> SaveAsync(string area, byte[] data, CancellationToken ct = default)
        {
            if (!_settings.UseFileSystem) return null;
            if (!FileAreas.All.Contains(area)) throw new ArgumentException("مجلد مرفقات غير معروف", nameof(area));

            var now = DateTime.UtcNow;
            var key = $"{area}/{now:yyyy}/{now:MM}/{Guid.NewGuid():N}";
            var path = PathOf(key);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            var temp = path + TempSuffix;
            try
            {
                await using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
                {
                    await stream.WriteAsync(data, ct);
                    await stream.FlushAsync(ct);
                    stream.Flush(flushToDisk: true);
                }
                File.Move(temp, path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                TryDelete(temp);
                throw new InvalidOperationException("تعذّر حفظ الملف في مخزن المرفقات — أعد المحاولة، وإن تكرر فأبلغ مدير النظام", ex);
            }
            return key;
        }

        public async Task<IStagedFile> StageAsync(string area, string? folder, Func<Stream, CancellationToken, Task> write, CancellationToken ct = default)
        {
            if (!FileAreas.All.Contains(area)) throw new ArgumentException("مجلد مرفقات غير معروف", nameof(area));
            if (_settings.Root == null)
                throw new InvalidOperationException("الملفات الكبيرة تُحفظ في مجلد على الخادم ولم يُضبط بعد (FileStorage:Root) — أبلغ مدير النظام");

            var now = DateTime.UtcNow;
            var key = string.IsNullOrEmpty(folder)
                ? $"{area}/{now:yyyy}/{now:MM}/{Guid.NewGuid():N}"
                : $"{area}/{folder}/{Guid.NewGuid():N}";
            if (!IsValidKey(key)) throw new ArgumentException("مجلد المرفق غير صالح", nameof(folder));   // الشكل المسموح فقط: لا يخرج المسار عن المجلد
            var path = PathOf(key);
            var temp = path + TempSuffix;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1 << 20, FileOptions.Asynchronous))
                {
                    await write(stream, ct);
                    await stream.FlushAsync(ct);
                    stream.Flush(flushToDisk: true);
                }
                return new StagedFile(key, path, temp, new FileInfo(temp).Length);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                TryDelete(temp);
                throw new InvalidOperationException("تعذّر حفظ الملف في مجلد المرفقات على الخادم — أعد المحاولة، وإن تكرر فأبلغ مدير النظام", ex);
            }
            catch
            {
                TryDelete(temp);   // انقطع الرفع أو رُفض أثناء الكتابة
                throw;
            }
        }

        private sealed class StagedFile : IStagedFile
        {
            private readonly string _path, _temp;
            private bool _committed;

            public StagedFile(string key, string path, string temp, long size)
            {
                Key = key;
                _path = path;
                _temp = temp;
                Size = size;
            }

            public string Key { get; }
            public long Size { get; }

            public Stream OpenRead() =>
                new FileStream(_temp, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous);

            public Task CommitAsync()
            {
                File.Move(_temp, _path);
                _committed = true;
                return Task.CompletedTask;
            }

            public ValueTask DisposeAsync()
            {
                if (!_committed) TryDelete(_temp);
                return ValueTask.CompletedTask;
            }
        }

        public Stream OpenRead(string key)
        {
            var path = PathOf(key);
            try
            {
                return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
            {
                throw new KeyNotFoundException("محتوى الملف غير موجود في مخزن المرفقات");
            }
            catch (IOException ex)
            {
                throw new InvalidOperationException("تعذّر الوصول إلى مخزن المرفقات — أبلغ مدير النظام", ex);
            }
        }

        /// <summary>المسار الكامل لمفتاح صالح داخل المجلد (المفتاح من القاعدة، ويُفحص شكله كي لا يخرج عن المجلد)</summary>
        internal string PathOf(string key)
        {
            var root = _settings.Root ?? throw new InvalidOperationException("مخزن المرفقات غير مُعدّ على الخادم (FileStorage:Root) — أبلغ مدير النظام");
            if (!IsValidKey(key)) throw new KeyNotFoundException("محتوى الملف غير موجود في مخزن المرفقات");
            return Path.Combine(root, key.Replace('/', Path.DirectorySeparatorChar));
        }

        /// <summary>يكتب ملف اختبار صغيراً ثم يحذفه (فحص الإقلاع و /health)</summary>
        public static bool CanWrite(string root)
        {
            var probe = Path.Combine(root, $".probe-{Guid.NewGuid():N}{TempSuffix}");
            try
            {
                File.WriteAllBytes(probe, [1]);
                File.Delete(probe);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                TryDelete(probe);
                return false;
            }
        }

        internal static void TryDelete(string path)
        {
            try { File.Delete(path); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }
}
