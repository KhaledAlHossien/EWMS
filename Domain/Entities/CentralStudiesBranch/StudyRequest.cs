namespace Domain.Entities.CentralStudiesBranch
{
    /// <summary>المستند الرسمي في سلسلة ما قبل المشروع: حالة + منشئ + توقيت + منع الحفظ المتزامن</summary>
    public interface IStudyDocument
    {
        int Id { get; }
        StudyDocumentStatus Status { get; set; }
        int CreatedByUserId { get; set; }
        DateTime CreatedAt { get; set; }
        DateTime? UpdatedAt { get; set; }
    }

    /// <summary>
    /// طلب تجهيز دراسة تكاليف — جذر السلسلة: الطلب ← التكليف (IPA) ← الرد ← أمر البدء (PIO) ← المشروع، كلها 1:1.
    /// السجل يتبع فرع منشئه (BranchId نسخة لحظة الإنشاء)، وحدّ صلاحيات الدراسات = فرع الطلب.
    /// </summary>
    public class StudyRequest : IStudyDocument
    {
        public int Id { get; set; }

        public int BranchId { get; set; }
        public Branch Branch { get; set; } = null!;

        /// <summary>الموقع في سجل المواقع المشترك (تظهر الدراسة على خريطة الفرع)</summary>
        public int? SiteId { get; set; }
        public Site? Site { get; set; }

        public int CreatedByUserId { get; set; }
        public User CreatedBy { get; set; } = null!;

        public string RequestNumber { get; set; } = string.Empty;   // REQ-26-0001 من عدّاد الترقيم
        public DateTime RequestDate { get; set; }
        public StudyUrgency Urgency { get; set; }

        /// <summary>رقم الطلب من الديوان المركزي — يُدخَل يدوياً (لا ربط مع الديوان)، ويُطبع في ترويسة الطلب</summary>
        public string? DiwanRequestNumber { get; set; }
        public string? MinistryArchiveNumber { get; set; }
        public DateTime? MinistryArchiveDate { get; set; }
        public string? AdminArchiveNumber { get; set; }
        public DateTime? AdminArchiveDate { get; set; }
        public string? BeneficiaryArchiveNumber { get; set; }
        public DateTime? AddedDate { get; set; }

        public string? EntityCode { get; set; }
        /// <summary>الجهة المستفيدة الرئيسية — نص الآن، جدول مرجعي لاحقاً</summary>
        public string MainBeneficiary { get; set; } = string.Empty;
        public string? SubBeneficiary { get; set; }

        /// <summary>رمز المحافظة من القائمة الثابتة (SY01…SY14) كما في سجل المواقع</summary>
        public string GovernorateCode { get; set; } = string.Empty;
        public string? Area { get; set; }
        public string? CityOrVillage { get; set; }
        public string? PropertyNature { get; set; }
        public StudyWorkType WorkType { get; set; }

        public string? FollowerName { get; set; }
        public string? FollowerPhone { get; set; }
        public string? Notes { get; set; }

        public StudyDocumentStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public byte[] RowVersion { get; set; } = [];

        /// <summary>التكليف الداخلي (واحد على الأكثر)</summary>
        public StudyAssignment? Assignment { get; set; }
    }
}
