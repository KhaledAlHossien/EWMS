using Application.Interfaces;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Files
{
    /// <summary>
    /// يحذف من مجلد المرفقات ما لا يشير إليه أي سجل: سجلات حُذفت بالحذف المتسلسل (مهمة، سجل طبقة، حقل، ملف معلّق انتهت مهلته)
    /// أو ملفات كُتبت ثم فشل حفظ سجلها. لا يُحذف ملف عمره أقل من 24 ساعة (قد يكون سجله قيد الحفظ)، ويعمل مرة كل 6 ساعات على الأكثر.
    /// </summary>
    public sealed class FileStoreSweeper : IFileStoreSweeper
    {
        private static readonly TimeSpan Every = TimeSpan.FromHours(6);
        private static readonly TimeSpan Grace = TimeSpan.FromHours(24);
        private const int Batch = 500;
        private static DateTime _lastRunUtc = DateTime.MinValue;

        private readonly DataContext _context;
        private readonly FileStorageSettings _settings;

        public FileStoreSweeper(DataContext context, FileStorageSettings settings)
        {
            _context = context;
            _settings = settings;
        }

        public async Task<int> SweepAsync(CancellationToken ct = default)
        {
            if (!_settings.UseFileSystem || _settings.Root is not string root) return 0;
            var now = DateTime.UtcNow;
            if (now - _lastRunUtc < Every) return 0;
            _lastRunUtc = now;

            var cutoff = now - Grace;
            var removed = 0;
            foreach (var area in FileAreas.All)
            {
                var dir = Path.Combine(root, area);
                if (!Directory.Exists(dir)) continue;

                var candidates = new Dictionary<string, string>();   // المفتاح ← المسار
                foreach (var path in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                {
                    ct.ThrowIfCancellationRequested();
                    if (File.GetLastWriteTimeUtc(path) >= cutoff) continue;
                    // ملف مؤقت بقي من كتابة انقطعت
                    if (path.EndsWith(FileStore.TempSuffix, StringComparison.Ordinal)) { FileStore.TryDelete(path); removed++; continue; }

                    var key = Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/');
                    if (!FileStore.IsValidKey(key)) continue;   // ليس من ملفاتنا: لا يُمسّ
                    candidates[key] = path;
                    if (candidates.Count >= Batch) { removed += await DeleteUnreferencedAsync(candidates, ct); candidates.Clear(); }
                }
                if (candidates.Count > 0) removed += await DeleteUnreferencedAsync(candidates, ct);
            }
            return removed;
        }

        private async Task<int> DeleteUnreferencedAsync(Dictionary<string, string> candidates, CancellationToken ct)
        {
            var keys = candidates.Keys.ToList();
            var used = new HashSet<string>(StringComparer.Ordinal);
            used.UnionWith(await _context.VacationAttachments.Where(a => a.StorageKey != null && keys.Contains(a.StorageKey)).Select(a => a.StorageKey!).ToListAsync(ct));
            used.UnionWith(await _context.AssignedTaskAttachments.Where(a => a.StorageKey != null && keys.Contains(a.StorageKey)).Select(a => a.StorageKey!).ToListAsync(ct));
            used.UnionWith(await _context.MapEntryFiles.Where(f => f.StorageKey != null && keys.Contains(f.StorageKey)).Select(f => f.StorageKey!).ToListAsync(ct));

            var removed = 0;
            foreach (var (key, path) in candidates)
            {
                if (used.Contains(key)) continue;
                FileStore.TryDelete(path);
                removed++;
            }
            return removed;
        }
    }
}
