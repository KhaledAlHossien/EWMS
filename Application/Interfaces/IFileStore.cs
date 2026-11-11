namespace Application.Interfaces
{
    /// <summary>
    /// مخزن محتوى المرفقات (قرار المستخدم 2026-10-14). FileStorage:Mode = Database (الافتراضي: المحتوى في جداول …Contents كما كان)
    /// أو FileSystem (المحتوى في المجلد FileStorage:Root — مشاركة SMB يركّبها مدير الخادم كمسار عادي). السجل الذي له StorageKey
    /// يُقرأ من المجلد، والذي بلا مفتاح يُقرأ من القاعدة، فالملفات القديمة تبقى تعمل بعد التحويل.
    /// </summary>
    public interface IFileStore
    {
        /// <summary>
        /// يحفظ المحتوى في المجلد ويعيد مفتاحه، أو null في وضع Database (المستدعي يضعه في جدول المحتوى).
        /// يُستدعى قبل حفظ السجل: إن فشل الحفظ بعدها يبقى ملف يتيم يحذفه المنظّف الدوري.
        /// </summary>
        Task<string?> SaveAsync(string area, byte[] data, CancellationToken ct = default);

        /// <summary>يفتح ملفاً محفوظاً بمفتاحه للقراءة المتدفقة (KeyNotFoundException إن لم يوجد)</summary>
        Stream OpenRead(string key);

        /// <summary>
        /// كتابة متدفقة إلى المجلد **دائماً** أيّاً كان FileStorage:Mode (لملفات كبيرة بلا سقف، مثل مخططات فرع الدراسات):
        /// write يكتب المحتوى في ملف مؤقت داخل المجلد دون تحميله في الذاكرة، ثم يُفحص (OpenRead) ويُعتمد (CommitAsync)،
        /// وإن لم يُعتمد يُحذف عند التخلص منه. يتطلب FileStorage:Root (وإن لم يُضبط فمجلد wwwroot/upload في مجلد مشروع الـ API).
        /// <paramref name="folder"/>: مجلد فرعي للملف داخل المنطقة (مثل <c>projects/12</c> = مجلد المشروع 12)، وبلا مجلد يكون «سنة/شهر».
        /// </summary>
        Task<IStagedFile> StageAsync(string area, string? folder, Func<Stream, CancellationToken, Task> write, CancellationToken ct = default);
    }

    /// <summary>ملف مكتوب في المجلد ولم يُعتمد بعد: يُقرأ للفحص ثم يُعتمد باسمه النهائي، أو يُحذف بالتخلص منه</summary>
    public interface IStagedFile : IAsyncDisposable
    {
        /// <summary>مفتاحه بعد الاعتماد (يُحفظ في السجل)</summary>
        string Key { get; }
        long Size { get; }
        /// <summary>قراءة قابلة للتنقّل من الملف المؤقت (لكشف نوعه من محتواه)</summary>
        Stream OpenRead();
        /// <summary>يجعله الملف النهائي — بعدها لا يُحذف بالتخلص منه</summary>
        Task CommitAsync();
    }

    /// <summary>مصدر ملف مرفوع يُقرأ تدفقاً (يوفّره الـ controller من جسم الطلب) — لا يُحمَّل الملف في الذاكرة</summary>
    public interface IUploadReader
    {
        /// <summary>يكتب أول ملف في الطلب إلى الوجهة ويعيد اسمه الأصلي، أو null إن لم يُرسل ملف</summary>
        Task<string?> CopyFileToAsync(Stream destination, CancellationToken ct);
    }

    /// <summary>مجلدات المرفقات داخل المخزن (الجزء الأول من المفتاح)</summary>
    public static class FileAreas
    {
        public const string Vacations = "vacations";
        public const string Tasks = "tasks";
        public const string MapFiles = "map-files";
        public const string Studies = "studies";

        public static readonly string[] All = [Vacations, Tasks, MapFiles, Studies];
    }

    /// <summary>صيانة المخزن من خدمة الخلفية</summary>
    public interface IFileStoreSweeper
    {
        /// <summary>
        /// يحذف من المجلد الملفات التي لا يشير إليها أي سجل (حُذف سجلها بالحذف المتسلسل، أو فشل حفظه) بعد مهلة أمان،
        /// ثم يعيد عدد المحذوف. لا يفعل شيئاً في وضع Database أو إن لم يحن موعده.
        /// </summary>
        Task<int> SweepAsync(CancellationToken ct = default);
    }
}
