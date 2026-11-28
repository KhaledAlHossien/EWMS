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
    /// السجل يتبع فرع الدراسات في النظام (BranchId = فرع «فرع الدراسة» المختار)، وحدّ صلاحيات الدراسات = فرع الطلب.
    /// فرع الدراسة (المركزية أو فرع جغرافي) يحدد المسار: المركزية بالسلسلة الكاملة، والجغرافي بالإحالة مباشرة إلى مشروع تنسيق.
    /// </summary>
    public class StudyRequest : IStudyDocument
    {
        public int Id { get; set; }

        public int BranchId { get; set; }
        public Branch Branch { get; set; } = null!;

        public int StudyBranchId { get; set; }
        public StudyBranch StudyBranch { get; set; } = null!;

        /// <summary>الموقع في سجل المواقع المشترك (تظهر الدراسة على خريطة الفرع)</summary>
        public int? SiteId { get; set; }
        public Site? Site { get; set; }

        public int CreatedByUserId { get; set; }
        public User CreatedBy { get; set; } = null!;

        public string RequestNumber { get; set; } = string.Empty;   // رقم الديوان المركزي — إدخال يدوي إلزامي
        public DateTime RequestDate { get; set; }
        public StudyUrgency Urgency { get; set; }

        /// <summary>حقل قديم محفوظ للتوافق مع البيانات السابقة؛ الرقم الرسمي هو RequestNumber</summary>
        public string? DiwanRequestNumber { get; set; }
        public string? MinistryArchiveNumber { get; set; }
        public DateTime? MinistryArchiveDate { get; set; }
        public string? AdminArchiveNumber { get; set; }
        public DateTime? AdminArchiveDate { get; set; }
        public string? BeneficiaryArchiveNumber { get; set; }
        public DateTime? AddedDate { get; set; }

        public string? EntityCode { get; set; }
        public int? MainBeneficiaryId { get; set; }
        public StudyBeneficiary? MainBeneficiaryEntity { get; set; }
        /// <summary>نسخة اسم الجهة وقت الاختيار، تبقى في المستند إذا أُعيدت تسمية الجهة</summary>
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

        /// <summary>التكليف الداخلي (واحد على الأكثر) — مسار المركزية فقط</summary>
        public StudyAssignment? Assignment { get; set; }

        /// <summary>مشروع الدراسة (واحد على الأكثر): من أمر البدء في المركزية، أو من الإحالة إلى فرع جغرافي</summary>
        public StudyProject? Project { get; set; }
    }
}
