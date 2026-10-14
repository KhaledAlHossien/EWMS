using Application.Interfaces;

namespace Application.Common
{
    /// <summary>قراءة محتوى مرفق من مكانه: مجلد المرفقات إن كان له مفتاح، وإلا جدول المحتوى في القاعدة (المرفقات القديمة ووضع Database)</summary>
    public static class StoredFiles
    {
        public static async Task<Stream> OpenAsync(IFileStore store, string? storageKey, Func<Task<byte[]?>> fromDatabase)
        {
            if (storageKey != null) return store.OpenRead(storageKey);
            var data = await fromDatabase() ?? throw new KeyNotFoundException("محتوى الملف غير موجود");
            return new MemoryStream(data, writable: false);
        }
    }
}
