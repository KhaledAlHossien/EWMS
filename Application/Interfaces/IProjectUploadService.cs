using Domain.Entities;

namespace Application.Interfaces
{
    /// <summary>إعدادات رفع وثائق المشاريع من إعدادات الخادم (انظر ProjectFileSettings)</summary>
    public interface IProjectFileSettings
    {
        /// <summary>true = المحتوى يُحفظ في مجلد المرفقات (يتسع للملفات الكبيرة)، false = داخل قاعدة البيانات</summary>
        bool UsesFileSystem { get; }
        /// <summary>أكبر ملف مقبول في الوضع الحالي بالبايت</summary>
        long EffectiveMaxBytes { get; }
        long ChunkBytes { get; }
    }

    /// <summary>الأجزاء المؤقتة لعمليات الرفع على القرص (ملف <c>{id}.part</c> لكل جلسة)</summary>
    public interface IProjectUploadFiles
    {
        /// <summary>طول ما كُتب فعلاً من الملف المؤقت (0 إن لم يوجد)</summary>
        long LengthOf(Guid uploadId);

        /// <summary>
        /// يكتب <paramref name="length"/> بايتاً من <paramref name="body"/> عند <paramref name="offset"/> (يقطع الملف عندها أولاً،
        /// فإعادة جزء انقطع في منتصفه آمنة)، ويعيد الطول الجديد. رفع متزامن للجلسة نفسها يُرفض.
        /// </summary>
        Task<long> WriteChunkAsync(Guid uploadId, long offset, Stream body, long length, CancellationToken ct);

        /// <summary>يفتح الملف المؤقت للقراءة (قابل للبحث)</summary>
        Stream OpenPart(Guid uploadId);

        /// <summary>مساحة حرة كافية لهذا الحجم (true إن تعذّر معرفة المساحة)</summary>
        bool HasFreeSpace(long size);

        /// <summary>ينقل الملف المؤقت إلى مجلد المرفقات ويعيد مفتاحه (وضع المجلد فقط)</summary>
        string Promote(Guid uploadId, string area);

        void Delete(Guid uploadId);
    }

    public interface IProjectUploadService
    {
        Task<ProjectUpload?> GetAsync(Guid id);
        Task<List<ProjectUpload>> GetMineAsync(int userId, int? projectId);
        Task<int> CountOpenAsync(int userId);
        Task AddAsync(ProjectUpload upload);

        /// <summary>يحدّث ما استُلم بعد كتابة جزء (تحديث مباشر بلا تحميل الكيان)</summary>
        Task SetReceivedAsync(Guid id, long received);
        Task SetStatusAsync(Guid id, ProjectUploadStatus status, string error = "");
        Task DeleteAsync(Guid id);

        /// <summary>الجلسات التي اكتملت أجزاؤها وتنتظر الإنهاء (الأقدم أولاً)</summary>
        Task<List<Guid>> GetProcessingIdsAsync();

        /// <summary>جلسات أُهملت (جارٍ الرفع ولم تُمسّ منذ receivingBefore) أو انتهت (نجحت أو فشلت قبل finishedBefore): تُمسح مع ملفاتها المؤقتة</summary>
        Task<List<Guid>> GetExpiredIdsAsync(DateTime receivingBefore, DateTime finishedBefore);

        /// <summary>إنهاء لم يتقدم منذ before (توقف في منتصفه): يُعلَّم فاشلاً ليراه المستخدم ويُعيد الرفع</summary>
        Task<int> FailStuckAsync(DateTime before);
    }

    /// <summary>فلاتر بحث الوثائق (كلها اختيارية): النص يطابق العنوان أو اسم أي ملف أو رقم المشروع واسمه</summary>
    public sealed record DocumentFilter(int? ProjectId, ProjectDocumentKind? Kind, string? Search, DateTime? From, DateTime? To);

    /// <summary>بيانات الإصدار الجديد بعد فحص الملف (رقمه يُحجز عند الحفظ)</summary>
    public sealed record NewVersionData(string FileName, string ContentType, long Size, string Sha256, string? StorageKey);

    public interface IProjectDocumentService
    {
        Task<ProjectDocument?> GetDocumentAsync(int id);

        /// <summary>
        /// ينشئ الوثيقة (إن لم تكن موجودة) والإصدار التالي وأثر «رفع» في التدقيق ويُتمّ جلسة الرفع، كلها في معاملة واحدة.
        /// رقم الإصدار يُحجز داخلها (تعارض رفعين متزامنين يُعاد تلقائياً). المحتوى null = حُفظ في المجلد (StorageKey).
        /// </summary>
        Task<ProjectDocumentVersion> AddVersionAsync(Guid uploadId, NewVersionData data, byte[]? content);

        /// <summary>وثائق المشاريع المرئية بالشرط المعطى (الأحدث أولاً) مع إصداراتها ومن رفعها واعتمدها</summary>
        Task<(List<ProjectDocument> Items, int TotalCount)> SearchAsync(System.Linq.Expressions.Expression<Func<Project, bool>> visibleProjects, DocumentFilter filter, int page, int pageSize);

        /// <summary>الوثيقة بكل إصداراتها (ومعها مشروعها بمشاركيه ومستخدميهم كي يُفحص الحدّ في الذاكرة)</summary>
        Task<ProjectDocument?> GetWithVersionsAsync(int id);

        /// <summary>الإصدار مع وثيقته (دون محتواه)</summary>
        Task<ProjectDocumentVersion?> GetVersionAsync(int versionId);

        /// <summary>محتوى إصدار حُفظ في القاعدة (وضع Database)؛ null إن كان في المجلد</summary>
        Task<byte[]?> GetContentAsync(int versionId);

        /// <summary>يعتمد الإصدار ويكتب أثره في معاملة واحدة؛ اعتماده من آخر في اللحظة نفسها يُرفض برسالة واضحة</summary>
        Task ApproveAsync(int versionId, int userId, AuditEntry audit);

        /// <summary>يحذف الوثيقة بإصداراتها ويكتب أثرها (الملفات تُمسح لاحقاً بالمنظّف لأن لا سجل يشير إليها)</summary>
        Task DeleteDocumentAsync(int documentId, AuditEntry audit);
    }

    /// <summary>ينبّه الخدمة الخلفية إلى جلسة اكتمل رفعها فتبدأ إنهاءها دون انتظار الدورة التالية</summary>
    public interface IProjectUploadSignal
    {
        void Notify();
    }
}
