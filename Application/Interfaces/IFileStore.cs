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
    }

    /// <summary>مجلدات المرفقات داخل المخزن (الجزء الأول من المفتاح)</summary>
    public static class FileAreas
    {
        public const string Vacations = "vacations";
        public const string Tasks = "tasks";
        public const string MapFiles = "map-files";

        public static readonly string[] All = [Vacations, Tasks, MapFiles];
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
